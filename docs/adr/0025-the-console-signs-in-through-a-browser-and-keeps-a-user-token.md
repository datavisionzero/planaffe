# The Console Signs In Through a Browser, and What It Keeps Is a User Token

`pa login` runs the **device-code flow**: the CLI prints a short code, a human
confirms it in a browser on this machine or on another one, and what the CLI
collects is an ordinary **user token** — the same thing `pa token create` makes,
delivered differently. It goes into the operating system's keychain, and where
there is none the CLI **says so, writes nothing, and names the two ways on**.

This overrides the paragraph "Not a login" of
[ADR 0015](./0015-a-token-is-an-agent-or-a-users-key-and-an-agent-is-never-an-administrator.md).
Everything else that ADR decides stands, and one of its consequences is what
makes this cheap: a user token is the same mechanism as an agent token, carried
the same way, told apart by the server. `login` adds no second kind of
credential. It adds a way of getting one that does not go through a person's
clipboard.

## What ADR 0015 refused, and what is different now

It refused "`pa auth login` with a session stored on disk", for three reasons:
it would be a second mechanism beside the environment variable, it would put a
file on the machine an agent could read, and that is the identity escape VISION
12 exists to close.

Two of the three were about a session **in a file**. That is not the only place
a login can put one, and the sibling project settled the question in
[vaultaffe ADR 0012](https://github.com/datavisionzero/vaultaffe/blob/main/docs/adr/0012-a-session-lives-in-the-keychain-and-nowhere-quietly.md): the
credential goes into the operating system's own store, and the machine that has
no store gets a sentence with two named ways on instead of a quiet plaintext
fallback. Adopt that whole and the file is gone.

The third — the second mechanism — is answered by not introducing one. What the
browser hands back here is a user token in `Authorization: Bearer`, exactly like
the one a person copies out of the interface today. The server tells no
difference, `pa token list` shows it beside the others, and `pa token revoke`
ends it. There is one credential kind for the CLI, as before.

## The environment variable is the leak, not the protection

This is the argument ADR 0015 did not have in front of it, and it is the
decisive one.

Today a console-minded human puts their user token in `PLANAFFE_TOKEN`, which in
practice means a line in a shell profile. **Every agent started from that shell
inherits it.** Not through a file it had to find and open — in its own
environment, where the first thing a harness does is pass the environment
through. An agent holding a user token has a claim that never expires (VISION
11), a close that goes straight to `done` past `review` (VISION 9,
[ADR 0014](./0014-review-is-a-status-and-a-project-switch.md)), and an answer to
a question that the history records as a human's word. Every rule this product
hangs on the difference between the two kinds of caller is forgeable, by
default, on exactly the machine agents run on.

A token in the keychain is not inherited. An agent would have to reach for the
store on purpose, as the same operating-system user — which is not a boundary
and is not claimed to be one, but it is the difference between a credential that
leaks by standing still and one that has to be taken. `login` is therefore not a
weakening of what ADR 0015 was protecting. It is the repair of it.

`PLANAFFE_TOKEN` keeps its job and keeps precedence over the keychain: it is how
an agent receives its own token, how CI holds one, and how a container is told
who it is. What changes is that a human no longer has to use the agents' channel
to be themselves.

## Why the device-code flow, and not a password prompt

`pa` is never interactive — no prompt, no editor, no pager
([`docs/cli.md`](../cli.md), Commitments to agents) — and asking for a password
on a terminal would be the first exception, on the one command where the stakes
are highest. It would also not work where this CLI actually runs: over SSH, in a
container, in an agent's sandbox, there is no browser to open and no reason to
trust the terminal with a password.

The device-code flow needs neither. The CLI prints eight characters, the human
opens `/device` in whatever browser they already have a session in, and the
confirmation happens where passwords already belong. It is the pattern the
target group of VISION 3 knows from every AI CLI they already run, which is not
a fashion argument: a login nobody has to be taught is a login nobody works
around.

The short code is not the credential. It is eight consonants — no vowels, so it
can never come out as a word; no digits, so `0`/`O` and `1`/`I` cannot be
mistyped — good for ten minutes, and what it protects is a request that still
needs a signed-in human to press a button. The long **device code** the CLI
polls with is 256 bits, stored as its hash, and never shown to anybody.

## Why a token and not a session

Vaultaffe's device login hands back a session. Ours hands back a user token, and
the difference is deliberate.

**A session expires, and this product has already decided what it thinks of
that.** VISION 12: "A token that expires on a date wakes nobody — it fails in
the middle of an agent run." A browser session is seven days idle and thirty
absolute, which is right for a tab and wrong for the credential a person's
scripts, cron entries and `.envrc` files run under. Revocation is the answer
here as it is everywhere else in this product: deliberate, not scheduled.

**A session is a browser's thing.** It authenticates with a cookie, it carries
CSRF, it appears in `GET /sessions` as a place somebody is signed in. Making it
also arrive as a bearer token would give one row two doors and two threat models.

**A token is already listed and already revocable.** Nothing has to be built for
a person to see what their machines hold, and `pa logout` is one more caller of
a revocation that exists.

The confirmation page authenticates with the browser session the human already
has — planaffe has a full interface and a sign-in screen, so there is no reason
to ask for an email and a password a second time on that one page. Somebody not
signed in is sent to sign in and returned to the code.

## Consequences

- **A new dependency**, `github.com/zalando/go-keyring`, approved before it was
  added ([ADR 0023](./0023-a-dependency-is-a-decision-a-human-takes.md)): MIT,
  no cgo, `security` on macOS and D-Bus on Linux, already in use in the sibling
  project. It reaches no browser and weighs nothing there.
- **`pa` grows a configuration file** — `~/.config/planaffe/config` — and it
  holds the instance address and, where one was chosen, the *path* of a token
  file. Nothing in it is a credential. This is the first state `pa` keeps
  between invocations, and `docs/cli.md`'s "nothing to log into" stops being
  true.
- **The resolution order is fixed and says where it read from**:
  `PLANAFFE_URL`/`PLANAFFE_TOKEN` first, then a token file the user named out
  loud, then the keychain. An agent's environment always wins, so a `login` on
  the machine cannot quietly re-identify a run.
- **`pa logout` refuses a token that came from the environment.** It did not put
  it there, and revoking it would revoke something the person at the terminal
  may not know they are holding — an agent's token, most likely.
- **An agent token never comes through here.** `login` mints a user token
  because a user confirmed it; there is no shape of this flow that produces an
  agent, and an agent that could run `login` would be issuing itself a second
  identity, which VISION 12 forbids.
- **A device login is a row with a lifetime, and the only one in this product
  that expires by the clock rather than by an act.** Ten minutes, five states,
  and four of the five end the CLI's polling — `device-pending` is the one that
  does not.
- **The instance keeps no code it could read back.** The device code is stored
  as its SHA-256, like a token secret; the user code is stored in the clear
  because it is not a credential and the confirming human has to be shown the
  one they typed.
