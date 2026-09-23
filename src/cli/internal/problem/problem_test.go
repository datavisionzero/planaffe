package problem

import (
	"testing"
)

func TestParseReadsTheDocumentAndKeepsTheExtensions(t *testing.T) {
	p := Parse([]byte(`{"type":"/problems/claim-held","title":"claim-held","status":409,"detail":"PLAN-42 is claimed.","instance":"/issues/PLAN-42/claim","holder":{"name":"quiet-otter-42"}}`))
	if p == nil {
		t.Fatal("a problem document was not read")
	}
	if p.Type != "/problems/claim-held" || p.Title != "claim-held" || p.Status != 409 || p.Detail != "PLAN-42 is claimed." || p.Instance != "/issues/PLAN-42/claim" {
		t.Fatalf("unexpected problem %+v", p)
	}
	if string(p.Extra["holder"]) != `{"name":"quiet-otter-42"}` || len(p.Extra) != 1 {
		t.Fatalf("unexpected extensions %v", p.Extra)
	}
	if p.Code() != "claim-held" {
		t.Errorf("code %q", p.Code())
	}
	if p.Message() != "PLAN-42 is claimed. (claim-held)" {
		t.Errorf("message %q", p.Message())
	}
}

func TestParseIsNilForWhatIsNotAProblemDocument(t *testing.T) {
	for _, body := range []string{"", "<html></html>", `{"detail":"no type"}`, `[1,2]`} {
		if p := Parse([]byte(body)); p != nil {
			t.Errorf("%q was read as %+v", body, p)
		}
	}
}

func TestCodeAndMessageFallBack(t *testing.T) {
	var none *Problem
	if none.Code() != "" || none.Message() != "" {
		t.Error("a nil problem has neither a code nor a message")
	}

	// No detail: the title stands in.
	if got := (&Problem{Type: "/problems/stale", Title: "The row changed."}).Message(); got != "The row changed. (stale)" {
		t.Errorf("message %q", got)
	}
	// A type without a slash is its own code.
	if got := (&Problem{Type: "validation"}).Code(); got != "validation" {
		t.Errorf("code %q", got)
	}
	// No type at all: the text alone.
	if got := (&Problem{Detail: "Something."}).Message(); got != "Something." {
		t.Errorf("message %q", got)
	}
}
