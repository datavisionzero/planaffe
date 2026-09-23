package client

import (
	"context"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"sync"
	"testing"
	"time"

	"github.com/datavisionzero/planaffe/src/cli/internal/api"
	"github.com/datavisionzero/planaffe/src/cli/internal/config"
)

// dropping answers nothing to the first `drop` requests — the connection goes
// away before a single byte of a response — and a token to the next one, and
// keeps what each request carried.
type dropping struct {
	mu     sync.Mutex
	drop   int
	keys   []string
	bodies []string
}

func (d *dropping) handler(t *testing.T) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		body, _ := io.ReadAll(r.Body)
		d.mu.Lock()
		d.keys = append(d.keys, r.Header.Get(IdempotencyHeader))
		d.bodies = append(d.bodies, string(body))
		dropped := len(d.keys) <= d.drop
		d.mu.Unlock()

		if dropped {
			conn, _, err := w.(http.Hijacker).Hijack()
			if err != nil {
				t.Errorf("hijack: %v", err)
				return
			}
			_ = conn.Close()
			return
		}

		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusCreated)
		_, _ = w.Write([]byte(`{"id":"0198e0c0-0000-7000-8000-000000000009","prefix":"pa_ABCDE","secret":"pa_x","created_at":"2026-09-09T10:00:00Z"}`))
	})
}

func fast(t *testing.T) {
	t.Helper()
	previous := Backoff
	Backoff = time.Millisecond
	t.Cleanup(func() { Backoff = previous })
}

func TestAWriteWhoseConnectionFailsBeforeAnAnswerIsSentAgainWithTheSameKey(t *testing.T) {
	fast(t)
	instance := &dropping{drop: 1}
	server := httptest.NewServer(instance.handler(t))
	defer server.Close()

	c, err := New(config.Config{URL: server.URL, Token: "pa_token"}, server.Client())
	if err != nil {
		t.Fatal(err)
	}

	resp, err := c.CreateAgentWithResponse(context.Background(), agent("one"))
	if err != nil {
		t.Fatalf("the repetition did not happen: %v", err)
	}
	if resp.StatusCode() != http.StatusCreated {
		t.Fatalf("status %d", resp.StatusCode())
	}

	if len(instance.keys) != 2 {
		t.Fatalf("expected two sendings, got %d", len(instance.keys))
	}
	if instance.keys[0] == "" || instance.keys[0] != instance.keys[1] {
		t.Fatalf("the repetition carried another key: %q", instance.keys)
	}
	if instance.bodies[0] == "" || instance.bodies[0] != instance.bodies[1] {
		t.Fatalf("the repetition carried another body: %q", instance.bodies)
	}
}

func TestAWriteIsSentAtMostAttemptsTimes(t *testing.T) {
	fast(t)
	instance := &dropping{drop: Attempts}
	server := httptest.NewServer(instance.handler(t))
	defer server.Close()

	c, err := New(config.Config{URL: server.URL, Token: "pa_token"}, server.Client())
	if err != nil {
		t.Fatal(err)
	}

	if _, err := c.CreateAgentWithResponse(context.Background(), agent("one")); err == nil {
		t.Fatal("a write that never got an answer succeeded")
	}
	if len(instance.keys) != Attempts {
		t.Fatalf("expected %d sendings, got %d", Attempts, len(instance.keys))
	}
}

func TestAReadAndAnAnswerAreNeverSentAgain(t *testing.T) {
	fast(t)
	instance := &dropping{drop: 1}
	server := httptest.NewServer(instance.handler(t))
	defer server.Close()

	c, err := New(config.Config{URL: server.URL, Token: "pa_token"}, server.Client())
	if err != nil {
		t.Fatal(err)
	}

	// A read carries no key, so nothing makes a second sending safe to say so.
	if _, err := c.ReadVersionWithResponse(context.Background()); err == nil {
		t.Fatal("a dropped read succeeded")
	}
	if len(instance.keys) != 1 {
		t.Fatalf("a read was sent %d times", len(instance.keys))
	}

	// An answer, whatever its status, is final.
	refusing := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		instance.mu.Lock()
		instance.keys = append(instance.keys, r.Header.Get(IdempotencyHeader))
		instance.mu.Unlock()
		w.Header().Set("Content-Type", "application/problem+json")
		w.WriteHeader(http.StatusBadGateway)
		_, _ = w.Write([]byte(`{"type":"/problems/internal","status":502}`))
	}))
	defer refusing.Close()
	c, err = New(config.Config{URL: refusing.URL, Token: "pa_token"}, refusing.Client())
	if err != nil {
		t.Fatal(err)
	}
	resp, err := c.CreateAgentWithResponse(context.Background(), agent("one"))
	if err != nil || resp.StatusCode() != http.StatusBadGateway {
		t.Fatalf("unexpected %v, %v", resp, err)
	}
	if got := len(instance.keys); got != 2 || !strings.Contains(instance.keys[1], "-1") {
		t.Fatalf("an answered write was sent again: %q", instance.keys)
	}
}

func agent(name string) api.CreateAgentRequest { return api.CreateAgentRequest{Name: &name} }
