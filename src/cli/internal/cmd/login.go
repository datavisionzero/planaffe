package cmd

import (
	"context"
	"errors"
	"fmt"
	"net/url"
	"os"
	"strings"
	"time"

	"github.com/spf13/cobra"

	"github.com/datavisionzero/planaffe/src/cli/internal/api"
	"github.com/datavisionzero/planaffe/src/cli/internal/client"
	"github.com/datavisionzero/planaffe/src/cli/internal/config"
	"github.com/datavisionzero/planaffe/src/cli/internal/exit"
	"github.com/datavisionzero/planaffe/src/cli/internal/keychain"
	"github.com/datavisionzero/planaffe/src/cli/internal/render"
)

func newLogin(g *globals) *cobra.Command {
	var address, tokenFile string

	cmd := &cobra.Command{
		Use:   "login",
		Short: "Sign in through a browser on any machine, and keep the user token in the keychain.",
		Long: "The device-code flow: pa prints a short code and a person confirms it in a\n" +
			"browser — on this machine or on another one. That is the only login that\n" +
			"works over SSH, in CI, in a container and in an agent's sandbox, where there\n" +
			"is no browser to open.\n\n" +
			"What it collects is an ordinary user token, the same thing `pa token create`\n" +
			"makes; it goes into the operating system's keychain. Where there is none, pa\n" +
			"says so and names the two ways on rather than quietly writing it to a file.\n\n" +
			"An agent's token never comes through here: it arrives in " + config.EnvToken + ".",
		Args: cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			return g.login(cmd.Context(), strings.TrimSpace(address), strings.TrimSpace(tokenFile))
		},
	}
	cmd.Flags().StringVar(&address, "url", "", "the instance to sign in to; remembered for the next command")
	cmd.Flags().StringVar(&tokenFile, "token-file", "",
		"write the token to this file instead of the keychain, readable only by you")
	return cmd
}

func (g *globals) login(ctx context.Context, address, tokenFile string) error {
	settings, err := g.readSettings()
	if err != nil {
		return err
	}
	if address != "" {
		settings.Instance = address
	}

	resolved, c, err := g.anonymous(settings)
	if err != nil {
		return err
	}

	// Before a code is printed, let alone a token collected: is this a planaffe
	// instance, and do the two of us fit (ADR 0011). A wrong address should
	// fail here, not after somebody has walked to another machine.
	handshake, err := c.ReadVersionWithResponse(ctx)
	if err != nil {
		return client.Transport(err)
	}
	if err := client.Check(handshake.HTTPResponse, handshake.Body); err != nil {
		return err
	}

	begun, err := c.BeginDeviceLoginWithResponse(ctx)
	if err != nil {
		return client.Transport(err)
	}
	if err := client.Check(begun.HTTPResponse, begun.Body); err != nil {
		return err
	}
	if begun.JSON200 == nil {
		return &client.Failure{Code: exit.Unexpected, Message: "the instance began a login pa cannot read"}
	}
	login := *begun.JSON200

	// Both addresses arrive relative to the instance, and pa is the one that
	// joins the halves: what it prints is read by a person who has to open it,
	// and `/device` is not something anybody can open.
	fmt.Fprintf(g.msg(), "Open %s and enter this code:\n\n    %s\n\n", at(resolved, login.VerificationUri), login.UserCode)
	if login.VerificationUriComplete != "" {
		fmt.Fprintf(g.msg(), "Or open the code's own address: %s\n\n", at(resolved, login.VerificationUriComplete))
	}
	fmt.Fprintf(g.msg(), "Waiting. The code is good for %s.\n", minutes(login.ExpiresInSeconds))

	collected, err := g.poll(ctx, c, login)
	if err != nil {
		return err
	}

	settings.Instance = resolved
	if err := g.keep(&settings, collected.Token.Secret, tokenFile); err != nil {
		return err
	}

	fmt.Fprintf(g.msg(), "Signed in to %s as %s.\n", resolved, collected.User.Name)

	if g.json {
		// The secret is what this command just put in the keychain, and
		// printing it here would put it in the terminal it was kept out of.
		return render.JSON(g.out(), map[string]any{
			"instance":      resolved,
			"user":          collected.User,
			"email":         collected.Email,
			"administrator": collected.Administrator,
			"token":         map[string]any{"id": collected.Token.Id, "prefix": collected.Token.Prefix},
		})
	}
	return nil
}

// poll collects the token once a human has confirmed. Which refusal comes back
// is the whole protocol: `device-pending` means keep asking, and everything
// else means stop (docs/api.md).
func (g *globals) poll(ctx context.Context, c *client.Client, login api.DeviceLoginBegun) (api.DeviceLoginRedeemed, error) {
	interval := time.Duration(login.IntervalSeconds) * time.Second
	if interval <= 0 {
		interval = 5 * time.Second
	}
	deadline := time.Now().Add(time.Duration(login.ExpiresInSeconds) * time.Second)

	for {
		resp, err := c.RedeemDeviceLoginWithResponse(ctx, api.RedeemDeviceLoginJSONRequestBody{DeviceCode: &login.DeviceCode})
		if err != nil {
			return api.DeviceLoginRedeemed{}, client.Transport(err)
		}

		checked := client.Check(resp.HTTPResponse, resp.Body)
		if checked == nil {
			if resp.JSON200 == nil {
				return api.DeviceLoginRedeemed{}, &client.Failure{
					Code: exit.Unexpected, Message: "the instance answered a login pa cannot read"}
			}
			return *resp.JSON200, nil
		}

		var failure *client.Failure
		if !errors.As(checked, &failure) || failure.Problem.Code() != "device-pending" {
			return api.DeviceLoginRedeemed{}, checked
		}

		if time.Now().After(deadline) {
			return api.DeviceLoginRedeemed{}, &client.Failure{
				Code:    exit.Denied,
				Message: "nobody confirmed that login in time.",
			}
		}

		select {
		case <-ctx.Done():
			return api.DeviceLoginRedeemed{}, ctx.Err()
		case <-time.After(interval):
		}
	}
}

// keep puts the token where it belongs and records where that was, so that the
// next command finds it without being told again.
func (g *globals) keep(settings *config.Settings, secret, tokenFile string) error {
	if tokenFile != "" {
		if err := config.WriteTokenFile(tokenFile, secret); err != nil {
			return err
		}
		settings.TokenFile = tokenFile
		fmt.Fprintf(g.msg(), "The token is in %s, readable only by you.\n", tokenFile)
		return g.writeSettings(*settings)
	}

	if err := g.store().Set(settings.Instance, secret); err != nil {
		if errors.Is(err, keychain.ErrUnavailable) {
			return &config.UsageError{Message: keychain.Advice}
		}
		return err
	}

	settings.TokenFile = ""
	return g.writeSettings(*settings)
}

func newLogout(g *globals) *cobra.Command {
	return &cobra.Command{
		Use:   "logout",
		Short: "Revoke this machine's token and take it out of the keychain.",
		Args:  cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			return g.logout(cmd.Context())
		},
	}
}

func (g *globals) logout(ctx context.Context) error {
	cfg, c, err := g.load()
	if err != nil {
		return err
	}

	// A token that came out of the environment is an agent's or CI's, and pa
	// did not put it there. Revoking it from here would revoke something the
	// person at this terminal may not even know they are holding.
	if cfg.TokenFrom == config.EnvToken {
		return &config.UsageError{Message: fmt.Sprintf(
			"the token in use came from %s, and pa did not put it there: unset the variable, or revoke that token with `pa token revoke`.",
			config.EnvToken)}
	}

	resp, err := c.RevokeMyTokenWithResponse(ctx)
	if err != nil {
		return client.Transport(err)
	}
	// A token the instance no longer knows is one this machine should stop
	// holding either: the local half of a logout is worth doing regardless.
	revoked := client.Check(resp.HTTPResponse, resp.Body)

	settings, err := g.readSettings()
	if err != nil {
		return err
	}
	if settings.TokenFile != "" {
		if err := config.WriteTokenFile(settings.TokenFile, ""); err != nil {
			return err
		}
		settings.TokenFile = ""
	}
	if err := g.store().Delete(cfg.URL); err != nil && !errors.Is(err, keychain.ErrUnavailable) {
		return err
	}
	if err := g.writeSettings(settings); err != nil {
		return err
	}

	if revoked != nil {
		fmt.Fprintf(g.msg(), "The token is gone from this machine. The instance answered: %v\n", revoked)
		return nil
	}
	fmt.Fprintf(g.msg(), "Signed out of %s.\n", cfg.URL)
	return nil
}

// anonymous is the client for the one command that talks to an instance with
// nothing in its hand.
func (g *globals) anonymous(settings config.Settings) (string, *client.Client, error) {
	getenv := g.env.Getenv
	if getenv == nil {
		getenv = os.Getenv
	}

	address, err := config.Input{Getenv: getenv, Settings: settings}.ResolveURL()
	if err != nil {
		return "", nil, err
	}

	httpClient := g.env.HTTP
	if httpClient == nil {
		httpClient = client.Default()
	}

	c, err := client.New(config.Config{URL: address}, httpClient)
	return address, c, err
}

// at reads a reference the instance gave against the address pa is talking to.
// A reference that is already absolute survives untouched, so an instance that
// one day answers with a whole URL is not broken by this.
func at(address, reference string) string {
	base, err := url.Parse(address)
	if err != nil {
		return reference
	}

	ref, err := url.Parse(reference)
	if err != nil {
		return reference
	}

	return base.ResolveReference(ref).String()
}

func minutes(seconds int32) string {
	if seconds < 120 {
		return fmt.Sprintf("%d seconds", seconds)
	}
	return fmt.Sprintf("%d minutes", seconds/60)
}
