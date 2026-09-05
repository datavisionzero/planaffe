# Triage Required Selects, It Does Not Permit

*Triage required* decides what `next` hands out and nothing else: without
`ready`, nothing is delivered. Who may **write** the flag no longer depends on
the kind of token the caller holds — anybody who may change the issue may set it
and clear it. That `ready` is a human's word survives as a convention, worded
like the one about answering questions: a human says it, and an agent says it
when told to. This amends [ADR 0015](./0015-a-token-is-an-agent-or-a-users-key-and-an-agent-is-never-an-administrator.md)
and the entrance half of [ADR 0014](./0014-review-is-a-status-and-a-project-switch.md),
and VISION 10 is rewritten with it.

## Why the rule was dropped

**It contradicted the paragraph above it.** VISION 10 says triage happens in the
chat, not in the interface: whoever sees that an issue is stuck tells their
agent to answer the question and rewrite the ticket. "Tidy these seven up and
release the three that are clear" is the same sentence, and it is exactly what
the server refused. The one road the vision names for triage was the one road
closed.

**The same question was already answered the other way.** Answering a question
is at least as much a human's word as `ready` is, and there the vision solves it
with a convention — "a question is answered by a human; an agent answers it only
when told to" — precisely because the instructing human stands behind the token.
Two identical questions, two different mechanisms, and no argument for the
difference.

**It drew the line at the token, not at the act.** The rule bound the *kind* of
identity, so the same person doing the same thing under their user token was
allowed and under their agent token was not. Anybody who found that out worked
around it by picking a token, which makes it a rule that costs more than it
protects — and one that quietly teaches people to run agents under user tokens,
which is the identity blur ADR 0015 exists to prevent.

## Why review required keeps its asymmetry

The mirror is not exact, and the difference is what makes one worth keeping. At
the **exit**, an agent's close is a claim about work that is already done and
that nobody else is going to look at; `review` is the only moment a human sees
it, and `done` feeds the release. At the **entrance**, `ready` is a statement
about a ticket that is still sitting there — wrong, it costs one agent one look
and a cleared flag, and the agent that finds it thin clears it itself. So the
exit keeps asking whose word it is, and the entrance stops.

## Consequences

- **`Issue.ReadyMayBeSetBy` and `RefusalCode.ReadyRequiresUser` are gone**, with
  the checks in `CreateIssues` and `ChangeIssue` and the `ready-requires-user`
  problem type. No client has to handle a `403` on the flag any more.
- **The switch keeps its whole effect on `next`**: condition 6 of VISION 10 is
  unchanged, and a project with triage required still delivers nothing unflagged.
- **The history is the answer to "who said so"**, as it already was: every write
  of `ready` records the actor, and an agent's is told apart from a user's there.
- **A project that wants the old guarantee has none.** That is the trade: what
  is left is a convention plus a record, and where a human's flag has to be
  provable, the record is where it is proved.
