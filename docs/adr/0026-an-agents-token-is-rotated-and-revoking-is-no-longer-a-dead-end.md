# An Agent's Token Is Rotated, and Revoking Is No Longer a Dead End

An agent's token can be replaced. The owner or an administrator asks for the
next one; the one the agent was holding is revoked in the same transaction, and
the new secret is shown once, the way the first one was. An agent therefore has
exactly **one token that works**, where it used to have exactly one token: the
partial unique index `token_agent` counts only the rows that are not revoked.

This narrows one sentence of `docs/storage.md` — "there is no act that adds a
token to an agent" — and nothing else. Everything
[ADR 0015](./0015-a-token-is-an-agent-or-a-users-key-and-an-agent-is-never-an-administrator.md)
decides about who may hold what stands, and so does
[ADR 0013](./0013-deleting-is-a-soft-delete-with-a-floor-and-identities-are-never-deleted.md):
no row is deleted here, and the agent keeps every token it ever had.

## Why a token has to be replaceable

An agent's secret lives in an environment file on a machine, is pasted into a
harness, and is read by whatever the harness starts. It is the credential of
this product most likely to end up somewhere it should not be, and it was the
one thing about an agent that could not be changed. Without this act there were
two ways out, and both are worse than the problem.

- **Revoke the agent and create a second one.** Names are unique across users
  and agents and a revoked identity keeps its name for good, so the replacement
  cannot be called what the first one was called. Every environment file, every
  filter saved on a name, and every reader of the history now deals with
  `quiet-otter-42` and `quiet-otter-43` being the same worker on two sides of an
  incident.
- **Leave the secret in circulation.** Which is what actually happens when the
  alternative is renaming an identity, and it is the failure this decision
  exists to prevent.

## Why the row is not overwritten

The cheap implementation writes the new `secret_hash` and `prefix` into the row
that is already there, and leaves the index exactly as it was. It also erases
the only record that anything happened: `created_at` would go on claiming the
agent's credential dates from the day the agent was created, and nothing at all
would say when the secret before it stopped working.

ADR 0013 keeps revoked tokens for a reason of the same family, and rotation is
where that reason is strongest — a rotation is usually the answer to something
having gone wrong, and the date it happened is exactly the fact somebody wants
afterwards. So the old row is revoked and the new one is added beside it.

What the index guarantees moves from "one row" to "one working row", which is
what the product always meant by it. Authentication looks a secret up and
refuses a revoked one; two rows of which one is revoked admit exactly one agent.

## Why it is a user's act

VISION 12: an agent that can issue itself a second token has escaped its own
identity. Rotating is issuing, so it belongs to the owner or an administrator,
and the check is the one rename and revoke already make. An agent that calls it
is told `forbidden`, like every other act on an identity.

## Consequences

- **Revoking is reversible — the agent, not the revocation.** The token that
  was revoked stays dead; the agent is given another one and works again under
  its own name, with everything it ever wrote still its own. That is what makes
  revoking usable as "stop this agent now" instead of "end this identity
  forever", which is how it read when it was the last thing that could happen to
  an agent.
- **It is no way around a deactivated owner.** An agent authenticates only while
  the user it belongs to is active, and that is read on every request rather than
  written into the token. Rotating the token of a deactivated user's agent hands
  out a secret that fails like the one before it.
- **`GET /agents` carries the agent's current token**: the working one where
  there is one, and the last one that worked where there is not, so a revoked
  agent still says when it was retired instead of losing its row in the list.
- **Both interfaces have it.** `pa agent rotate AGENT` prints the secret once and
  says on stderr that the one it replaced is gone; the row's menu under Personal
  settings · Agents asks first, and the question says that a run still holding
  the old secret fails on its next request.
