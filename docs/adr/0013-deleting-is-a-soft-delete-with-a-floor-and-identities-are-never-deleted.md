# Deleting Is a Soft Delete With a Floor, and Identities Are Never Deleted

Deleting exists, and it is a soft delete. A deleted issue is invisible
everywhere — lists, counts, search, `next`, epic progress — and can be restored
for a grace period of seven days. Only after that is the row removed for good.
**Anyone who may edit an issue may delete it, agents included**; the grace period
is the safety net, not a permission.

Users and API tokens are the exception: they are **never** deleted, only
deactivated and revoked.

## Why deleting is needed at all

The vision does not have the verb. It lists create, read, change, comment and
close (VISION 6.1) and offers `canceled` for work that will not happen. That is
not the same thing: a cancelled issue stays visible in lists, in counts and in
epic progress, which is right for a decision that was made and wrong for a ticket
that should never have existed. Agents produce those — "create me seven issues"
returns two that are nonsense — and an issue tracker whose only answer to its own
noise is "keep it forever, marked" accumulates exactly the sludge VISION 6.1 says
the CLI must be able to clear.

## Why soft, and why agents may do it

Hard deletion by an agent is a bad trade: the agent that misreads a filter is the
same agent the product hands autonomy to, and there is no undo. Withholding the
verb from agents instead would move the cleanup back to a human, which is the
work VISION 6.1 explicitly wants done by an agent through the CLI.

The soft delete makes both unnecessary. A mistake is visible for a week and
reversible with one command, so the verb can be handed to the party that
generates the mess without the failure mode that usually comes with it.

## Why the floor is a floor

Removal happens **opportunistically, not on a schedule**: the next write
transaction that touches a project also removes that project's issues whose grace
period has passed, bounded to a small batch so no single request pays for a large
backlog.

This is deliberately the same shape as the expired claim, and for the same reason
VISION 11 gives there: an expired claim is evaluated on read rather than cleaned
up by a background job, "that saves a scheduler". Introducing one here would give
back the component that decision bought — and the wake-up research took the same
position when it declined a timer for due dates (VISION 17).

The honest consequence is that **seven days is a floor, not a deadline.** A
project nobody writes to keeps its deleted rows longer. For a product whose
operations are supposed to be "Postgres backups and nothing else" (VISION 16),
that is the right side to err on.

## What deletion does to everything else

- **Keys are never reused.** `PLAN-42` deleted stays spent; a reused key would
  make every history entry and every commit message that quotes it a lie.
- **A deleted issue counts as absent for readiness.** Issues that listed it in
  `blocked_by` become workable, exactly as if the blocker had closed.
- **An issue in a published release cannot be deleted.** A release is a record of
  what shipped (VISION 7), and a record is not edited. It can be deleted once no
  published release holds it, which for a shipped ticket is never.
- **Deleting a label detaches it** from the issues carrying it; it does not touch
  them otherwise.
- **A project is deleted the same way**, with everything in it, by an
  administrator, confirmed by typing the project key.
- **Restoring is one command** and restores the issue alone, into whatever state
  it was in. Its claim does not come back.

## Consequences

**Every read path filters deleted rows**, and forgetting that filter in one query
is the way this decision fails. It belongs in the store layer once, not in each
use case — the same place the expired claim is derived.

**`--deleted` lists what is in the grace period**, so an agent that deleted the
wrong seven issues can find them without a human opening a database client.

**The history of a deletion dies with the row it describes.** During the grace
period the entry is there and says who deleted it; after removal, both are gone.
Recording deletions somewhere that outlives the issue was rejected — it is a
second lifetime to reason about, for an event whose subject no longer exists.

**Identities outlive their access.** A revoked token still names the agent in
every claim and history entry it ever wrote; it simply cannot authenticate any
more. Deleting it would silently rewrite the record of who did what, which is the
one thing the history exists to prevent (VISION 7).

**The grace period is one instance-wide environment variable**, like the claim
deadline of VISION 11, and for the same reason: it is an operational dial, not a
per-project preference.
