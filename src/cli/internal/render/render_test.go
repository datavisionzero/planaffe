package render

import (
	"bytes"
	"fmt"
	"strings"
	"testing"

	"github.com/datavisionzero/planaffe/src/cli/internal/api"
)

func summary(key string, epic *string, title string) api.IssueSummary {
	return api.IssueSummary{Key: key, Priority: 2, Status: "todo", Title: title, Epic: epic}
}

// The epic belongs in the row, because a list that hides it makes the reader
// open a ticket to learn what it is part of.
func TestSummariesNameTheEpicOfEveryIssueThatHasOne(t *testing.T) {
	epic := "PLAN-E1"
	var out bytes.Buffer

	Summaries(&out, []api.IssueSummary{
		summary("PLAN-1", &epic, "The space in the domain"),
		summary("PLAN-5", nil, "The epic in the list"),
	})

	lines := strings.Split(strings.TrimRight(out.String(), "\n"), "\n")
	if len(lines) != 2 {
		t.Fatalf("lines = %q", lines)
	}
	if !strings.Contains(lines[0], "PLAN-E1") {
		t.Errorf("the epic is missing from %q", lines[0])
	}
	if strings.Contains(lines[1], "PLAN-E") {
		t.Errorf("an issue without an epic names none: %q", lines[1])
	}
	// The column is a column: the titles start in the same place.
	if strings.Index(lines[0], "The space") != strings.Index(lines[1], "The epic") {
		t.Errorf("the titles are not aligned:\n%q\n%q", lines[0], lines[1])
	}
}

// A column of blanks costs width a terminal does not have.
func TestSummariesLeaveTheEpicColumnOutWhenNothingHasOne(t *testing.T) {
	var with, without bytes.Buffer
	epic := "PLAN-E1"

	Summaries(&with, []api.IssueSummary{summary("PLAN-1", &epic, "Titled")})
	Summaries(&without, []api.IssueSummary{summary("PLAN-1", nil, "Titled")})

	if len(without.String()) >= len(with.String()) {
		t.Errorf("the empty column was printed anyway: %q", without.String())
	}
	if want := fmt.Sprintf("%-10s P%d  %-11s  ", "PLAN-1", 2, "todo"); !strings.HasPrefix(without.String(), want+"Titled") {
		t.Errorf("the row without an epic is not the row it always was: %q", without.String())
	}
}
