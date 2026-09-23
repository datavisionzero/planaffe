package keychain

import (
	"errors"
	"strings"
	"testing"

	"github.com/zalando/go-keyring"
)

func TestTranslateSaysWhatAnErrorOfTheStoreMeansHere(t *testing.T) {
	if err := translate(nil); err != nil {
		t.Errorf("nil became %v", err)
	}
	if err := translate(keyring.ErrNotFound); !errors.Is(err, ErrNotFound) {
		t.Errorf("not found became %v", err)
	}
	if err := translate(keyring.ErrUnsupportedPlatform); !errors.Is(err, ErrUnavailable) {
		t.Errorf("an unsupported platform became %v", err)
	}

	// A missing Secret Service arrives as a D-Bus error of no kind of its own;
	// it is no keychain, and it keeps what it said.
	err := translate(errors.New("org.freedesktop.DBus.Error.ServiceUnknown"))
	if !errors.Is(err, ErrUnavailable) || !strings.Contains(err.Error(), "ServiceUnknown") {
		t.Errorf("a D-Bus error became %v", err)
	}
}

// The system store round-trips through go-keyring's own in-memory provider, so
// that no test touches the keychain of whoever runs them.
func TestTheSystemStoreKeepsOneTokenPerInstance(t *testing.T) {
	keyring.MockInit()
	store := System()

	if _, err := store.Get("https://a.example"); !errors.Is(err, ErrNotFound) {
		t.Fatalf("an empty store answered %v", err)
	}
	if err := store.Set("https://a.example", "token-a"); err != nil {
		t.Fatal(err)
	}
	if err := store.Set("https://b.example", "token-b"); err != nil {
		t.Fatal(err)
	}
	if token, err := store.Get("https://a.example"); err != nil || token != "token-a" {
		t.Fatalf("a holds %q, %v", token, err)
	}

	if err := store.Delete("https://a.example"); err != nil {
		t.Fatal(err)
	}
	// Forgetting what is not there is not an error: `pa logout` twice.
	if err := store.Delete("https://a.example"); err != nil {
		t.Fatalf("a second delete answered %v", err)
	}
	if token, err := store.Get("https://b.example"); err != nil || token != "token-b" {
		t.Fatalf("b holds %q, %v", token, err)
	}
}

func TestAStoreThatFailsIsUnavailable(t *testing.T) {
	keyring.MockInitWithError(errors.New("no Secret Service on the bus"))
	store := System()

	if err := store.Set("https://a.example", "token"); !errors.Is(err, ErrUnavailable) {
		t.Errorf("set answered %v", err)
	}
	if _, err := store.Get("https://a.example"); !errors.Is(err, ErrUnavailable) {
		t.Errorf("get answered %v", err)
	}
	if err := store.Delete("https://a.example"); !errors.Is(err, ErrUnavailable) {
		t.Errorf("delete answered %v", err)
	}
}

func TestTheAdviceNamesBothWaysOn(t *testing.T) {
	for _, want := range []string{"PLANAFFE_TOKEN", "--token-file"} {
		if !strings.Contains(Advice, want) {
			t.Errorf("the advice lacks %q", want)
		}
	}
}
