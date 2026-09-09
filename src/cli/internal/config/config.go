// Package config is where pa learns which instance it talks to and as whom:
// two environment variables, the configuration file `pa login` writes, the
// keychain it keeps a token in, and an optional .planaffe file in the
// repository that fixes the project (VISION 6.1, 13; ADR 0025).
package config

import (
	"bufio"
	"errors"
	"fmt"
	"net/url"
	"os"
	"path/filepath"
	"strings"
)

// FileName is the project file: checked in at the root of a repository, it
// points from that repository at exactly one project and, where one project
// spans several repositories, names the `repo` label of this one (CONTEXT.md,
// Project file).
const FileName = ".planaffe"

// The environment, in one place because it is a contract with CI, with
// containers and with whatever harness starts an agent.
const (
	EnvURL   = "PLANAFFE_URL"
	EnvToken = "PLANAFFE_TOKEN"
)

// Keychain is what ResolveToken says a token came from when it read one.
const Keychain = "the keychain"

// Config is what a command runs with.
type Config struct {
	// URL is the instance, from PLANAFFE_URL or from what `pa login` wrote.
	URL string
	// Token is the caller's token; the server tells a user token from an agent
	// token, pa never says which it holds (ADR 0015).
	Token string
	// TokenFrom is where that token was read: PLANAFFE_TOKEN, the path of a
	// token file, or the keychain. `pa me` shows it, and `pa logout` refuses to
	// revoke what pa did not put there.
	TokenFrom string
	// Project is the project key, from the file or from --project.
	Project string
	// Repo is the `repo` label of this repository, from the file, or empty.
	Repo string
	// File is where the project file was found, or empty.
	File string
}

// Input is everything Resolve reads, so that a test supplies all of it and
// nothing reaches around it to the real machine.
type Input struct {
	Getenv func(string) string
	Dir    string
	// Settings is pa's own configuration file, already read.
	Settings Settings
	// ReadKeychain answers the token this machine kept for an instance. Nil
	// where there is no store to ask, which is not an error until nothing else
	// answered either.
	ReadKeychain func(instance string) (string, error)
}

// UsageError is a mistake in the environment or the arguments: exit 2.
type UsageError struct{ Message string }

func (e *UsageError) Error() string { return e.Message }

// Resolve is the whole ladder: the address, the token and the project file.
func Resolve(in Input) (Config, error) {
	address, err := in.ResolveURL()
	if err != nil {
		return Config{}, err
	}

	token, from, err := in.ResolveToken(address)
	if err != nil {
		return Config{URL: address}, err
	}

	cfg := Config{URL: address, Token: token, TokenFrom: from}

	dir := in.Dir
	if dir == "" {
		dir, _ = os.Getwd()
	}
	path, found := find(dir)
	if found {
		file, err := parse(path)
		if err != nil {
			return cfg, &UsageError{fmt.Sprintf("%s: %v", path, err)}
		}
		cfg.File = path
		cfg.Project = file.project
		cfg.Repo = file.repo
	}

	return cfg, nil
}

// ResolveURL answers which instance this invocation talks to: the environment,
// then the instance `pa login` wrote down.
func (in Input) ResolveURL() (string, error) {
	address := strings.TrimSpace(in.getenv(EnvURL))
	if address == "" {
		address = strings.TrimSpace(in.Settings.Instance)
	}
	if address == "" {
		return "", &UsageError{fmt.Sprintf(
			"no instance: set %s, or run `pa login --url https://planaffe.example`.", EnvURL)}
	}

	address = strings.TrimRight(address, "/")
	if u, err := url.Parse(address); err != nil || !u.IsAbs() || (u.Scheme != "http" && u.Scheme != "https") {
		return "", &UsageError{fmt.Sprintf(
			"%s is %q; it has to be an absolute http or https address.", EnvURL, address)}
	}
	return address, nil
}

// ResolveToken answers the token and where it came from.
//
// The environment wins, always: it is how an agent receives its own token and
// how CI holds one, so a `pa login` on the machine can never quietly
// re-identify a run (ADR 0025). A file the user named out loud is next, because
// they named it. The keychain is last, and is where `login` puts a token unless
// it was told otherwise.
func (in Input) ResolveToken(address string) (token string, from string, err error) {
	if value := strings.TrimSpace(in.getenv(EnvToken)); value != "" {
		return value, EnvToken, nil
	}

	if path := strings.TrimSpace(in.Settings.TokenFile); path != "" {
		value, err := ReadTokenFile(path)
		if err != nil {
			return "", "", err
		}
		return value, path, nil
	}

	if in.ReadKeychain != nil {
		if value, err := in.ReadKeychain(address); err == nil && strings.TrimSpace(value) != "" {
			return strings.TrimSpace(value), Keychain, nil
		}
	}

	return "", "", &UsageError{fmt.Sprintf(
		"no token for %s: run `pa login`, or put a user token or an agent token in %s.", address, EnvToken)}
}

func (in Input) getenv(name string) string {
	if in.Getenv == nil {
		return ""
	}
	return in.Getenv(name)
}

type projectFile struct {
	project string
	repo    string
}

// find walks up from dir to the root, the way git finds its own directory.
func find(dir string) (string, bool) {
	for {
		candidate := filepath.Join(dir, FileName)
		if info, err := os.Stat(candidate); err == nil && !info.IsDir() {
			return candidate, true
		}

		parent := filepath.Dir(dir)
		if parent == dir {
			return "", false
		}
		dir = parent
	}
}

// parse reads `key = value` lines; `#` starts a comment. Two keys are known,
// `project` and `repo`, and anything else is a mistake rather than ignored — a
// misspelt `projekt` that silently did nothing would send every command to the
// wrong project.
func parse(path string) (projectFile, error) {
	f, err := os.Open(path)
	if err != nil {
		return projectFile{}, err
	}
	defer f.Close()

	var file projectFile
	scanner := bufio.NewScanner(f)
	line := 0
	for scanner.Scan() {
		line++
		text := strings.TrimSpace(scanner.Text())
		if text == "" || strings.HasPrefix(text, "#") {
			continue
		}

		key, value, ok := strings.Cut(text, "=")
		if !ok {
			return projectFile{}, fmt.Errorf("line %d: expected `key = value`", line)
		}

		key, value = strings.TrimSpace(key), strings.TrimSpace(value)
		switch key {
		case "project":
			file.project = strings.ToUpper(value)
		case "repo":
			file.repo = value
		default:
			return projectFile{}, fmt.Errorf("line %d: unknown key %q; the file knows `project` and `repo`", line, key)
		}
	}
	if err := scanner.Err(); err != nil {
		return projectFile{}, err
	}

	if file.project == "" {
		return projectFile{}, errors.New("no `project = KEY` line")
	}

	return file, nil
}
