# Review Is a Status, and Whether a Close Lands There Is a Project Switch

The status set gains `review`, between `in_progress` and `done`: the agent has
delivered, the claim is released, the `result` is written, and the issue waits
for a human — neither workable nor claimed. Whether an agent's close lands in
`review` or straight in `done` is a project switch, *review required*, off by
default. A user's close goes to `done` either way.

## Why the settled set was reopened

The research settled the status set as Linear's default, and the vision said so
(VISION 9). What it did not say is what `done` means when an agent sets it. An
agent closes its own ticket; nobody looks; the release fills itself from `done`
and calls itself "a record that is always true" (VISION 7). That is only true if
the agent's word is.

The sharper problem is that a ticket which is finished but not yet looked at had
nowhere to be. Keep the claim while the pull request waits, and the claim expires
after four hours (VISION 11) — the ticket falls back to `todo` and the next agent
does the work again. Release the claim, and the same happens at once. Close it,
and the release records something nobody checked. Reviewing what agents produce
is the largest human task in agentic development, and the one moment of the cycle
the product gave no place.

## Why a status, and not a derived list

The alternative was to keep the set and derive "unreviewed": `done` issues closed
by a token that no human has looked at yet. That needs a "seen" flag through the
back door, leaves `done` untrue in the meantime, and leaves the release untrue
with it. A status is what the product already uses for "this ticket is waiting
for someone" — `review` is to the finished ticket what the open question is to
the stuck one, and it lands in the same list, "needs you".

## Why a switch, and why it is off

Always routing through `review` costs the solo developer one action per ticket,
for agents they may trust entirely. The vision already has the answer to that at
the other end of the cycle: *triage required* (VISION 10) decides whether the
creator's `ready` is trusted, and is off by default because a solo developer
should not have to flag their own tickets. *Review required* is the mirror image
— triage guards the entrance, review guards the exit — and is off for the same
reason. With it on, `done` is a human's word, and the release is a record rather
than a claim.

## Consequences

- **`review` is not workable and not claimed.** Condition 1 of VISION 10 excludes
  it, and `next` never hands it out.
- **Handing into `review` releases the claim** exactly as closing does, and the
  `result` is expected — not enforced — at that moment rather than at `done`.
- **Back from `review` is `todo`, with a comment**, and it is the same movement
  as reopening a closed issue: `todo`, no claim, `closed_at` cleared, `result`
  kept until the next close overwrites it. The comment is expected the way the
  `result` is — pointed out when missing, never enforced. A question is the
  wrong tool for the rejection: a question is what whoever cannot go on asks of
  a human, and it parks the issue until a human answers. A human asking one
  here would have the issue wait for a human to answer the human, while the
  agent that did the work is gone.
- **`review` has three exits: `done`, `canceled`, `todo`.** Where the switch is
  on, every close by an agent lands in `review`, `canceled` included — "could
  not be done" is a claim to check as much as "done" is, and the `result`
  carries the reason. Otherwise the switch would guard one door and leave the
  other open.
- **`review` is reachable explicitly whatever the switch says.** The switch
  decides where a *plain* close lands; an agent unsure of its work hands the
  issue over for a look in any project, the same way it clears `ready` on an
  issue it wrote and finds thin.
- **The mirror holds at the entrance.** Under triage required, an agent may
  clear `ready` and never set it — only a user's flag counts. Without that, the
  agent that writes seven issues flags seven issues, and the switch guards
  nothing a human has looked at. The two switches have the same shape: an
  agent hands in, a human accepts.
- **`done` is defined, and it is the project's convention** — merged, pushed,
  tagged, whatever the project calls delivered. planaffe checks none of it; it has
  no repository (VISION 13).
- **The switch is per project**, like triage required, not per instance: whether
  an agent's word is enough is a property of who works in the project.
