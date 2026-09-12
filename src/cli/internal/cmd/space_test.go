package cmd

import (
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/datavisionzero/planaffe/src/cli/internal/exit"
)

const hits = `[{"path":"company/onboarding","space":"handbuch","space_title":"Handbuch","title":"Onboarding",
"trail":[{"path":"company","title":"Die Firma"}],
"excerpt":[{"text":"Am ","hit":false},{"text":"ersten Tag","hit":true},{"text":" bekommt jede neue\nPerson eine Karte.","hit":false}]}]`

func TestSpaceSearchAsksTheKnowledgeBaseAndPrintsWhereAHitStands(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: func(_ *http.Request) (int, string) { return 200, hits }}
	server := httptest.NewServer(f.handler())
	defer server.Close()
	dir := repository(t, "project = PLAN\n")

	code, out, stderr := run(t, server, dir, "space", "search", "onboarding pate")

	if code != exit.OK || stderr != "" {
		t.Fatalf("code %d, stderr %q", code, stderr)
	}
	asked := f.requests[len(f.requests)-1]
	if asked.Method != http.MethodGet || asked.URL.Path != "/pages" {
		t.Fatalf("%s %s", asked.Method, asked.URL.Path)
	}
	if got := asked.URL.Query().Get("q"); got != "onboarding pate" {
		t.Errorf("q %q", got)
	}
	if asked.URL.Query().Has("space") || asked.URL.Query().Has("limit") {
		t.Errorf("asked %q, and neither was given", asked.URL.RawQuery)
	}

	// The space, the address and the title, and the excerpt under it — on one
	// line, because a hit is a row and the body it was cut out of is not.
	if !strings.Contains(out, "handbuch") || !strings.Contains(out, "company/onboarding") || !strings.Contains(out, "Onboarding") {
		t.Errorf("stdout %q says nothing about where the hit stands", out)
	}
	if !strings.Contains(out, "Am ersten Tag bekommt jede neue Person eine Karte.") {
		t.Errorf("stdout %q is not the excerpt in one line", out)
	}
	// Nothing but text: the output of a test is not a terminal, and an escape
	// in a pipe is noise in somebody's parser.
	if strings.Contains(out, "\x1b") {
		t.Errorf("stdout %q carries an escape", out)
	}
}

func TestSpaceSearchCarriesTheSpaceAndTheLimit(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: func(_ *http.Request) (int, string) { return 200, hits }}
	server := httptest.NewServer(f.handler())
	defer server.Close()
	dir := repository(t, "project = PLAN\n")

	code, _, stderr := run(t, server, dir, "space", "search", "vertrag", "--space", "personal", "--limit", "5")

	if code != exit.OK || stderr != "" {
		t.Fatalf("code %d, stderr %q", code, stderr)
	}
	query := f.requests[len(f.requests)-1].URL.Query()
	if query.Get("space") != "personal" || query.Get("limit") != "5" {
		t.Errorf("query %q", query.Encode())
	}
}

// --json is the object as the instance answered it, pieces and all: that is
// what an agent reads, and it loses nothing the terminal shows.
func TestSpaceSearchJSONIsTheAnswer(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: func(_ *http.Request) (int, string) { return 200, hits }}
	server := httptest.NewServer(f.handler())
	defer server.Close()
	dir := repository(t, "project = PLAN\n")

	code, out, _ := run(t, server, dir, "space", "search", "onboarding", "--json")

	if code != exit.OK {
		t.Fatalf("code %d", code)
	}
	for _, want := range []string{`"space_title": "Handbuch"`, `"hit": true`, `"trail"`} {
		if !strings.Contains(out, want) {
			t.Errorf("stdout %q lacks %s", out, want)
		}
	}
}

func TestSpaceSearchWithoutWordsIsAUsageMistake(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: func(_ *http.Request) (int, string) { return 200, hits }}
	server := httptest.NewServer(f.handler())
	defer server.Close()
	dir := repository(t, "project = PLAN\n")

	code, out, stderr := run(t, server, dir, "space", "search", "  ")

	if code != exit.Usage {
		t.Fatalf("code %d, stderr %q", code, stderr)
	}
	if out != "" || !strings.Contains(stderr, "pa space search QUERY") {
		t.Errorf("stdout %q, stderr %q", out, stderr)
	}
	// And the instance was never troubled with it.
	if len(f.requests) != 0 {
		t.Errorf("%d requests", len(f.requests))
	}
}

// A space this caller has not got answers what a space that does not exist
// answers, and pa adds nothing to it (ADR 0027).
func TestSpaceSearchInASpaceThatIsNotThere(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: func(_ *http.Request) (int, string) {
		return 404, `{"type":"about:blank","title":"Nothing by that key or id","status":404,"detail":"No space fremd.","code":"not-found"}`
	}}
	server := httptest.NewServer(f.handler())
	defer server.Close()
	dir := repository(t, "project = PLAN\n")

	code, out, stderr := run(t, server, dir, "space", "search", "vertrag", "--space", "fremd")

	if code != exit.NotFound {
		t.Fatalf("code %d", code)
	}
	if out != "" || !strings.Contains(stderr, "No space fremd.") {
		t.Errorf("stdout %q, stderr %q", out, stderr)
	}
}

func TestSpaceSearchThatFoundNothingSaysSoAndIsNoError(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: func(_ *http.Request) (int, string) { return 200, `[]` }}
	server := httptest.NewServer(f.handler())
	defer server.Close()
	dir := repository(t, "project = PLAN\n")

	code, out, stderr := run(t, server, dir, "space", "search", "nichts")

	if code != exit.OK || stderr != "" {
		t.Fatalf("code %d, stderr %q", code, stderr)
	}
	if !strings.Contains(out, "Nothing matched") {
		t.Errorf("stdout %q says nothing", out)
	}
}
