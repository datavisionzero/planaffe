package cmd

import (
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/datavisionzero/planaffe/src/cli/internal/exit"
)

func TestIdentityVerbsPrintSecretsOnceAndHitTheirEndpoints(t *testing.T) {
	const me = `{"id":"0198e0c0-0000-7000-8000-000000000002","kind":"user","name":"maintainer","administrator":true,"owner":null,"token":{"prefix":"pa_a1b2c","created_at":"2026-09-02T14:00:00.000000Z"}}`
	const issued = `{"id":"0198e0c0-0000-7000-8000-00000000000b","prefix":"pa_secre","secret":"pa_secret-shown-once-and-nowhere-else-at-all-43chars","created_at":"2026-09-02T14:00:00.000000Z"}`
	f := &fake{t: t, version: "0.0.0-dev", answer: func(r *http.Request) (int, string) {
		switch {
		case r.URL.Path == "/me":
			return 200, me
		case r.URL.Path == "/version":
			return 200, `{"version":"0.0.0-dev"}`
		case r.Method == http.MethodPost && r.URL.Path == "/users":
			return 201, `{"id":"0198e0c0-0000-7000-8000-000000000003","kind":"user","name":"other","administrator":false,"created_at":"2026-09-02T14:00:00.000000Z","token":` + issued + `}`
		case r.URL.Path == "/users":
			return 200, `[{"id":"0198e0c0-0000-7000-8000-000000000002","kind":"user","name":"maintainer","administrator":true,"created_at":"2026-09-02T14:00:00.000000Z"}]`
		case r.Method == http.MethodPost && r.URL.Path == "/agents":
			return 201, `{"id":"0198e0c0-0000-7000-8000-000000000001","kind":"agent","name":"quiet-otter-42","owner":{"id":"0198e0c0-0000-7000-8000-000000000002","kind":"user","name":"maintainer"},"created_at":"2026-09-02T14:00:00.000000Z","token":` + issued + `}`
		case r.URL.Path == "/agents":
			return 200, `[{"id":"0198e0c0-0000-7000-8000-000000000001","kind":"agent","name":"quiet-otter-42","owner":{"id":"0198e0c0-0000-7000-8000-000000000002","kind":"user","name":"maintainer"},"created_at":"2026-09-02T14:00:00.000000Z","token":{"id":"0198e0c0-0000-7000-8000-00000000000b","prefix":"pa_secre","created_at":"2026-09-02T14:00:00.000000Z","revoked_at":null}}]`
		case r.Method == http.MethodPatch:
			return 200, `{"id":"0198e0c0-0000-7000-8000-000000000001","kind":"agent","name":"brisk-heron-7","owner":{"id":"0198e0c0-0000-7000-8000-000000000002","kind":"user","name":"maintainer"},"created_at":"2026-09-02T14:00:00.000000Z","token":{"id":"0198e0c0-0000-7000-8000-00000000000b","prefix":"pa_secre","created_at":"2026-09-02T14:00:00.000000Z","revoked_at":null}}`
		case r.Method == http.MethodDelete:
			return 204, ""
		case r.Method == http.MethodPost && r.URL.Path == "/tokens":
			return 201, issued
		case r.URL.Path == "/tokens":
			return 200, `[{"id":"0198e0c0-0000-7000-8000-00000000000b","prefix":"pa_secre","created_at":"2026-09-02T14:00:00.000000Z","revoked_at":null}]`
		}
		return 404, ""
	}}
	server := httptest.NewServer(f.handler())
	defer server.Close()
	dir := t.TempDir()

	cases := []struct {
		args   []string
		method string
		path   string
		body   map[string]any
		stdout string
		stderr string
	}{
		{[]string{"me"}, "GET", "/me", nil, "maintainer (user)  administrator", ""},
		{[]string{"version"}, "GET", "/version", nil, "pa 0.0.0-dev\nplanaffe 0.0.0-dev", ""},
		{[]string{"user", "create", "other"}, "POST", "/users", map[string]any{"name": "other"}, "token: pa_secret-shown-once", "shown once"},
		{[]string{"user", "list"}, "GET", "/users", nil, "maintainer", ""},
		{[]string{"agent", "create", "--name", "quiet-otter-42"}, "POST", "/agents", map[string]any{"name": "quiet-otter-42"}, "token: pa_secret-shown-once", "shown once"},
		{[]string{"agent", "list"}, "GET", "/agents", nil, "quiet-otter-42", ""},
		{[]string{"agent", "rename", "0198e0c0-0000-7000-8000-000000000001", "--name", "brisk-heron-7"}, "PATCH", "/agents/0198e0c0-0000-7000-8000-000000000001", map[string]any{"name": "brisk-heron-7"}, "renamed to brisk-heron-7", ""},
		{[]string{"agent", "revoke", "0198e0c0-0000-7000-8000-000000000001"}, "DELETE", "/agents/0198e0c0-0000-7000-8000-000000000001", nil, "revoked", ""},
		{[]string{"token", "create"}, "POST", "/tokens", nil, "token: pa_secret-shown-once", "shown once"},
		{[]string{"token", "list"}, "GET", "/tokens", nil, "pa_secre…", ""},
		{[]string{"token", "revoke", "0198e0c0-0000-7000-8000-00000000000b"}, "DELETE", "/tokens/0198e0c0-0000-7000-8000-00000000000b", nil, "revoked", ""},
	}
	for _, c := range cases {
		code, out, errOut := run(t, server, dir, c.args...)
		if code != exit.OK {
			t.Fatalf("%v: code %d, stderr %s", c.args, code, errOut)
		}
		last := f.requests[len(f.requests)-1]
		if last.Method != c.method || last.URL.Path != c.path {
			t.Errorf("%v: %s %s, want %s %s", c.args, last.Method, last.URL.Path, c.method, c.path)
		}
		if c.body != nil {
			var body map[string]any
			_ = json.Unmarshal([]byte(f.bodies[len(f.bodies)-1]), &body)
			for k, want := range c.body {
				if body[k] != want {
					t.Errorf("%v: body[%s] = %v, want %v", c.args, k, body[k], want)
				}
			}
		}
		if !strings.Contains(out, c.stdout) {
			t.Errorf("%v: stdout %q lacks %q", c.args, out, c.stdout)
		}
		if c.stderr == "" && errOut != "" || c.stderr != "" && !strings.Contains(errOut, c.stderr) {
			t.Errorf("%v: stderr %q, want %q", c.args, errOut, c.stderr)
		}
		if strings.Contains(errOut, "pa_secret") {
			t.Errorf("%v: the secret is on stdout and nowhere else", c.args)
		}
	}

	code, _, errOut := run(t, server, dir, "agent", "revoke", "not-an-id")
	if code != exit.Usage || !strings.Contains(errOut, "not an agent id") {
		t.Errorf("bad id: code %d, stderr %q", code, errOut)
	}
}

func TestVersionPrintsBothSidesAndSaysWhenTheyDoNotFit(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: func(*http.Request) (int, string) { return 200, `{"version":"0.0.0-dev"}` }}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, out, _ := run(t, server, t.TempDir(), "version", "--json")
	if code != exit.OK || !strings.Contains(out, `"pa": "0.0.0-dev"`) || !strings.Contains(out, `"planaffe": "0.0.0-dev"`) {
		t.Fatalf("code %d, out %s", code, out)
	}
	// The skew itself cannot be provoked from a development build, whose version
	// is never checked; the rule is tested in the version package, and the
	// message route through `pa version` is what this test would need a
	// released build for.
}
