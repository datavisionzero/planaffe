# A List Returns a Slim Issue, and Only a Single Issue Is Complete

`issue view` returns the complete issue — every field, the epic description, the
questions with their answers, the comments. **A list does not.** `issue list`,
`next` in its explaining form, search and every other collection return a slim
issue: key, title, status, `ready`, priority, labels, epic, assignee, claim with
its holder and since when, the keys in `blocked_by`, the counts of open
questions, open blockers and open sub-issues, and the timestamps. No
`description`, no `result`, no comment bodies, no history.

Lists are paginated: a default of 50, a maximum of 200, refused above that rather
than silently truncated. Pages are addressed by **cursor, not offset**, and every
response says how many items match in total and whether more follow.

## This narrows a promise the vision made

VISION 6.1 commits to "`--json` prints the complete object, not a selection of
fields. No guessing field names, no second call." Held literally over a list, it
is a context bomb: the same document says tickets written by AI get long (VISION
6.2), and four hundred of them with their Markdown bodies is not an answer an
agent can afford to receive. Context budget is the one hard resource in this
target group, and this is the single place where "always everything" works
against the agent-friendliness the rest of the document is built on.

So the promise is narrowed to what it can actually keep, and VISION 6.1 is
reworded to say so: **the complete object is the promise of the single object.**
A promise that has to be broken in practice is worse than a smaller one that
holds.

**Cursor rather than offset** because agents create issues in parallel here. With
an offset, a page shifts under a reader while another agent inserts rows, and the
reader silently skips or repeats entries. That is an academic objection in most
products and a routine event in this one.

**Not two formats.** `--json` slim beside `--json-full` complete would cover both
cases and hand the agent a choice it has no basis for making — the switch guiding
principle 3 exists to avoid. One shape per endpoint, decided here.

## Consequences

**The slim issue is part of the contract**, appears in `docs/api/openapi.json` as
a type of its own, and both generated clients see two issue shapes. Naming them
apart is the point: a caller holding a list item knows it has no body.

**An agent that wants bodies asks per issue**, which is the correct traffic
pattern — it has filtered down to a handful by then. The CLI does not fan out
behind the caller's back to reassemble a "complete" list.

**Counts carry what the bodies would have.** Whether an issue has an open
question, an open blocker or open children is exactly what the list is read for,
and it is a number rather than a document.

**`next` keeps its explanation.** The empty answer that says "3 blocked, 2 waiting
for an answer, 4 already in progress" (VISION 10) is counts, not content, and is
unaffected.
