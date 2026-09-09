package cmd

import (
	"bytes"
	"context"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"strings"
	"testing"

	"github.com/datavisionzero/planaffe/src/cli/internal/config"
	"github.com/datavisionzero/planaffe/src/cli/internal/exit"
	"github.com/datavisionzero/planaffe/src/cli/internal/keychain"
)

// store is a keychain in a map: the injected one of ADR 0025, so that no test
// touches the store belonging to whoever is running them.
type store struct {
	entries   map[string]string
	deleted   []string
	available bool
}

func newStore() *store { return &store{entries: map[string]string{}, available: true} }

func (s *store) asStore() *keychain.Store {
	return &keychain.Store{
		Set: func(instance, token string) error {
			if !s.available {
				return keychain.ErrUnavailable
			}
			s.entries[instance] = token
			return nil
		},
		Get: func(instance string) (string, error) {
			if !s.available {
				return "", keychain.ErrUnavailable
			}
			if token, ok := s.entries[instance]; ok {
				return token, nil
			}
			return "", keychain.ErrNotFound
		},
		Delete: func(instance string) error {
			s.deleted = append(s.deleted, instance)
			delete(s.entries, instance)
			return nil
		},
	}
}

// session runs one invocation with everything supplied: the environment, the
// configuration file, and the keychain.
type session struct {
	t        *testing.T
	server   *httptest.Server
	env      map[string]string
	settings string
	store    *store
}

func newSession(t *testing.T, server *httptest.Server, env map[string]string) *session {
	t.Helper()
	return &session{
		t:        t,
		server:   server,
		env:      env,
		settings: filepath.Join(t.TempDir(), "planaffe", "config"),
		store:    newStore(),
	}
}

func (s *session) run(args ...string) (code int, stdout, stderr string) {
	s.t.Helper()
	var out, errOut bytes.Buffer
	code = Run(context.Background(), args, Env{
		Getenv:   func(k string) string { return s.env[k] },
		Dir:      s.t.TempDir(),
		Stdin:    strings.NewReader(""),
		Stdout:   &out,
		Stderr:   &errOut,
		HTTP:     s.server.Client(),
		Settings: s.settings,
		Store:    s.store.asStore(),
	})
	return code, out.String(), errOut.String()
}

// theInstance answers the device-code flow: pending until pressed, then the
// token, then never again.
type theInstance struct {
	version string
	// pending is how many polls are answered `device-pending` before a human
	// is taken to have pressed the button.
	pending   int
	collected bool
	begun     int
	polls     int
}

func (i *theInstance) handler() http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Planaffe-Version", i.version)
		reply := func(status int, body string) {
			if status >= 400 {
				w.Header().Set("Content-Type", "application/problem+json")
			} else {
				w.Header().Set("Content-Type", "application/json")
			}
			w.WriteHeader(status)
			_, _ = w.Write([]byte(body))
		}

		switch {
		case r.URL.Path == "/version":
			reply(200, `{"version":"0.0.0-dev"}`)
		case r.URL.Path == "/device-logins" && r.Method == http.MethodPost:
			i.begun++
			reply(200, `{"device_code":"a-very-long-device-code","user_code":"BCDF-GHJK","verification_uri":"/device",`+
				`"verification_uri_complete":"/device?code=BCDF-GHJK","expires_in_seconds":600,"interval_seconds":1}`)
		case r.URL.Path == "/device-logins/redeem":
			i.polls++
			switch {
			case i.collected:
				reply(410, `{"type":"/problems/device-expired","status":410,"detail":"A device code works once."}`)
			case i.polls > i.pending:
				i.collected = true
				reply(200, `{"token":{"id":"0198e0c0-0000-7000-8000-000000000009","prefix":"pa_ABCDE","secret":"pa_a-token-of-thirty-two-characters-or-more","created_at":"2026-09-09T10:00:00Z"},`+
					`"user":{"id":"0198e0c0-0000-7000-8000-000000000002","kind":"user","name":"maintainer"},"email":"maintainer@example.test","administrator":true}`)
			default:
				// The one refusal that means keep asking.
				reply(409, `{"type":"/problems/device-pending","status":409,"detail":"Nobody has confirmed this login yet."}`)
			}
		case r.URL.Path == "/me/token" && r.Method == http.MethodDelete:
			w.WriteHeader(204)
		case r.URL.Path == "/me":
			reply(200, `{"id":"0198e0c0-0000-7000-8000-000000000002","kind":"user","name":"maintainer","administrator":true,"email":"maintainer@example.test",`+
				`"owner":null,"token":{"prefix":"pa_ABCDE","created_at":"2026-09-09T10:00:00Z"},"metadata":null,"metadata_reported_at":null}`)
		default:
			reply(404, `{"type":"/problems/not-found","status":404}`)
		}
	})
}

func TestLoginPollsUntilAHumanConfirmsAndKeepsTheTokenInTheKeychain(t *testing.T) {
	instance := &theInstance{version: "0.0.0-dev", pending: 1}
	server := httptest.NewServer(instance.handler())
	defer server.Close()

	s := newSession(t, server, nil)
	code, out, errOut := s.run("login", "--url", server.URL)

	if code != exit.OK {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}
	// The code and the address a person has to open go to stderr; stdout stays
	// the data an agent parses.
	for _, want := range []string{"BCDF-GHJK", server.URL + "/device", "Signed in to", "maintainer"} {
		if !strings.Contains(errOut, want) {
			t.Errorf("stderr lacks %q:\n%s", want, errOut)
		}
	}
	if strings.Contains(out, "pa_a-token") || strings.Contains(errOut, "pa_a-token") {
		t.Fatal("the secret was printed to the terminal it was kept out of")
	}
	if instance.polls < 2 {
		t.Fatalf("expected the poll to have waited at least once, got %d", instance.polls)
	}

	if got := s.store.entries[server.URL]; got != "pa_a-token-of-thirty-two-characters-or-more" {
		t.Fatalf("the keychain holds %q", got)
	}

	// And the next command finds it without being told anything.
	settings, err := config.ReadSettings(s.settings)
	if err != nil {
		t.Fatal(err)
	}
	if settings.Instance != server.URL || settings.TokenFile != "" {
		t.Fatalf("unexpected settings %+v", settings)
	}

	code, out, errOut = s.run("me")
	if code != exit.OK {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}
	if !strings.Contains(out, "from "+config.Keychain) {
		t.Fatalf("pa me does not say where the token came from:\n%s", out)
	}
}

func TestLoginWithoutAKeychainWritesNothingAndNamesTheTwoWaysOn(t *testing.T) {
	instance := &theInstance{version: "0.0.0-dev"}
	server := httptest.NewServer(instance.handler())
	defer server.Close()

	s := newSession(t, server, nil)
	s.store.available = false

	code, _, errOut := s.run("login", "--url", server.URL)

	if code != exit.Usage {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}
	for _, want := range []string{"no keychain", "PLANAFFE_TOKEN", "--token-file"} {
		if !strings.Contains(errOut, want) {
			t.Errorf("the refusal lacks %q:\n%s", want, errOut)
		}
	}
	if len(s.store.entries) != 0 {
		t.Fatal("nothing may be written when there is no store")
	}
}

func TestLoginCanBeToldAFileAndWritesItReadableOnlyByYou(t *testing.T) {
	instance := &theInstance{version: "0.0.0-dev"}
	server := httptest.NewServer(instance.handler())
	defer server.Close()

	s := newSession(t, server, nil)
	path := filepath.Join(t.TempDir(), "token")

	code, _, errOut := s.run("login", "--url", server.URL, "--token-file", path)
	if code != exit.OK {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}

	info, err := os.Stat(path)
	if err != nil {
		t.Fatal(err)
	}
	if mode := info.Mode().Perm(); mode != 0o600 {
		t.Fatalf("the token file is mode %04o", mode)
	}
	if len(s.store.entries) != 0 {
		t.Fatal("a file was chosen; the keychain should hold nothing")
	}

	settings, err := config.ReadSettings(s.settings)
	if err != nil {
		t.Fatal(err)
	}
	if settings.TokenFile != path {
		t.Fatalf("unexpected settings %+v", settings)
	}
}

func TestLoginWithoutAnAddressSaysWhichTwoWaysThereAre(t *testing.T) {
	instance := &theInstance{version: "0.0.0-dev"}
	server := httptest.NewServer(instance.handler())
	defer server.Close()

	code, _, errOut := newSession(t, server, nil).run("login")

	if code != exit.Usage || !strings.Contains(errOut, config.EnvURL) || !strings.Contains(errOut, "pa login --url") {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}
}

func TestLogoutRevokesTheTokenAndForgetsIt(t *testing.T) {
	instance := &theInstance{version: "0.0.0-dev"}
	server := httptest.NewServer(instance.handler())
	defer server.Close()

	s := newSession(t, server, nil)
	if code, _, errOut := s.run("login", "--url", server.URL); code != exit.OK {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}

	code, _, errOut := s.run("logout")
	if code != exit.OK {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}
	if !strings.Contains(errOut, "Signed out of "+server.URL) {
		t.Fatalf("unexpected stderr %q", errOut)
	}
	if len(s.store.entries) != 0 || len(s.store.deleted) != 1 {
		t.Fatalf("the keychain still holds %+v, deleted %v", s.store.entries, s.store.deleted)
	}
}

func TestLogoutRefusesATokenPaDidNotPutThere(t *testing.T) {
	instance := &theInstance{version: "0.0.0-dev"}
	server := httptest.NewServer(instance.handler())
	defer server.Close()

	// The agent's own token, in the agent's own environment. Revoking it from
	// a terminal would revoke something the person may not know they hold.
	s := newSession(t, server, map[string]string{
		config.EnvURL:   server.URL,
		config.EnvToken: "pa_an-agents-token-of-thirty-two-characters",
	})

	code, _, errOut := s.run("logout")
	if code != exit.Usage {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}
	if !strings.Contains(errOut, config.EnvToken) || !strings.Contains(errOut, "pa token revoke") {
		t.Fatalf("unexpected stderr %q", errOut)
	}
	if len(s.store.deleted) != 0 {
		t.Fatal("nothing may be forgotten on a refusal")
	}
}

func TestTheEnvironmentStillWinsOverALoginOnThisMachine(t *testing.T) {
	instance := &theInstance{version: "0.0.0-dev"}
	server := httptest.NewServer(instance.handler())
	defer server.Close()

	s := newSession(t, server, nil)
	if code, _, errOut := s.run("login", "--url", server.URL); code != exit.OK {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}

	// An agent started from this shell must work under its own token, whatever
	// the human on this machine signed in as (ADR 0025).
	s.env = map[string]string{config.EnvToken: "pa_an-agents-token-of-thirty-two-characters"}
	code, out, errOut := s.run("me")
	if code != exit.OK {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}
	if !strings.Contains(out, "from "+config.EnvToken) {
		t.Fatalf("pa me reads the wrong token:\n%s", out)
	}
}
