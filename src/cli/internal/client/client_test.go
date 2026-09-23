package client

import (
	"context"
	"errors"
	"fmt"
	"net"
	"net/http"
	"net/http/httptest"
	"net/url"
	"os"
	"strings"
	"testing"

	"github.com/datavisionzero/planaffe/src/cli/internal/api"
	"github.com/datavisionzero/planaffe/src/cli/internal/config"
	"github.com/datavisionzero/planaffe/src/cli/internal/exit"
	"github.com/datavisionzero/planaffe/src/cli/internal/version"
)

func answer(status int, contentType, body string) (*http.Response, []byte) {
	resp := &http.Response{
		StatusCode: status,
		Status:     fmt.Sprintf("%d %s", status, http.StatusText(status)),
		Header:     http.Header{},
		Request:    &http.Request{URL: &url.URL{Path: "/issues/PLAN-42"}},
	}
	if contentType != "" {
		resp.Header.Set("Content-Type", contentType)
	}
	return resp, []byte(body)
}

func code(t *testing.T, err error) int {
	t.Helper()
	if err == nil {
		return exit.OK
	}
	var failure *Failure
	if !errors.As(err, &failure) {
		t.Fatalf("%v is not a Failure", err)
	}
	return failure.Code
}

func TestTransportSaysWhatWentWrongOnTheWay(t *testing.T) {
	refused := &url.Error{Op: "Get", URL: "http://127.0.0.1:1/version", Err: &net.OpError{Op: "dial", Net: "tcp", Err: errors.New("connection refused")}}
	canceled := &url.Error{Op: "Get", URL: "http://planaffe.example/next", Err: context.Canceled}

	cases := []struct {
		name string
		err  error
		want int
	}{
		{"connection refused", refused, exit.Unreachable},
		{"a deadline", context.DeadlineExceeded, exit.Unreachable},
		{"a deadline of the OS", os.ErrDeadlineExceeded, exit.Unreachable},
		// Ctrl-C comes wrapped in a *url.Error, and it is not the instance's fault.
		{"ctrl-c during a request", canceled, exit.Interrupted},
		{"ctrl-c", context.Canceled, exit.Interrupted},
		{"a failure already", &Failure{Code: exit.Skew, Message: "skew"}, exit.Skew},
		{"anything else", errors.New("unexpected end of JSON input"), exit.Unexpected},
	}
	for _, c := range cases {
		if got := code(t, Transport(c.err)); got != c.want {
			t.Errorf("%s: code %d, want %d", c.name, got, c.want)
		}
	}

	if Transport(nil) != nil {
		t.Error("no error is no failure")
	}
	if err := Transport(refused); !strings.Contains(err.Error(), "could not be reached") {
		t.Errorf("unexpected message %q", err)
	}
}

func TestCheckReadsTheStatusAndTheProblem(t *testing.T) {
	cases := []struct {
		name        string
		status      int
		contentType string
		body        string
		want        int
		message     string
	}{
		{"json", 200, "application/json", `{}`, exit.OK, ""},
		{"a json suffix", 200, "application/merge-patch+json; charset=utf-8", `{}`, exit.OK, ""},
		{"no content", 204, "", "", exit.OK, ""},
		{"accepted", 202, "", "", exit.OK, ""},
		// An endpoint this pa knows and that instance does not: the web
		// application's index.html, with 200.
		{"html", 200, "text/html", "<!doctype html>", exit.Unexpected, "text/html, not JSON"},
		{"no type at all", 200, "", "{}", exit.Unexpected, "something else, not JSON"},
		// A proxy answering 200 with nothing where a body is promised.
		{"an empty ok", 200, "", "", exit.Unexpected, "no body"},
		{"an empty created", 201, "application/json", "", exit.Unexpected, "no body"},
		{"a problem", 409, "application/problem+json", `{"type":"/problems/claim-held","status":409,"detail":"PLAN-42 is claimed."}`, exit.Conflict, "PLAN-42 is claimed. (claim-held)"},
		{"stale", 412, "application/problem+json", `{"type":"/problems/stale","status":412,"title":"stale"}`, exit.Stale, "stale"},
		{"a missing endpoint", 404, "text/plain", "", exit.NotFound, "does not have this endpoint yet"},
		{"a server error", 502, "text/html", "<html>", exit.Unexpected, "502 Bad Gateway"},
	}
	for _, c := range cases {
		resp, body := answer(c.status, c.contentType, c.body)
		err := Check(resp, body)
		if got := code(t, err); got != c.want {
			t.Errorf("%s: code %d, want %d (%v)", c.name, got, c.want, err)
			continue
		}
		if c.message != "" && !strings.Contains(err.Error(), c.message) {
			t.Errorf("%s: message %q lacks %q", c.name, err, c.message)
		}
	}

	if got := code(t, Check(nil, nil)); got != exit.Unexpected {
		t.Errorf("no response: code %d", got)
	}
}

func TestCheckStopsAtAVersionSkewBeforeAnythingElse(t *testing.T) {
	defer func(previous string) { version.Version = previous }(version.Version)
	version.Version = "1.2.0"

	resp, body := answer(409, "application/problem+json", `{"type":"/problems/claim-held","status":409}`)
	resp.Header.Set(VersionHeader, "2.0.0")
	if got := code(t, Check(resp, body)); got != exit.Skew {
		t.Fatalf("code %d", got)
	}

	resp.Header.Set(VersionHeader, "1.1.3")
	if got := code(t, Check(resp, body)); got != exit.Conflict {
		t.Fatalf("an older minor of the instance fits; code %d", got)
	}
}

func TestEveryRequestCarriesWhoAndEveryWriteItsOwnKey(t *testing.T) {
	var requests []*http.Request
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		requests = append(requests, r)
		w.Header().Set("Content-Type", "application/json")
		_, _ = w.Write([]byte(`{"version":"0.0.0-dev"}`))
	}))
	defer server.Close()

	c, err := New(config.Config{URL: server.URL, Token: "pa_a-token"}, server.Client())
	if err != nil {
		t.Fatal(err)
	}
	ctx := context.Background()
	_, _ = c.ReadVersionWithResponse(ctx)
	_, _ = c.ClaimIssueWithResponse(ctx, "PLAN-1", api.ClaimRequest{})
	_, _ = c.ClaimIssueWithResponse(ctx, "PLAN-2", api.ClaimRequest{})

	if len(requests) != 3 {
		t.Fatalf("%d requests", len(requests))
	}
	for _, r := range requests {
		if r.Header.Get("Authorization") != "Bearer pa_a-token" || !strings.HasPrefix(r.Header.Get("User-Agent"), "pa/") {
			t.Errorf("%s %s: headers %v", r.Method, r.URL.Path, r.Header)
		}
	}
	if requests[0].Header.Get(IdempotencyHeader) != "" {
		t.Error("a read carries no idempotency key")
	}
	first, second := requests[1].Header.Get(IdempotencyHeader), requests[2].Header.Get(IdempotencyHeader)
	if first == "" || second == "" || first == second {
		t.Errorf("two writes of one invocation carry %q and %q", first, second)
	}

	// The one command with nothing in its hand presents no bearer at all.
	anonymous, err := New(config.Config{URL: server.URL}, server.Client())
	if err != nil {
		t.Fatal(err)
	}
	_, _ = anonymous.ReadVersionWithResponse(ctx)
	if got := requests[3].Header.Get("Authorization"); got != "" {
		t.Errorf("an anonymous request presented %q", got)
	}
}

func TestByAddressKeepsTheSlashesOfAPagesAddress(t *testing.T) {
	req, _ := http.NewRequest(http.MethodGet, "http://planaffe.example/spaces/handbuch/pages/company%2Fonboarding", nil)
	if err := ByAddress(context.Background(), req); err != nil {
		t.Fatal(err)
	}
	if got := req.URL.String(); got != "http://planaffe.example/spaces/handbuch/pages/company/onboarding" {
		t.Fatalf("the address travels as %q", got)
	}
}

func TestCheckedIsTheCallAndTheCheck(t *testing.T) {
	resp, body := answer(200, "application/json", `{"version":"1.0.0"}`)
	read := &api.ReadVersionResponse{HTTPResponse: resp, Body: body}
	if got, err := Checked(read, nil); err != nil || got != read {
		t.Fatalf("a success came back as %v, %v", got, err)
	}

	if _, err := Checked[api.ReadVersionResponse](nil, context.Canceled); code(t, err) != exit.Interrupted {
		t.Fatalf("a transport error came back as %v", err)
	}

	resp, body = answer(404, "application/problem+json", `{"type":"/problems/not-found","status":404}`)
	if _, err := Checked(&api.ReadVersionResponse{HTTPResponse: resp, Body: body}, nil); code(t, err) != exit.NotFound {
		t.Fatalf("a refusal came back as %v", err)
	}

	// A type without the two fields is a bug in pa, said as one.
	type strange struct{ Payload string }
	if _, err := Checked(&strange{}, nil); code(t, err) != exit.Unexpected || !strings.Contains(err.Error(), "bug in pa") {
		t.Fatalf("a strange type came back as %v", err)
	}
	if _, err := Checked[api.ReadVersionResponse](nil, nil); code(t, err) != exit.Unexpected {
		t.Fatalf("nothing at all came back as %v", err)
	}
}
