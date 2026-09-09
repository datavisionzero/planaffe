// Package keychain is where a person's user token lives after `pa login` — the
// operating system's own store, and nowhere else quietly (ADR 0025).
//
// An agent's token never comes through here. It arrives in the environment as
// PLANAFFE_TOKEN, which is how the harness that started the agent hands it
// over (VISION 12), and the environment keeps precedence over this store for
// exactly that reason.
package keychain

import (
	"errors"
	"fmt"

	"github.com/zalando/go-keyring"
)

// Service is the name this CLI's entries carry in the store. One entry per
// instance: the account is the address, so two instances on one machine do not
// overwrite each other's token.
const Service = "planaffe"

// ErrUnavailable is no keychain on this machine — a headless Linux with no
// Secret Service, most often. It is not a failure to be worked around
// silently: a login that quietly wrote a credential to a file the user did not
// choose would be the thing ADR 0015 refused, arriving by another road.
var ErrUnavailable = errors.New("no keychain")

// ErrNotFound is a keychain that works and holds nothing for this instance.
var ErrNotFound = errors.New("no token for this instance")

// Store is the three functions the command tree is given, so that a test never
// touches the machine's own store and CI needs no Secret Service to run these
// tests.
type Store struct {
	Set    func(instance, token string) error
	Get    func(instance string) (string, error)
	Delete func(instance string) error
}

// System is the operating system's store: `security` on macOS, the Secret
// Service over D-Bus on Linux, and the credential manager on Windows.
func System() Store {
	return Store{
		Set: func(instance, token string) error {
			return translate(keyring.Set(Service, instance, token))
		},
		Get: func(instance string) (string, error) {
			token, err := keyring.Get(Service, instance)
			if err != nil {
				return "", translate(err)
			}
			return token, nil
		},
		Delete: func(instance string) error {
			err := keyring.Delete(Service, instance)
			if err == nil || errors.Is(err, keyring.ErrNotFound) {
				// Forgetting what was not there is not a failure: `pa logout`
				// twice is not an error.
				return nil
			}
			return translate(err)
		},
	}
}

// Advice is what `pa` says when there is no keychain: the two ways to hold a
// token on a machine that has no store of its own, named rather than chosen for
// the user.
const Advice = `This machine has no keychain pa can use (a headless Linux without a Secret Service, most often).
Nothing was written: a token belongs in a store, and writing one to a file you did not ask for is not a decision pa makes for you.
Two ways on:
  • put the token in the environment as PLANAFFE_TOKEN — how an agent receives one anyway, and how CI holds one;
  • or choose a file yourself: pa login --token-file ~/.config/planaffe/token, which writes it readable only by you.`

func translate(err error) error {
	switch {
	case err == nil:
		return nil
	case errors.Is(err, keyring.ErrNotFound):
		return ErrNotFound
	case errors.Is(err, keyring.ErrUnsupportedPlatform):
		return ErrUnavailable
	default:
		// go-keyring answers a missing Secret Service as a D-Bus error rather
		// than as a kind of its own, and there is nothing else it means here.
		return fmt.Errorf("%w: %v", ErrUnavailable, err)
	}
}
