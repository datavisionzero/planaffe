package cmd

import (
	"bytes"
	"encoding/json"
	"errors"
	"fmt"

	"github.com/google/uuid"
	"github.com/spf13/cobra"

	"github.com/datavisionzero/planaffe/src/cli/internal/api"
	"github.com/datavisionzero/planaffe/src/cli/internal/client"
	"github.com/datavisionzero/planaffe/src/cli/internal/config"
	"github.com/datavisionzero/planaffe/src/cli/internal/exit"
	"github.com/datavisionzero/planaffe/src/cli/internal/render"
	"github.com/datavisionzero/planaffe/src/cli/internal/version"
)

// The identity verbs (ADR 0015): a user administers, an agent works. A secret
// is printed once, to stdout, and nowhere else.
func identityCommands(g *globals) []*cobra.Command {
	return []*cobra.Command{newMe(g), newVersion(g), newUser(g), newAgent(g), newToken(g)}
}

func newMe(g *globals) *cobra.Command {
	cmd := &cobra.Command{
		Use: "me", Short: "Who the token says you are: the identity, its kind, its role, the token it came in under.", Args: cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.ReadMeWithResponse(cmd.Context())
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), resp.JSON200)
			}
			render.Me(cmd.OutOrStdout(), *resp.JSON200)
			return nil
		},
	}
	cmd.AddCommand(newMeSet(g))
	return cmd
}

func newMeSet(g *globals) *cobra.Command {
	var kind, harness, environment, harnessVersion string
	cmd := &cobra.Command{
		Use: "set", Short: "Report stable metadata about this agent; `none` clears a field. Agent tokens only.", Args: cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			body := map[string]any{}
			for flag, value := range map[string]string{"kind": kind, "harness": harness, "environment": environment, "version": harnessVersion} {
				if cmd.Flags().Changed(flag) {
					if value == "none" {
						body[flag] = nil
					} else {
						body[flag] = value
					}
				}
			}
			if len(body) == 0 {
				return &config.UsageError{Message: "nothing to report: pass --kind, --harness, --environment or --version."}
			}
			encoded, err := json.Marshal(body)
			if err != nil {
				return err
			}
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.ReportAgentMetadataWithBodyWithResponse(cmd.Context(), "application/json", bytes.NewReader(encoded))
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), resp.JSON200)
			}
			render.Me(cmd.OutOrStdout(), *resp.JSON200)
			return nil
		},
	}
	cmd.Flags().StringVar(&kind, "kind", "", "what the agent is; `none` clears")
	cmd.Flags().StringVar(&harness, "harness", "", "what it runs in; `none` clears")
	cmd.Flags().StringVar(&environment, "environment", "", "where it runs; `none` clears")
	cmd.Flags().StringVar(&harnessVersion, "version", "", "the harness version; `none` clears")
	return cmd
}

// newVersion prints both sides — and, when they do not fit, says so with exit
// 9, the way every other command would have on its first request (ADR 0011).
func newVersion(g *globals) *cobra.Command {
	return &cobra.Command{
		Use: "version", Short: "pa's version and the instance's, and whether the two fit.", Args: cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.ReadVersionWithResponse(cmd.Context())
			if err != nil {
				return client.Transport(err)
			}
			checked := client.Check(resp.HTTPResponse, resp.Body)
			server := ""
			if resp.JSON200 != nil {
				server = resp.JSON200.Version
			}
			if g.json {
				if err := render.JSON(cmd.OutOrStdout(), map[string]string{"pa": version.Version, "planaffe": server}); err != nil {
					return err
				}
			} else {
				fmt.Fprintf(cmd.OutOrStdout(), "pa %s\nplanaffe %s\n", version.Version, server)
			}
			var failure *client.Failure
			if errors.As(checked, &failure) && failure.Code == exit.Skew {
				return failure
			}
			return checked
		},
	}
}

func newUser(g *globals) *cobra.Command {
	cmd := &cobra.Command{Use: "user", Short: "Users: the humans. Administrators only create them."}
	var administrator bool
	create := &cobra.Command{
		Use: "create NAME", Short: "Invite a user: their first user token is printed once — hand it over yourself.", Args: cobra.ExactArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			_, c, err := g.load()
			if err != nil {
				return err
			}
			body := api.CreateUserRequest{Name: &args[0]}
			if administrator {
				body.Administrator = &administrator
			}
			resp, err := c.CreateUserWithResponse(cmd.Context(), body)
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), resp.JSON201)
			}
			fmt.Fprintf(cmd.OutOrStdout(), "%s created%s\ntoken: %s\n", resp.JSON201.Name, adminSuffix(resp.JSON201.Administrator), resp.JSON201.Token.Secret)
			fmt.Fprintln(cmd.ErrOrStderr(), "pa: the token is shown once; the instance keeps a hash and the prefix.")
			return nil
		},
	}
	create.Flags().BoolVar(&administrator, "administrator", false, "administers the instance: users, projects, and everything outside one project")
	list := &cobra.Command{
		Use: "list", Short: "Every user.", Args: cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.ListUsersWithResponse(cmd.Context())
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), resp.JSON200)
			}
			for _, u := range *resp.JSON200 {
				fmt.Fprintf(cmd.OutOrStdout(), "%-24s %s%s\n", u.Name, u.CreatedAt.Format("2006-01-02"), adminSuffix(u.Administrator))
			}
			return nil
		},
	}
	cmd.AddCommand(create, list)
	return cmd
}

func adminSuffix(administrator bool) string {
	if administrator {
		return "  administrator"
	}
	return ""
}

func newAgent(g *globals) *cobra.Command {
	cmd := &cobra.Command{Use: "agent", Short: "Agents: the identities runs work under, each with exactly one token."}
	var name string
	create := &cobra.Command{
		Use: "create [--name NAME]", Short: "Create an agent and its token, printed once. A name is assigned when none is given.", Args: cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.CreateAgentWithResponse(cmd.Context(), api.CreateAgentRequest{Name: optional(name)})
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), resp.JSON201)
			}
			fmt.Fprintf(cmd.OutOrStdout(), "%s created, owned by %s\ntoken: %s\n", resp.JSON201.Name, resp.JSON201.Owner.Name, resp.JSON201.Token.Secret)
			fmt.Fprintln(cmd.ErrOrStderr(), "pa: the token is shown once; give it to one agent only — every agent its own (VISION 12).")
			return nil
		},
	}
	create.Flags().StringVar(&name, "name", "", "the agent's name; two words and a number when left out")
	list := &cobra.Command{
		Use: "list", Short: "Every agent with its owner and its token, revoked ones included.", Args: cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.ListAgentsWithResponse(cmd.Context())
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), resp.JSON200)
			}
			render.Agents(cmd.OutOrStdout(), *resp.JSON200)
			return nil
		},
	}
	view := &cobra.Command{
		Use: "view ID", Short: "One agent with its owner, token state and last reported metadata.", Args: cobra.ExactArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			id, err := agentID(args[0])
			if err != nil {
				return err
			}
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.ListAgentsWithResponse(cmd.Context())
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			for _, agent := range *resp.JSON200 {
				if agent.Id == id {
					if g.json {
						return render.JSON(cmd.OutOrStdout(), agent)
					}
					render.Agent(cmd.OutOrStdout(), agent)
					return nil
				}
			}
			return &client.Failure{Code: exit.NotFound, Message: fmt.Sprintf("no agent %s", id)}
		},
	}
	var newName string
	rename := &cobra.Command{
		Use: "rename ID --name NAME", Short: "Rename an agent; the history keeps the id, so old entries show the new name.", Args: cobra.ExactArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			if newName == "" {
				return &config.UsageError{Message: "--name NAME: the new name."}
			}
			id, err := agentID(args[0])
			if err != nil {
				return err
			}
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.RenameAgentWithResponse(cmd.Context(), id, api.RenameAgentRequest{Name: &newName})
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), resp.JSON200)
			}
			fmt.Fprintf(cmd.OutOrStdout(), "%s renamed to %s\n", args[0], resp.JSON200.Name)
			return nil
		},
	}
	rename.Flags().StringVar(&newName, "name", "", "the new name")
	revoke := &cobra.Command{
		Use: "revoke ID", Short: "Revoke an agent's token. The identity stays, naming the agent in everything it did.", Args: cobra.ExactArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			id, err := agentID(args[0])
			if err != nil {
				return err
			}
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.RevokeAgentWithResponse(cmd.Context(), id)
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), map[string]any{"id": id, "revoked": true})
			}
			fmt.Fprintf(cmd.OutOrStdout(), "%s revoked\n", args[0])
			return nil
		},
	}
	cmd.AddCommand(create, list, view, rename, revoke)
	return cmd
}

func agentID(s string) (uuid.UUID, error) {
	id, err := uuid.Parse(s)
	if err != nil {
		return uuid.Nil, &config.UsageError{Message: fmt.Sprintf("%q is not an agent id; `pa agent list` prints them.", s)}
	}
	return id, nil
}

func newToken(g *globals) *cobra.Command {
	cmd := &cobra.Command{Use: "token", Short: "Your own user tokens: as many as you create, each revocable on its own."}
	create := &cobra.Command{
		Use: "create", Short: "A further token for you, printed once.", Args: cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.CreateTokenWithResponse(cmd.Context())
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), resp.JSON201)
			}
			fmt.Fprintf(cmd.OutOrStdout(), "token: %s\n", resp.JSON201.Secret)
			fmt.Fprintln(cmd.ErrOrStderr(), "pa: the token is shown once.")
			return nil
		},
	}
	list := &cobra.Command{
		Use: "list", Short: "Your tokens, revoked ones included.", Args: cobra.NoArgs,
		RunE: func(cmd *cobra.Command, _ []string) error {
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.ListTokensWithResponse(cmd.Context())
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), resp.JSON200)
			}
			for _, t := range *resp.JSON200 {
				state := "active"
				if t.RevokedAt != nil {
					state = "revoked " + t.RevokedAt.Format("2006-01-02")
				}
				fmt.Fprintf(cmd.OutOrStdout(), "%-36s %s…  %s  %s\n", t.Id, t.Prefix, t.CreatedAt.Format("2006-01-02"), state)
			}
			return nil
		},
	}
	revoke := &cobra.Command{
		Use: "revoke ID", Short: "Revoke one of your own tokens.", Args: cobra.ExactArgs(1),
		RunE: func(cmd *cobra.Command, args []string) error {
			id, err := uuid.Parse(args[0])
			if err != nil {
				return &config.UsageError{Message: fmt.Sprintf("%q is not a token id; `pa token list` prints them.", args[0])}
			}
			_, c, err := g.load()
			if err != nil {
				return err
			}
			resp, err := c.RevokeTokenWithResponse(cmd.Context(), id)
			if err != nil {
				return client.Transport(err)
			}
			if err := client.Check(resp.HTTPResponse, resp.Body); err != nil {
				return err
			}
			if g.json {
				return render.JSON(cmd.OutOrStdout(), map[string]any{"id": id, "revoked": true})
			}
			fmt.Fprintf(cmd.OutOrStdout(), "%s revoked\n", args[0])
			return nil
		},
	}
	cmd.AddCommand(create, list, revoke)
	return cmd
}
