# The MVP Is Built in Three Cuts, and the First One Ends at the Switch-Over

The MVP of [VISION 14](../../VISION.md) is built in three cuts, in order. **Cut
one is finished when the file-based tracker in `scratchpad/` can be deleted** —
not when a list of features is ticked off. Cut two makes unattended operation
work, cut three builds the interface for humans.

This changes the order of the work, not the scope of the product. Version 1.0 is
still everything VISION 14 lists; the cuts only say what exists first.

## The cuts

**Cut one — the switch-over.** Project with its *triage required* and *review
required* switches, the
issue with the field set of VISION 8 except `release` and `parent`, the status
set, the claim and its expiry, the history, labels with groups, the epic as a
bracket, `blocks`/`blocked_by`, the question, `pa next --claim` evaluating the
conditions of VISION 10, creating several wired-up issues in one act, users with
their user tokens and agents with their named tokens (ADR 0015), the bootstrap
path, the CLI for all of it, and Docker Compose. No web application.

**Cut two — unattended operation.** Sub-issues, `--wait` on `LISTEN`/`NOTIFY` —
for `next`, for a question's answer, and for "needs you" as a list of the API —
releases, full-text search, the agent metadata back channel, bulk operations
beyond creating, `pa export --json`.

**Cut three — for humans.** The design foundation, the shell and its views —
issue list, issue detail, epic, release, "needs you" — user administration,
project assignment and the admin role.

## Why not the thinnest possible core

The obvious first cut is smaller than this one: project, issue, status, claim,
`next`, token, CLI, and nothing else. It was rejected because it produces
something that runs and cannot be used.

**Without the question, `next` livelocks.** VISION 10 describes the way back: an
agent claims an issue, finds it too vague, asks a question and releases it —
whereupon the issue is no longer workable because a question is open. Take the
question away and all that is left is releasing, and the issue is immediately the
next one again. The same agent pulls it, gets stuck, releases it. The only exit
is a human, which is the thing `next` exists to abolish.

**Without blockers, `next` cannot keep its promise.** VISION 8 states it plainly:
"Without this field, *give me the next ready issue* cannot keep its promise,
because *ready* does not only mean well specified but also unblocked."

**Without epics and labels there is no switch-over.** The tracker that is to be
replaced already uses `epic`, `labels`, `blocked_by`, `ready` and `priority`. A
first cut that does not know them leaves the product running beside the work
instead of carrying it.

Everything else was kept out by one rule: **what is needed for the switch-over
goes in, plus what cannot be added later without losing something.** The history
qualifies under the second half — VISION 7 says you only ask for it once
something has gone wrong, and then the data is either there or gone for good.
Identity qualifies too: VISION 12 says a token is the agent, and attaching that
afterwards devalues every claim and every history entry written before it.

## Consequences

**The readiness conditions of VISION 10 are a conjunction, so a missing concept
is silently satisfied.** With no sub-issues in cut one, conditions five and
eight are vacuously true. Cut two adds conditions rather than rewriting them, which is what
makes this split safe.

**Between cut one and cut three nobody outside can judge the product.** A visitor
finds an issue tracker with no issue view. That is the price of guiding principle
1, and the reason the distinction between a cut and a release has to be stated
rather than assumed.

**The design foundation is decided late, on real screens.** ADR 0006 argues the
shell is where a foundation is cheapest to swap; choosing it before there is data
to put in it would be choosing it on mockups alone.

**The finish line is falsifiable.** "Can the tracker in `scratchpad/` be deleted"
has an answer on any given day, and it is not the answer to "does it feel done".
