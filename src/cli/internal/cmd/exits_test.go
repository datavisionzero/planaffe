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

// panicking is a transport that fails the way a bug in pa would.
type panicking struct{}

func (panicking) RoundTrip(*http.Request) (*http.Response, error) { panic("a nil pointer somewhere") }

func TestAPanicIsExitOneAndNotTheUsageCode(t *testing.T) {
	var out, errOut bytes.Buffer
	code := Run(context.Background(), []string{"issue", "view", "PLAN-42"}, Env{
		Getenv: func(k string) string {
			return map[string]string{config.EnvURL: "http://planaffe.example", config.EnvToken: "pa_test-token-of-thirty-two-characters-or-more"}[k]
		},
		Dir:      t.TempDir(),
		Stdin:    strings.NewReader(""),
		Stdout:   &out,
		Stderr:   &errOut,
		HTTP:     &http.Client{Transport: panicking{}},
		Settings: filepath.Join(t.TempDir(), "config"),
		Store:    &keychain.Store{Get: func(string) (string, error) { return "", keychain.ErrNotFound }},
	})

	if code != exit.Unexpected {
		t.Fatalf("code %d, stderr %q", code, errOut.String())
	}
	if !strings.Contains(errOut.String(), "bug in pa") || !strings.Contains(errOut.String(), "a nil pointer somewhere") {
		t.Fatalf("unexpected stderr %q", errOut.String())
	}
}

func TestAnEmptySuccessWhereJSONIsPromisedIsExitOne(t *testing.T) {
	// A proxy in front of the instance that answers 200 with nothing, and says
	// nothing about what it is: the generated client leaves the JSON unset,
	// and every verb used to dereference it.
	f := &fake{t: t, version: "0.0.0-dev", contentType: "text/plain", answer: func(*http.Request) (int, string) { return 200, "" }}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, _, errOut := run(t, server, repository(t, "project = PLAN\n"), "issue", "view", "PLAN-42")
	if code != exit.Unexpected || !strings.Contains(errOut, "no body") || strings.Contains(errOut, "bug in pa") {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}

	// Claiming to be JSON with nothing in it fails in the generated client
	// already; it is exit 1 as well.
	f.contentType = ""
	code, _, errOut = run(t, server, repository(t, "project = PLAN\n"), "issue", "view", "PLAN-42")
	if code != exit.Unexpected || strings.Contains(errOut, "bug in pa") {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}
}

func TestAnEmptyNoContentIsStillASuccess(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: func(*http.Request) (int, string) { return http.StatusNoContent, "" }}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, _, errOut := run(t, server, repository(t, "project = PLAN\n"), "epic", "delete", "PLAN-E2")
	if code != exit.OK {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}
}

func TestCtrlCIsInterruptedAndNotUnreachable(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: func(*http.Request) (int, string) { return 200, issue }}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	// What signal.NotifyContext hands Run once somebody pressed Ctrl-C.
	ctx, cancel := context.WithCancel(context.Background())
	cancel()

	var out, errOut bytes.Buffer
	code := Run(ctx, []string{"issue", "view", "PLAN-42"}, Env{
		Getenv: func(k string) string {
			return map[string]string{config.EnvURL: server.URL, config.EnvToken: "pa_test-token-of-thirty-two-characters-or-more"}[k]
		},
		Dir:      t.TempDir(),
		Stdin:    strings.NewReader(""),
		Stdout:   &out,
		Stderr:   &errOut,
		HTTP:     server.Client(),
		Settings: filepath.Join(t.TempDir(), "config"),
		Store:    &keychain.Store{Get: func(string) (string, error) { return "", keychain.ErrNotFound }},
	})

	if code != exit.Interrupted || strings.Contains(errOut.String(), "could not be reached") {
		t.Fatalf("code %d, stderr %q", code, errOut.String())
	}
}

func TestALoginCodeThatRunsOutOnPasOwnClockIsRefused(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Planaffe-Version", "0.0.0-dev")
		switch r.URL.Path {
		case "/version":
			w.Header().Set("Content-Type", "application/json")
			_, _ = w.Write([]byte(`{"version":"0.0.0-dev"}`))
		case "/device-logins":
			w.Header().Set("Content-Type", "application/json")
			_, _ = w.Write([]byte(`{"device_code":"a-very-long-device-code","user_code":"BCDF-GHJK","verification_uri":"/device",` +
				`"verification_uri_complete":"/device?code=BCDF-GHJK","expires_in_seconds":0,"interval_seconds":1}`))
		default:
			w.Header().Set("Content-Type", "application/problem+json")
			w.WriteHeader(http.StatusConflict)
			_, _ = w.Write([]byte(`{"type":"/problems/device-pending","status":409}`))
		}
	}))
	defer server.Close()

	code, _, errOut := newSession(t, server, nil).run("login", "--url", server.URL)
	// The same code as the instance's own `device-expired`, a 410.
	if code != exit.Refused || !strings.Contains(errOut, "in time") {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}
}

func TestNeedsYouWaitKeepsWaitingWhenOnlyTheTagOfAnEmptyPageMoves(t *testing.T) {
	const empty = `{"items":[],"total":0,"has_more":false,"next_cursor":null,"agents":%d}`
	f := &fake{t: t, version: "0.0.0-dev"}
	f.answer = func(r *http.Request) (int, string) {
		switch r.Header.Get("If-None-Match") {
		case "":
			return 200, strings.Replace(empty, "%d", "1", 1)
		case `"one-agent"`:
			// An agent token was created meanwhile: a new tag, the list as
			// empty as it was.
			return 200, strings.Replace(empty, "%d", "2", 1)
		default:
			return 200, `{"items":[{"because":"question","issue":{"key":"PLAN-1","project":"PLAN","title":"Answer me","status":"todo","ready":true,"priority":4,"labels":[],"epic":null,"parent":null,"assignee":null,"claim":null,"blocked_by":[],"open_questions":1,"open_blockers":0,"open_sub_issues":0,"created_at":"2026-09-02T14:00:00Z","updated_at":"2026-09-02T14:00:00Z","closed_at":null,"deleted_at":null,"deleted_by":null}}],"total":1,"has_more":false,"next_cursor":null,"agents":2}`
		}
	}
	f.headers = func(r *http.Request) map[string]string {
		if r.Header.Get("If-None-Match") == "" {
			return map[string]string{"ETag": `"one-agent"`}
		}
		return map[string]string{"ETag": `"two-agents"`}
	}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, out, errOut := run(t, server, repository(t, "project = PLAN\n"), "needs-you", "--wait", "60")
	if code != exit.OK || strings.Contains(out, "Nothing needs you.") || !strings.Contains(out, "PLAN-1") {
		t.Fatalf("code %d, stdout %q, stderr %q", code, out, errOut)
	}
	if len(f.requests) != 3 || f.requests[2].Header.Get("If-None-Match") != `"two-agents"` {
		t.Fatalf("requests = %d; the third should carry the new tag", len(f.requests))
	}
	if wait := f.requests[2].URL.Query().Get("wait"); wait == "" || wait == "60" {
		t.Fatalf("the third round waits %q; what the second used up is gone", wait)
	}
}

func TestInitRefusesAnExistingProjectFileBeforeCreatingAnything(t *testing.T) {
	f, server := emptyInstance(t)
	defer server.Close()
	dir := repository(t, "project = OLD\n")

	code, _, errOut := run(t, server, dir, "init", "NEWKEY")
	if code != exit.Usage || !strings.Contains(errOut, "--force") {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}
	for _, r := range f.requests {
		if r.Method == http.MethodPost {
			t.Fatalf("a refused init created %s", r.URL.Path)
		}
	}
	if written, _ := os.ReadFile(filepath.Join(dir, ".planaffe")); string(written) != "project = OLD\n" {
		t.Fatalf(".planaffe = %q", written)
	}
}

func TestInitNamesTheTokenWhereItCameFrom(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Planaffe-Version", "0.0.0-dev")
		if r.URL.Path == "/version" {
			w.Header().Set("Content-Type", "application/json")
			_, _ = w.Write([]byte(`{"version":"0.0.0-dev"}`))
			return
		}
		w.Header().Set("Content-Type", "application/problem+json")
		w.WriteHeader(http.StatusUnauthorized)
		_, _ = w.Write([]byte(`{"type":"/problems/unauthenticated","status":401}`))
	}))
	defer server.Close()

	// Signed in on this machine: the address from the settings, the token
	// from the keychain, and no variable anywhere.
	s := newSession(t, server, nil)
	if err := config.WriteSettings(s.settings, config.Settings{Instance: server.URL}); err != nil {
		t.Fatal(err)
	}
	s.store.entries[server.URL] = "pa_a-revoked-token-of-thirty-two-characters"

	code, _, errOut := s.run("init", "PLAN")
	if code != exit.Denied || !strings.Contains(errOut, config.Keychain) || strings.Contains(errOut, config.EnvToken) {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}
}

func TestInitNamesTheAddressWhereItCameFrom(t *testing.T) {
	server := httptest.NewServer(http.NotFoundHandler())
	address := server.URL
	server.Close() // nothing answers there any more

	s := newSession(t, server, nil)
	if err := config.WriteSettings(s.settings, config.Settings{Instance: address}); err != nil {
		t.Fatal(err)
	}
	s.store.entries[address] = "pa_a-token-of-thirty-two-characters-or-more"

	code, _, errOut := s.run("init", "PLAN")
	if code != exit.Unreachable || !strings.Contains(errOut, config.LoggedIn) || strings.Contains(errOut, config.EnvURL) {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}
}

func openSummary(key string) string {
	return `{"key":"` + key + `","project":"PLAN","title":"Still open","status":"todo","ready":false,"priority":0,"labels":[],"epic":"PLAN-E2","assignee":null,"claim":null,"blocked_by":[],"open_questions":0,"open_blockers":0,"open_sub_issues":0,"created_at":"2026-09-02T14:00:00.000000Z","updated_at":"2026-09-02T14:00:00.000000Z","closed_at":null,"deleted_at":null,"deleted_by":null}`
}

// An epic with more open issues than a page holds, one of which is claimed by
// somebody else.
func epicWithTwoPages(t *testing.T) *fake {
	return &fake{t: t, version: "0.0.0-dev", answer: func(r *http.Request) (int, string) {
		switch {
		case r.Method == http.MethodGet && r.URL.Path == "/issues":
			if r.URL.Query().Get("cursor") == "" {
				return 200, `{"items":[` + openSummary("PLAN-7") + `,` + openSummary("PLAN-8") + `],"total":3,"has_more":true,"next_cursor":"page-2"}`
			}
			return 200, `{"items":[` + openSummary("PLAN-9") + `],"total":3,"has_more":false,"next_cursor":null}`
		case r.URL.Path == "/epics/PLAN-E2/close":
			return 200, epic
		case strings.HasPrefix(r.URL.Path, "/issues/PLAN-8"):
			return 409, `{"type":"/problems/claim-held","status":409,"detail":"PLAN-8 is claimed by quiet-otter-42."}`
		case strings.HasPrefix(r.URL.Path, "/issues/"):
			return 200, issue
		default:
			return 404, `{"type":"/problems/not-found","status":404}`
		}
	}}
}

func TestEpicCloseFollowsEveryPageAndGoesOnPastARefusal(t *testing.T) {
	for _, flag := range []string{"--cancel-open", "--park-open"} {
		f := epicWithTwoPages(t)
		server := httptest.NewServer(f.handler())

		code, out, errOut := run(t, server, repository(t, "project = PLAN\n"), "epic", "close", "PLAN-E2", flag)
		server.Close()

		if code != exit.Conflict {
			t.Errorf("%s: code %d, stderr %q", flag, code, errOut)
		}
		if !strings.Contains(out, "PLAN-E2") {
			t.Errorf("%s: the closed epic is still printed; stdout %q", flag, out)
		}
		if !strings.Contains(errOut, "PLAN-8 not") || !strings.Contains(errOut, "1 of 3") {
			t.Errorf("%s: stderr %q", flag, errOut)
		}

		touched := map[string]bool{}
		for _, r := range f.requests {
			if r.Method != http.MethodGet && strings.HasPrefix(r.URL.Path, "/issues/") {
				touched[strings.Split(strings.TrimPrefix(r.URL.Path, "/issues/"), "/")[0]] = true
			}
		}
		for _, key := range []string{"PLAN-7", "PLAN-8", "PLAN-9"} {
			if !touched[key] {
				t.Errorf("%s: %s was never tried; touched %v", flag, key, touched)
			}
		}
	}
}
