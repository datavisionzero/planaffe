# A Token Is an Agent or a User's Key, and an Agent Is Never an Administrator

A token is one of two things. An **agent token** is an agent: the identity a run
works under, with a name, the metadata it reports about itself, and the one-way
back channel of VISION 12. A **user token** is a human's key to the CLI: it
authenticates *as the user*, is no identity of its own, and has none of the
above. Both are created by a human, shown once, and revoked rather than deleted.
The administrator role belongs to users only; an agent token never carries it,
whoever owns it.

## Why a second kind of token

The vision said "the token is the agent" and, in the same breath, that the CLI is
the interface "for agents and for console-minded humans" authenticated "via API
token". Read together, a human at the console works as an agent — and every rule
the product hangs on the difference stops applying to them: their claim expires
after four hours (VISION 11 says a user's never does), their close lands in
`review` where the switch is on (VISION 9 says a user's goes to `done`), and the
history records their answer to a question as an agent's. The commissioning path
(VISION 12) has the first human run `pa project create` with "the first token",
which under the old reading was an agent creating a project.

**Not a login.** `pa auth login` with a session stored on disk would be a second
mechanism beside the environment variable, and a file an agent on the same
machine can read — the identity escape VISION 12 exists to close. A user token is
the same mechanism as the agent token, carried the same way, told apart by the
server.

**Not the agent's token, used by its owner.** Ownership answers *which* human is
behind a token; it does not make the token's acts a human's acts. Review
required, the expiring claim and the history all rest on telling the two apart.
(Triage required did too until
[ADR 0019](./0019-triage-required-selects-it-does-not-permit.md) — it now
governs what `next` hands out and not who writes `ready`, which is one place
fewer where picking a token changes what one may do.)

## Why an agent is never an administrator

A token is valid across projects "like the human identity it belongs to" (VISION
12), and nothing said whether it inherits the administrator role as well. If it
did, the agent of an administrator could invite users, delete projects and hand
out project access — while VISION 12 forbids it to create so much as a second
token, because "an agent that can issue itself a second token has escaped its own
identity". Inviting a user is a wider door than that.

So instance administration — users, projects, project access — is a human's act,
like creating a token, and an agent token never carries the role. Project
creation stays on the human side too: the project key is the one thing never
changed afterwards, and it should be typed by a person.

## Consequences

- **The bootstrap token is a user token** of the first administrator, and the
  first agent gets its own token from that administrator one command later. The
  commissioning path needs no UI, as VISION 12 requires, and no agent creates a
  project.
- **Everything VISION 12 says about naming, metadata and the back channel is
  about agent tokens.** A user token has no name to show in a list and no
  metadata to report; it is the user.
- **The CLI does not know which kind it holds.** `PLANAFFE_TOKEN` carries either;
  the server tells them apart and applies the user's rules or the agent's. No
  flag, no second variable.
- **The same line runs through `claim --force`.** A user's claim does not expire
  (VISION 11); an agent taking it over with `--force` would undo that protection
  by another road. Over a user's claim, only a user may; over an agent's claim,
  anyone.
- **Tokens of both kinds are never deleted**, for the reason ADR 0013 gives: a
  revoked user token names nobody new, but revoking rather than deleting keeps
  one lifecycle for one table.
