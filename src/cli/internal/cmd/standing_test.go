package cmd

import (
	"fmt"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"time"

	"github.com/datavisionzero/planaffe/src/cli/internal/exit"
)

func standingAnswer(agents int, projects ...string) string {
	return fmt.Sprintf(`{"agents":%d,"projects":[%s]}`, agents, strings.Join(projects, ","))
}

func aStanding(key, standing, because, needsYou, work string) string {
	return fmt.Sprintf(`{"key":%q,"name":%q,"standing":%q,"because":%q,"needs_you":%s,"work":%s}`,
		key, strings.ToLower(key), standing, because, needsYou, work)
}

func TestStandingPrintsOneLinePerProjectInTheOrderTheInstanceGave(t *testing.T) {
	oldest := time.Now().Add(-6 * 24 * time.Hour).UTC().Format(time.RFC3339)
	f := &fake{t: t, version: "0.0.0-dev", answer: func(r *http.Request) (int, string) {
		if r.Method != http.MethodGet || r.URL.Path != "/standing" {
			return 404, `{"type":"/problems/not-found","status":404}`
		}
		return 200, standingAnswer(1,
			aStanding("PLAN", "neglected", "question",
				fmt.Sprintf(`{"question":2,"review":0,"unready":0,"stuck":0,"oldest":%q}`, oldest),
				`{"in_progress":3,"ready":12,"open":41}`),
			aStanding("HOST", "idle", "nothing_ready",
				`{"question":0,"review":0,"unready":0,"stuck":0,"oldest":null}`,
				`{"in_progress":0,"ready":0,"open":7}`),
			aStanding("LOG", "clear", "nothing",
				`{"question":0,"review":0,"unready":0,"stuck":0,"oldest":null}`,
				`{"in_progress":0,"ready":0,"open":0}`),
		)
	}}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	// No `.planaffe` file and no key: this is the one command that looks across
	// projects, so it asks for none.
	code, out, errOut := run(t, server, t.TempDir(), "standing")
	if code != exit.OK {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}

	lines := strings.Split(strings.TrimSpace(out), "\n")
	if len(lines) != 3 {
		t.Fatalf("one line per project, got:\n%s", out)
	}
	if !strings.HasPrefix(lines[0], "PLAN") || !strings.HasPrefix(lines[1], "HOST") || !strings.HasPrefix(lines[2], "LOG") {
		t.Fatalf("the instance's order is printed as it came:\n%s", out)
	}
	// The word carries the step, never a symbol: this runs into pipes.
	if !strings.Contains(lines[0], "neglected") || !strings.Contains(lines[0], "2 questions, oldest 6 days") {
		t.Errorf("the step and what produced it:\n%s", lines[0])
	}
	if !strings.Contains(lines[0], "3 in progress · 12 ready · 41 open") {
		t.Errorf("the three counts:\n%s", lines[0])
	}
	if !strings.Contains(lines[1], "nothing an agent could take") {
		t.Errorf("idle says which case it is:\n%s", lines[1])
	}
	// Nothing open is said, not printed as three zeros.
	if strings.Contains(lines[2], "0 open") {
		t.Errorf("clear prints no counts:\n%s", lines[2])
	}
}

func TestStandingSaysOnceThatTheInstanceHasNoAgent(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: func(*http.Request) (int, string) {
		return 200, standingAnswer(0,
			aStanding("PLAN", "idle", "no_agent",
				`{"question":0,"review":0,"unready":0,"stuck":0,"oldest":null}`,
				`{"in_progress":0,"ready":4,"open":4}`))
	}}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, out, errOut := run(t, server, t.TempDir(), "standing")
	if code != exit.OK {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}
	if strings.Count(out, "no active agent token") != 1 {
		t.Errorf("said once for the whole answer, not per project:\n%s", out)
	}
	if !strings.Contains(out, "no agent to pick anything up") {
		t.Errorf("and named as the reason for the step:\n%s", out)
	}
}

func TestStandingExplainsAnInstanceWithoutProjects(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: func(*http.Request) (int, string) {
		return 200, standingAnswer(1)
	}}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, out, errOut := run(t, server, t.TempDir(), "standing")
	if code != exit.OK {
		t.Fatalf("code %d, stderr %q", code, errOut)
	}
	if !strings.Contains(out, "No project yet.") {
		t.Errorf("an empty result explains itself; stdout %q", out)
	}
}
