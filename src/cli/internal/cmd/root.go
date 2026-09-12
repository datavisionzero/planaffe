// Package cmd is the command tree: `pa <object> <verb>`, like gh and glab
// (VISION 6.1). Data goes to stdout, errors to stderr, and the exit code says
// what happened (docs/api.md, Exit codes of the CLI).
package cmd

import (
	"context"
	"errors"
	"fmt"
	"io"
	"net/http"
	"os"

	"github.com/spf13/cobra"

	"github.com/datavisionzero/planaffe/src/cli/internal/client"
	"github.com/datavisionzero/planaffe/src/cli/internal/config"
	"github.com/datavisionzero/planaffe/src/cli/internal/exit"
	"github.com/datavisionzero/planaffe/src/cli/internal/keychain"
	"github.com/datavisionzero/planaffe/src/cli/internal/version"
)

// Env is what a command runs in, so that a test can supply all of it.
type Env struct {
	Getenv func(string) string
	Dir    string
	Stdin  io.Reader
	Stdout io.Writer
	Stderr io.Writer
	HTTP   *http.Client
	// Store is where `login` keeps a token; the zero value is the machine's
	// own keychain. A test supplies its own so that nothing touches the store
	// the person running the tests signs in with.
	Store *keychain.Store
	// Settings is where pa's own configuration file is; empty is the real place.
	Settings string
}

// Run executes args and returns the exit code. Nothing here is ever
// interactive: pa reads stdin only where a flag says so, and never prompts.
func Run(ctx context.Context, args []string, env Env) int {
	root := newRoot(env)
	root.SetArgs(args)
	root.SetIn(env.Stdin)
	root.SetOut(env.Stdout)
	root.SetErr(env.Stderr)

	if err := root.ExecuteContext(ctx); err != nil {
		return report(env.Stderr, err)
	}
	return exit.OK
}

func report(stderr io.Writer, err error) int {
	var failure *client.Failure
	var usage *config.UsageError
	var empty emptyResult
	switch {
	case errors.As(err, &empty):
		// Not an error: the reasons went to stdout, and the code says it.
		return exit.Empty
	case errors.As(err, &failure):
		fmt.Fprintln(stderr, "pa:", failure.Message)
		return failure.Code
	case errors.As(err, &usage):
		fmt.Fprintln(stderr, "pa:", usage.Message)
		return exit.Usage
	default:
		fmt.Fprintln(stderr, "pa:", err)
		return exit.Unexpected
	}
}

type globals struct {
	env     Env
	json    bool
	project string
}

func newRoot(env Env) *cobra.Command {
	g := &globals{env: env}
	root := &cobra.Command{
		Use:           "pa",
		Short:         "planaffe from the console: the interface for agents and console-minded humans.",
		Version:       version.Version,
		SilenceUsage:  true,
		SilenceErrors: true,
	}
	root.PersistentFlags().BoolVar(&g.json, "json", false, "print the object as the API answered it")
	root.PersistentFlags().StringVar(&g.project, "project", "", "the project key; defaults to the .planaffe file of this repository")
	root.SetVersionTemplate("pa {{.Version}}\n")

	// A usage mistake is exit 2, in the words of the flag package rather than a
	// wall of help.
	root.SetFlagErrorFunc(func(_ *cobra.Command, err error) error {
		return &config.UsageError{Message: err.Error()}
	})

	root.AddCommand(newLogin(g), newLogout(g))
	root.AddCommand(newInit(g), newNext(g), newNeedsYou(g), newStanding(g), newExport(g), newIssue(g), newQuestion(g), newProject(g), newLabel(g), newEpic(g), newPage(g), newSpace(g), newRelease(g))
	root.AddCommand(identityCommands(g)...)
	return root
}

// load is what every command that talks to the instance starts with.
func (g *globals) load() (config.Config, *client.Client, error) {
	return g.loadForWait(0)
}

func (g *globals) loadForWait(seconds int) (config.Config, *client.Client, error) {
	getenv := g.env.Getenv
	if getenv == nil {
		getenv = os.Getenv
	}
	dir := g.env.Dir
	if dir == "" {
		dir, _ = os.Getwd()
	}

	settings, err := g.readSettings()
	if err != nil {
		return config.Config{}, nil, err
	}

	store := g.store()
	cfg, err := config.Resolve(config.Input{
		Getenv:       getenv,
		Dir:          dir,
		Settings:     settings,
		ReadKeychain: store.Get,
	})
	if err != nil {
		return cfg, nil, err
	}
	if g.project != "" {
		cfg.Project = g.project
	}

	httpClient := g.env.HTTP
	if httpClient == nil {
		if seconds > 0 {
			httpClient = client.ForWait(seconds)
		} else {
			httpClient = client.Default()
		}
	}

	c, err := client.New(cfg, httpClient)
	return cfg, c, err
}

// dir is the repository pa was run in.
func (g *globals) dir() string {
	if g.env.Dir != "" {
		return g.env.Dir
	}
	dir, _ := os.Getwd()
	return dir
}

// getenv is the environment this invocation runs in — the test's, where it
// supplied one.
func (g *globals) getenv(name string) string {
	if g.env.Getenv != nil {
		return g.env.Getenv(name)
	}
	return os.Getenv(name)
}

// requireProject is the one thing a command in a repository never has to say.
func requireProject(cfg config.Config) (string, error) {
	if cfg.Project == "" {
		return "", &config.UsageError{Message: "no project: pass --project KEY, or put a .planaffe file with `project = KEY` in the repository."}
	}
	return cfg.Project, nil
}

// store is the keychain this invocation uses: the machine's own, unless a test
// supplied one.
func (g *globals) store() keychain.Store {
	if g.env.Store != nil {
		return *g.env.Store
	}
	return keychain.System()
}

// settingsPath is where pa keeps what it knows between invocations.
func (g *globals) settingsPath() (string, error) {
	if g.env.Settings != "" {
		return g.env.Settings, nil
	}
	getenv := g.env.Getenv
	if getenv == nil {
		getenv = os.Getenv
	}
	return config.SettingsPath(getenv)
}

func (g *globals) readSettings() (config.Settings, error) {
	path, err := g.settingsPath()
	if err != nil {
		return config.Settings{}, err
	}
	return config.ReadSettings(path)
}

func (g *globals) writeSettings(settings config.Settings) error {
	path, err := g.settingsPath()
	if err != nil {
		return err
	}
	return config.WriteSettings(path, settings)
}

// msg is where pa says things to a person: stderr, so that stdout stays the
// data an agent parses (docs/cli.md, Commitments to agents).
func (g *globals) msg() io.Writer {
	if g.env.Stderr == nil {
		return os.Stderr
	}
	return g.env.Stderr
}

// out is where the data goes.
func (g *globals) out() io.Writer {
	if g.env.Stdout == nil {
		return os.Stdout
	}
	return g.env.Stdout
}
