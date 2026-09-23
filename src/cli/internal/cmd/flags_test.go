package cmd

import (
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/datavisionzero/planaffe/src/cli/internal/config"
	"github.com/datavisionzero/planaffe/src/cli/internal/exit"
)

// Two flags that would each have had their way without a word are refused
// before a request leaves.
func TestContradictoryFlagsAreRefusedBeforeAnyRequest(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: func(*http.Request) (int, string) { return 200, issue }}
	server := httptest.NewServer(f.handler())
	defer server.Close()
	dir := repository(t, "project = PLAN\n")

	cases := []struct {
		args []string
		want string
	}{
		// The second `-` read what the first left, nothing, and cleared the result.
		{[]string{"issue", "edit", "PLAN-42", "--description-file", "-", "--result-file", "-"}, "stdin can be read once"},
		{[]string{"issue", "reopen", "PLAN-42", "--comment", "Not enough.", "--comment-file", "-"}, "--comment and --comment-file"},
		{[]string{"question", "list", "--answered", "--all"}, "--answered and --all"},
		// Granting the administrator role is no longer what saying nothing means.
		{[]string{"user", "administrator", "bob"}, "--enabled=true"},
	}
	for _, c := range cases {
		code, _, errOut := runWith(t, server, dir, "Some text.\n", c.args...)
		if code != exit.Usage || !strings.Contains(errOut, c.want) {
			t.Errorf("%v: code %d, stderr %q", c.args, code, errOut)
		}
	}
	for _, r := range f.requests {
		if r.Method != http.MethodGet {
			t.Errorf("a refused command sent %s %s", r.Method, r.URL.Path)
		}
	}
}

func TestOneFlagMayStillReadStdin(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: func(*http.Request) (int, string) { return 200, issue }}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, _, errOut := runWith(t, server, repository(t, "project = PLAN\n"), "The result.\n",
		"issue", "edit", "PLAN-42", "--description-file", "-")
	if code != exit.OK {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}
	if body := f.bodies[len(f.bodies)-1]; !strings.Contains(body, `"description":"The result."`) {
		t.Fatalf("body %s", body)
	}
}

func TestUserAdministratorSendsWhatItWasTold(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: func(*http.Request) (int, string) {
		return 200, `{"id":"0198e0c0-0000-7000-8000-000000000003","kind":"user","name":"bob","email":"bob@example.test","state":"active","administrator":false,"created_at":"2026-09-02T14:00:00.000000Z"}`
	}}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, out, errOut := run(t, server, t.TempDir(), "user", "administrator", "bob", "--enabled=false")
	if code != exit.OK || !strings.Contains(out, "administrator=false") {
		t.Fatalf("code %d, stdout %q, stderr %q", code, out, errOut)
	}
	if body := f.bodies[len(f.bodies)-1]; !strings.Contains(body, `"administrator":false`) {
		t.Fatalf("body %s", body)
	}
}

// `/version` is anonymous, and `pa version` is what other messages send
// somebody to: it must work before there is a token.
func TestVersionNeedsAnAddressAndNoToken(t *testing.T) {
	var authorization []string
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		authorization = append(authorization, r.Header.Get("Authorization"))
		w.Header().Set("Planaffe-Version", "0.0.0-dev")
		w.Header().Set("Content-Type", "application/json")
		_, _ = w.Write([]byte(`{"version":"0.0.0-dev"}`))
	}))
	defer server.Close()

	code, out, errOut := newSession(t, server, map[string]string{config.EnvURL: server.URL}).run("version")
	if code != exit.OK || !strings.Contains(out, "planaffe 0.0.0-dev") {
		t.Fatalf("code %d, stdout %q, stderr %q", code, out, errOut)
	}
	if len(authorization) != 1 || authorization[0] != "" {
		t.Fatalf("authorization %q", authorization)
	}

	// Without an address there is nothing to ask, and it says how to give one.
	code, _, errOut = newSession(t, server, nil).run("version")
	if code != exit.Usage || !strings.Contains(errOut, config.EnvURL) {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}
}
