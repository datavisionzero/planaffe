package config

import (
	"errors"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func env(values map[string]string) func(string) string {
	return func(key string) string { return values[key] }
}

func in(values map[string]string, dir string) Input {
	return Input{Getenv: env(values), Dir: dir}
}

func TestResolveNeedsAnInstanceAndAToken(t *testing.T) {
	if _, err := Resolve(in(map[string]string{"PLANAFFE_TOKEN": "t"}, t.TempDir())); err == nil ||
		!strings.Contains(err.Error(), "pa login --url") {
		t.Fatalf("expected a usage error naming `pa login --url`, got %v", err)
	}
	if _, err := Resolve(in(map[string]string{"PLANAFFE_URL": "https://x.example"}, t.TempDir())); err == nil ||
		!strings.Contains(err.Error(), "run `pa login`") {
		t.Fatalf("expected a usage error naming `pa login`, got %v", err)
	}
	if _, err := Resolve(in(map[string]string{"PLANAFFE_URL": "x.example", "PLANAFFE_TOKEN": "t"}, t.TempDir())); err == nil {
		t.Fatal("expected a usage error for a relative URL")
	}
}

func TestTheEnvironmentWinsOverEverythingElse(t *testing.T) {
	input := Input{
		Getenv:       env(map[string]string{"PLANAFFE_URL": "https://env.example", "PLANAFFE_TOKEN": "from-env"}),
		Dir:          t.TempDir(),
		Settings:     Settings{Instance: "https://kept.example"},
		ReadKeychain: func(string) (string, error) { return "from-keychain", nil },
	}

	cfg, err := Resolve(input)
	if err != nil {
		t.Fatal(err)
	}
	// An agent's own token must never be displaced by a login somebody did on
	// this machine (ADR 0025).
	if cfg.URL != "https://env.example" || cfg.Token != "from-env" || cfg.TokenFrom != EnvToken {
		t.Fatalf("unexpected config %+v", cfg)
	}
}

func TestWithoutTheEnvironmentTheKeychainAnswers(t *testing.T) {
	var asked string
	input := Input{
		Getenv:   env(nil),
		Dir:      t.TempDir(),
		Settings: Settings{Instance: "https://kept.example/"},
		ReadKeychain: func(instance string) (string, error) {
			asked = instance
			return "  from-keychain\n", nil
		},
	}

	cfg, err := Resolve(input)
	if err != nil {
		t.Fatal(err)
	}
	if cfg.URL != "https://kept.example" || asked != "https://kept.example" {
		t.Fatalf("unexpected instance %q, asked %q", cfg.URL, asked)
	}
	if cfg.Token != "from-keychain" || cfg.TokenFrom != Keychain {
		t.Fatalf("unexpected token %+v", cfg)
	}
}

func TestATokenFileComesBeforeTheKeychainAndRefusesLooseModes(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "token")
	if err := WriteTokenFile(path, "from-file"); err != nil {
		t.Fatal(err)
	}

	input := Input{
		Getenv:       env(nil),
		Dir:          dir,
		Settings:     Settings{Instance: "https://kept.example", TokenFile: path},
		ReadKeychain: func(string) (string, error) { return "from-keychain", nil },
	}

	cfg, err := Resolve(input)
	if err != nil {
		t.Fatal(err)
	}
	if cfg.Token != "from-file" || cfg.TokenFrom != path {
		t.Fatalf("unexpected token %+v", cfg)
	}

	// A file mode is the only protection a token in a file has.
	if err := os.Chmod(path, 0o644); err != nil {
		t.Fatal(err)
	}
	if _, err := Resolve(input); err == nil || !strings.Contains(err.Error(), "chmod 600") {
		t.Fatalf("expected a refusal naming chmod, got %v", err)
	}
}

func TestProjectFileIsFoundUpwardsAndParsed(t *testing.T) {
	root := t.TempDir()
	if err := os.WriteFile(filepath.Join(root, FileName), []byte("# the project of this repository\nproject = plan\nrepo = repo/api\n"), 0o644); err != nil {
		t.Fatal(err)
	}
	nested := filepath.Join(root, "src", "deep")
	if err := os.MkdirAll(nested, 0o755); err != nil {
		t.Fatal(err)
	}

	cfg, err := Resolve(in(map[string]string{"PLANAFFE_URL": "https://x.example", "PLANAFFE_TOKEN": "t"}, nested))
	if err != nil {
		t.Fatal(err)
	}
	if cfg.Project != "PLAN" || cfg.Repo != "repo/api" || cfg.File != filepath.Join(root, FileName) {
		t.Fatalf("unexpected config %+v", cfg)
	}
}

func TestProjectFileRefusesWhatItDoesNotKnow(t *testing.T) {
	dir := t.TempDir()
	if err := os.WriteFile(filepath.Join(dir, FileName), []byte("projekt = PLAN\n"), 0o644); err != nil {
		t.Fatal(err)
	}
	_, err := Resolve(in(map[string]string{"PLANAFFE_URL": "https://x.example", "PLANAFFE_TOKEN": "t"}, dir))
	if err == nil || !strings.Contains(err.Error(), "unknown key") {
		t.Fatalf("expected an unknown-key error, got %v", err)
	}

	if err := os.WriteFile(filepath.Join(dir, FileName), []byte("repo = x\n"), 0o644); err != nil {
		t.Fatal(err)
	}
	if _, err := Resolve(in(map[string]string{"PLANAFFE_URL": "https://x.example", "PLANAFFE_TOKEN": "t"}, dir)); err == nil {
		t.Fatal("expected an error for a file without a project")
	}
}

func TestWithoutAFileThereIsNoProject(t *testing.T) {
	cfg, err := Resolve(in(map[string]string{"PLANAFFE_URL": "https://x.example", "PLANAFFE_TOKEN": "t"}, t.TempDir()))
	if err != nil {
		t.Fatal(err)
	}
	if cfg.Project != "" || cfg.File != "" {
		t.Fatalf("unexpected config %+v", cfg)
	}
}

func TestSettingsRoundTripAndHoldNoCredential(t *testing.T) {
	path := filepath.Join(t.TempDir(), "planaffe", "config")

	if settings, err := ReadSettings(path); err != nil || settings != (Settings{}) {
		t.Fatalf("a machine nobody logged in on is not an error: %+v %v", settings, err)
	}

	written := Settings{Instance: "https://kept.example", TokenFile: "/home/somebody/.config/planaffe/token"}
	if err := WriteSettings(path, written); err != nil {
		t.Fatal(err)
	}

	read, err := ReadSettings(path)
	if err != nil {
		t.Fatal(err)
	}
	if read != written {
		t.Fatalf("read %+v, wrote %+v", read, written)
	}

	body, err := os.ReadFile(path)
	if err != nil {
		t.Fatal(err)
	}
	if strings.Contains(string(body), "pa_") {
		t.Fatal("nothing in the configuration file is a credential")
	}
}

func TestSettingsRefuseAKeyTheyDoNotKnow(t *testing.T) {
	path := filepath.Join(t.TempDir(), "config")
	if err := os.WriteFile(path, []byte("instanz = https://x.example\n"), 0o600); err != nil {
		t.Fatal(err)
	}
	var usage *UsageError
	if _, err := ReadSettings(path); err == nil || !errors.As(err, &usage) || !strings.Contains(err.Error(), "unknown key") {
		t.Fatalf("expected an unknown-key error, got %v", err)
	}
}

func TestSettingsPathFollowsXdgThenHome(t *testing.T) {
	path, err := SettingsPath(env(map[string]string{"XDG_CONFIG_HOME": "/somewhere/config"}))
	if err != nil {
		t.Fatal(err)
	}
	if path != filepath.Join("/somewhere/config", "planaffe", "config") {
		t.Fatalf("unexpected path %q", path)
	}

	path, err = SettingsPath(env(map[string]string{"HOME": "/home/somebody"}))
	if err != nil {
		t.Fatal(err)
	}
	if path != filepath.Join("/home/somebody", ".config", "planaffe", "config") {
		t.Fatalf("unexpected path %q", path)
	}
}
