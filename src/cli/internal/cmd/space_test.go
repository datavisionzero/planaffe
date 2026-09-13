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

const spaces = `[{"name":"handbuch","title":"Handbuch","closed_to_agents":false,
"author":{"id":"0198e0c0-0000-7000-8000-000000000002","kind":"user","name":"maintainer"},
"created_at":"2026-09-12T09:00:00.000000Z","updated_at":"2026-09-12T09:00:00.000000Z","deleted_at":null},
{"name":"personal","title":"Personalien","closed_to_agents":true,
"author":{"id":"0198e0c0-0000-7000-8000-000000000002","kind":"user","name":"maintainer"},
"created_at":"2026-09-12T09:00:00.000000Z","updated_at":"2026-09-12T09:00:00.000000Z","deleted_at":null}]`

const tree = `[{"path":"company","space":"handbuch","slug":"company","parent":null,"depth":0,"title":"Die Firma",
"updated_by":{"id":"0198e0c0-0000-7000-8000-000000000002","kind":"user","name":"maintainer"},
"created_at":"2026-09-12T09:00:00.000000Z","updated_at":"2026-09-12T09:00:00.000000Z"},
{"path":"company/onboarding","space":"handbuch","slug":"onboarding","parent":"company","depth":1,"title":"Onboarding",
"updated_by":{"id":"0198e0c0-0000-7000-8000-000000000002","kind":"user","name":"maintainer"},
"created_at":"2026-09-12T09:00:00.000000Z","updated_at":"2026-09-13T11:00:00.000000Z"}]`

// answering serves each path its own body, so that a command asking two routes
// is tested on what it did with both.
func answering(bodies map[string]string) func(*http.Request) (int, string) {
	return func(r *http.Request) (int, string) {
		body, ok := bodies[r.URL.Path]
		if !ok {
			return 404, `{"status":404,"detail":"No such route here.","code":"not-found"}`
		}
		return 200, body
	}
}

func TestSpaceListIsALinePerSpaceAndMarksTheClosedOne(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: answering(map[string]string{"/spaces": spaces})}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	// No `.planaffe` anywhere: the knowledge base has no project, so nothing
	// under `pa space` may ask for one.
	code, out, stderr := run(t, server, t.TempDir(), "space", "list")

	if code != exit.OK || stderr != "" {
		t.Fatalf("code %d, stderr %q", code, stderr)
	}
	if f.requests[0].URL.Path != "/spaces" || f.requests[0].Method != http.MethodGet {
		t.Fatalf("%s %s", f.requests[0].Method, f.requests[0].URL.Path)
	}
	if !strings.Contains(out, "handbuch") || !strings.Contains(out, "Handbuch") {
		t.Errorf("stdout %q", out)
	}
	if !strings.Contains(out, "closed to agents") {
		t.Errorf("stdout %q says nothing about the switch", out)
	}
	// And it says it about the one space it is set on.
	if strings.Count(out, "closed to agents") != 1 {
		t.Errorf("stdout %q marks more than the closed space", out)
	}
}

func TestSpaceListJSONIsTheAnswer(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: answering(map[string]string{"/spaces": spaces})}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, out, _ := run(t, server, t.TempDir(), "space", "list", "--json")

	if code != exit.OK {
		t.Fatalf("code %d", code)
	}
	for _, want := range []string{`"closed_to_agents": true`, `"name": "handbuch"`} {
		if !strings.Contains(out, want) {
			t.Errorf("stdout %q lacks %s", out, want)
		}
	}
}

// The whole space: the head from its own route, the tree from the pages'.
func TestSpaceViewPrintsTheHeadAndTheTree(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: answering(map[string]string{
		"/spaces/handbuch":       `{"name":"handbuch","title":"Handbuch","closed_to_agents":false,"author":{"id":"0198e0c0-0000-7000-8000-000000000002","kind":"user","name":"maintainer"},"created_at":"2026-09-12T09:00:00.000000Z","updated_at":"2026-09-12T09:00:00.000000Z","deleted_at":null}`,
		"/spaces/handbuch/pages": tree,
	})}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, out, stderr := run(t, server, t.TempDir(), "space", "view", "handbuch")

	if code != exit.OK || stderr != "" {
		t.Fatalf("code %d, stderr %q", code, stderr)
	}
	if len(f.requests) != 2 || f.requests[1].URL.Path != "/spaces/handbuch/pages" {
		t.Fatalf("asked %d routes, the last %q", len(f.requests), f.requests[len(f.requests)-1].URL.Path)
	}
	if !strings.Contains(out, "handbuch  Handbuch") || !strings.Contains(out, "author: maintainer") {
		t.Errorf("stdout %q is not the head", out)
	}
	// The address whole on every row, and the child indented under its parent.
	if !strings.Contains(out, "\ncompany  ") || !strings.Contains(out, "\n  company/onboarding ") {
		t.Errorf("stdout %q is not the tree", out)
	}
	if !strings.Contains(out, "2026-09-13") || !strings.Contains(out, "Onboarding") {
		t.Errorf("stdout %q lacks when the page moved or what it is called", out)
	}
}

// --json is the space as the route answered it, and the tree is not fetched:
// two objects under one flag would be a shape of pa's own making.
func TestSpaceViewJSONIsTheSpaceAndAsksNothingElse(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: answering(map[string]string{
		"/spaces/handbuch": `{"name":"handbuch","title":"Handbuch","closed_to_agents":false,"author":{"id":"0198e0c0-0000-7000-8000-000000000002","kind":"user","name":"maintainer"},"created_at":"2026-09-12T09:00:00.000000Z","updated_at":"2026-09-12T09:00:00.000000Z","deleted_at":null}`,
	})}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, out, stderr := run(t, server, t.TempDir(), "space", "view", "handbuch", "--json")

	if code != exit.OK || stderr != "" {
		t.Fatalf("code %d, stderr %q", code, stderr)
	}
	if len(f.requests) != 1 {
		t.Fatalf("asked %d routes", len(f.requests))
	}
	if !strings.Contains(out, `"name": "handbuch"`) {
		t.Errorf("stdout %q", out)
	}
}

func TestSpaceViewOfASpaceWithoutPagesSaysSo(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: answering(map[string]string{
		"/spaces/handbuch":       `{"name":"handbuch","title":"Handbuch","closed_to_agents":false,"author":{"id":"0198e0c0-0000-7000-8000-000000000002","kind":"user","name":"maintainer"},"created_at":"2026-09-12T09:00:00.000000Z","updated_at":"2026-09-12T09:00:00.000000Z","deleted_at":null}`,
		"/spaces/handbuch/pages": `[]`,
	})}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, out, stderr := run(t, server, t.TempDir(), "space", "view", "handbuch")

	if code != exit.OK || stderr != "" {
		t.Fatalf("code %d, stderr %q", code, stderr)
	}
	if !strings.Contains(out, "No pages in handbuch yet.") {
		t.Errorf("stdout %q says nothing", out)
	}
}

// A space this caller has not got — closed to it, or never granted — answers
// what a space that does not exist answers, and pa adds nothing (ADR 0027).
func TestSpaceViewOfASpaceThatIsNotThere(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: func(_ *http.Request) (int, string) {
		return 404, `{"type":"about:blank","title":"Nothing by that key or id","status":404,"detail":"No space personal.","code":"not-found"}`
	}}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, out, stderr := run(t, server, t.TempDir(), "space", "view", "personal")

	if code != exit.NotFound {
		t.Fatalf("code %d", code)
	}
	if out != "" || !strings.Contains(stderr, "No space personal.") {
		t.Errorf("stdout %q, stderr %q", out, stderr)
	}
	// The tree was never asked for: the space is the answer.
	if len(f.requests) != 1 {
		t.Errorf("asked %d routes", len(f.requests))
	}
}

func TestSpaceListWithoutASingleSpaceSaysSo(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: answering(map[string]string{"/spaces": `[]`})}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, out, stderr := run(t, server, t.TempDir(), "space", "list")

	if code != exit.OK || stderr != "" {
		t.Fatalf("code %d, stderr %q", code, stderr)
	}
	if !strings.Contains(out, "No spaces here.") {
		t.Errorf("stdout %q", out)
	}
}
