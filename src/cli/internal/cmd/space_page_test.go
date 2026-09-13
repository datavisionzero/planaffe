package cmd

import (
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/datavisionzero/planaffe/src/cli/internal/exit"
)

const spacePage = `{"path":"company/onboarding","space":"handbuch","slug":"onboarding","parent":"company","depth":1,
"title":"Onboarding","body":"Am ersten Tag bekommt jede neue Person eine Karte.",
"author":{"id":"0198e0c0-0000-7000-8000-000000000002","kind":"user","name":"maintainer"},
"updated_by":{"id":"0198e0c0-0000-7000-8000-000000000001","kind":"agent","name":"quiet-otter-42"},
"created_at":"2026-09-12T09:00:00.000000Z","updated_at":"2026-09-13T11:00:00.000000Z"}`

// serving answers everything with one body, so that a test about what was
// asked is only about that.
func serving(body string) func(*http.Request) (int, string) {
	return func(_ *http.Request) (int, string) { return 200, body }
}

// The slashes in an address are the address, and they reach the instance as
// slashes: a client that percent-encodes them names nothing (docs/api.md,
// ADR 0028). The request target is where that is visible — the server decodes
// the path either way.
func TestSpacePageViewAsksTheAddressUnencoded(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: serving(spacePage)}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, out, stderr := run(t, server, t.TempDir(), "space", "page", "view", "handbuch/company/onboarding")

	if code != exit.OK || stderr != "" {
		t.Fatalf("code %d, stderr %q", code, stderr)
	}
	asked := f.requests[0]
	if asked.Method != http.MethodGet || asked.RequestURI != "/spaces/handbuch/pages/company/onboarding" {
		t.Fatalf("%s %s", asked.Method, asked.RequestURI)
	}
	// The head opens with the address, and then the body stands as it is
	// stored, so that the output pipes back into --body-file -.
	if !strings.Contains(out, "handbuch/company/onboarding  Onboarding") {
		t.Errorf("stdout %q is not the head", out)
	}
	if !strings.Contains(out, "\nAm ersten Tag bekommt jede neue Person eine Karte.\n") {
		t.Errorf("stdout %q is not the body", out)
	}
}

func TestSpacePageViewJSONIsTheAnswer(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: serving(spacePage)}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, out, _ := run(t, server, t.TempDir(), "space", "page", "view", "handbuch/company/onboarding", "--json")

	if code != exit.OK {
		t.Fatalf("code %d", code)
	}
	for _, want := range []string{`"path": "company/onboarding"`, `"parent": "company"`, `"depth": 1`} {
		if !strings.Contains(out, want) {
			t.Errorf("stdout %q lacks %s", out, want)
		}
	}
}

// A word without a slash names a space and not a page. The command line
// already said so, so the instance is never troubled with it.
func TestSpacePageAddressWithoutASlashIsAUsageMistake(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: serving(spacePage)}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, out, stderr := run(t, server, t.TempDir(), "space", "page", "view", "handbuch")

	if code != exit.Usage {
		t.Fatalf("code %d, stderr %q", code, stderr)
	}
	if out != "" || !strings.Contains(stderr, "handbuch/company/onboarding") {
		t.Errorf("stdout %q, stderr %q", out, stderr)
	}
	if len(f.requests) != 0 {
		t.Errorf("%d requests", len(f.requests))
	}
}

func TestSpacePageListIsTheTree(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: answering(map[string]string{"/spaces/handbuch/pages": tree})}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, out, stderr := run(t, server, t.TempDir(), "space", "page", "list", "handbuch")

	if code != exit.OK || stderr != "" {
		t.Fatalf("code %d, stderr %q", code, stderr)
	}
	if !strings.HasPrefix(out, "company  ") || !strings.Contains(out, "\n  company/onboarding ") {
		t.Errorf("stdout %q is not the tree", out)
	}
	// No head above it: the tree is the answer here, and the space is
	// `pa space view`.
	if strings.Contains(out, "closed to agents") {
		t.Errorf("stdout %q carries the space's head", out)
	}
}

// --json is the slim tree as the route answered it, bodies and all absent
// (ADR 0012) — the difference from `pa space view --json`, which is the space.
func TestSpacePageListJSONIsTheSummaries(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: answering(map[string]string{"/spaces/handbuch/pages": tree})}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, out, _ := run(t, server, t.TempDir(), "space", "page", "list", "handbuch", "--json")

	if code != exit.OK {
		t.Fatalf("code %d", code)
	}
	if !strings.Contains(out, `"path": "company/onboarding"`) || strings.Contains(out, `"body"`) {
		t.Errorf("stdout %q", out)
	}
}

// The address says where the page lands: the last slug is its own, everything
// in front of it is its parent.
func TestSpacePageCreateReadsTheParentOutOfTheAddress(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: func(_ *http.Request) (int, string) { return 201, spacePage }}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, out, stderr := runWith(t, server, t.TempDir(), "Am ersten Tag bekommt jede neue Person eine Karte.\n",
		"space", "page", "create", "handbuch/company/onboarding", "--title", "Onboarding", "--body-file", "-")

	if code != exit.OK || stderr != "" {
		t.Fatalf("code %d, stderr %q", code, stderr)
	}
	asked := f.requests[0]
	if asked.Method != http.MethodPost || asked.RequestURI != "/spaces/handbuch/pages" {
		t.Fatalf("%s %s", asked.Method, asked.RequestURI)
	}
	var sent map[string]any
	if err := json.Unmarshal([]byte(f.bodies[0]), &sent); err != nil {
		t.Fatal(err)
	}
	if sent["slug"] != "onboarding" || sent["parent"] != "company" || sent["title"] != "Onboarding" {
		t.Errorf("sent %v", sent)
	}
	if sent["body"] != "Am ersten Tag bekommt jede neue Person eine Karte." {
		t.Errorf("body %q", sent["body"])
	}
	if !strings.Contains(out, "handbuch/company/onboarding") {
		t.Errorf("stdout %q is not the page it created", out)
	}
}

// A page at the top of a space has no parent, and an address of two segments
// is how that is written.
func TestSpacePageCreateAtTheTopOfASpaceHasNoParent(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: func(_ *http.Request) (int, string) { return 201, spacePage }}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, _, stderr := run(t, server, t.TempDir(), "space", "page", "create", "handbuch/company", "--title", "Die Firma")

	if code != exit.OK || stderr != "" {
		t.Fatalf("code %d, stderr %q", code, stderr)
	}
	var sent map[string]any
	if err := json.Unmarshal([]byte(f.bodies[0]), &sent); err != nil {
		t.Fatal(err)
	}
	if sent["slug"] != "company" || sent["parent"] != nil {
		t.Errorf("sent %v", sent)
	}
}

func TestSpacePageCreateWithoutATitleIsAUsageMistake(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: func(_ *http.Request) (int, string) { return 201, spacePage }}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, _, stderr := run(t, server, t.TempDir(), "space", "page", "create", "handbuch/company/onboarding")

	if code != exit.Usage || !strings.Contains(stderr, "--title") {
		t.Fatalf("code %d, stderr %q", code, stderr)
	}
	if len(f.requests) != 0 {
		t.Errorf("%d requests", len(f.requests))
	}
}

func TestSpacePageEditCarriesTheGuardAndTheAddress(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: serving(spacePage)}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, _, stderr := runWith(t, server, t.TempDir(), "Ein neuer Text.\n",
		"space", "page", "edit", "handbuch/company/onboarding", "--body-file", "-", "--if-match", "2026-09-13T11:00:00.000000Z")

	if code != exit.OK || stderr != "" {
		t.Fatalf("code %d, stderr %q", code, stderr)
	}
	asked := f.requests[0]
	if asked.Method != http.MethodPatch || asked.RequestURI != "/spaces/handbuch/pages/company/onboarding" {
		t.Fatalf("%s %s", asked.Method, asked.RequestURI)
	}
	if got := asked.Header.Get("If-Match"); got != `"2026-09-13T11:00:00.000000Z"` {
		t.Errorf("If-Match %q", got)
	}
	if f.bodies[0] != `{"body":"Ein neuer Text."}` {
		t.Errorf("sent %q", f.bodies[0])
	}
}

func TestSpacePageEditWithNothingToChangeIsAUsageMistake(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: serving(spacePage)}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, _, stderr := run(t, server, t.TempDir(), "space", "page", "edit", "handbuch/company/onboarding")

	if code != exit.Usage {
		t.Fatalf("code %d, stderr %q", code, stderr)
	}
	// And it says where the two things it will not do live.
	if !strings.Contains(stderr, "pa space page rename") || !strings.Contains(stderr, "pa space page move") {
		t.Errorf("stderr %q", stderr)
	}
}

func TestSpacePageRenameSendsTheSlugAlone(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: serving(spacePage)}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, _, stderr := run(t, server, t.TempDir(), "space", "page", "rename", "handbuch/company/onboarding", "einarbeitung")

	if code != exit.OK || stderr != "" {
		t.Fatalf("code %d, stderr %q", code, stderr)
	}
	if f.bodies[0] != `{"slug":"einarbeitung"}` {
		t.Errorf("sent %q", f.bodies[0])
	}
}

// Renaming is what the page is called where it stands; where it stands is
// `move`. An address in the second argument is the confusion of the two.
func TestSpacePageRenameRefusesAnAddress(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: serving(spacePage)}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, _, stderr := run(t, server, t.TempDir(), "space", "page", "rename", "handbuch/company/onboarding", "product/einarbeitung")

	if code != exit.Usage || !strings.Contains(stderr, "pa space page move") {
		t.Fatalf("code %d, stderr %q", code, stderr)
	}
	if len(f.requests) != 0 {
		t.Errorf("%d requests", len(f.requests))
	}
}

// A page this caller cannot reach — deleted, never there, or in a space that
// is not there for it — answers with the instance's own sentence.
func TestSpacePageViewOfAPageThatIsNotThere(t *testing.T) {
	f := &fake{t: t, version: "0.0.0-dev", answer: func(_ *http.Request) (int, string) {
		return 404, `{"type":"about:blank","title":"Nothing by that key or id","status":404,"detail":"No page company/onboarding in handbuch.","code":"not-found"}`
	}}
	server := httptest.NewServer(f.handler())
	defer server.Close()

	code, out, stderr := run(t, server, t.TempDir(), "space", "page", "view", "handbuch/company/onboarding")

	if code != exit.NotFound {
		t.Fatalf("code %d", code)
	}
	if out != "" || !strings.Contains(stderr, "No page company/onboarding in handbuch.") {
		t.Errorf("stdout %q, stderr %q", out, stderr)
	}
}
