---
name: planaffe-answer-questions
description: Walk the user through all open planaffe questions in the current project, one issue at a time, with recommended answer options, and record the user's decisions. Does not implement issues or answer on the user's behalf without their decision.
---

# Answer questions together

Help the user clear open questions across all epics and issues of the current
project. The user makes each decision; the agent prepares the options and
records the answer. Invocation authorizes recording the decisions the user
actually provides, not choosing answers for them.

## Build the round

Before any other `pa` command, run `pa me`. Continue only for an agent identity;
otherwise report the mismatch and stop. Never switch to a user's credentials.
Resolve the project from the working repository or explicit request. If it is
missing, ask rather than listing the whole instance. Read the project's
instructions with `pa project view PROJECT --json`, and pass
`--project PROJECT` explicitly on subsequent commands. Cross-project rounds
require an explicit request and a resolved list of projects.

Use `pa question list --limit 200 --json`. It returns open questions, oldest
first. Fetch all pages using `has_more`, `next_cursor` and `--cursor` before
answering anything, so writes do not shift the page being traversed. Do not use
`--answered` or `--all` for the queue. Do not substitute `needs-you`, which also
contains reviews and other work. Do not restrict by epic, readiness, claim,
issue status or repository label.

Group by issue in order of its oldest open question and keep the question IDs.
If the queue is empty, say so and finish. Tell the user how many issues and
questions the round covers. New questions discovered on a final refresh can
extend the round; skipped IDs stay skipped for this invocation.

## Discuss one issue at a time

Read `pa issue view ISSUE --json` for the full assignment, project instructions,
epic context and blocker results. Read relevant code or documents when they
help establish the options. Refresh `pa question list --issue ISSUE --json`
with pagination before presenting its open questions.

For each decision:

1. Name the issue and explain the question and why it blocks progress in a few
   sentences. Separate established facts from assumptions.
2. Offer two or three meaningful options when alternatives exist. Put the
   recommended option first and explain the practical tradeoffs. Do not invent
   options for a question that needs a specific fact; ask for that fact instead.
3. Let the user select an option, give a free-form answer, skip or stop. Use the
   harness's interactive question tool when available, otherwise ordinary chat.
   Wait for an actual answer: a default selection, timeout or silence is not one.
4. If the response leaves a material ambiguity, ask a focused follow-up. If it
   is unambiguous, record it without asking the user to confirm it again.

A skipped question remains open; continue with the next one without repeatedly
presenting it. On stop, leave the rest open and summarize progress. If one user
response covers multiple question IDs, record it for each only when its
applicability is explicit. Do not extend a decision beyond the stated scope.

## Record and continue

Recheck that the question is still open immediately before writing. If someone
else answered it, report that and move on; do not overwrite their answer. Save
the user's decision as self-contained text, preserving conditions and reasons:

```sh
pa question answer QUESTION_ID --file -
```

Supply the answer on stdin. Use the question UUID, not the issue key. Do not
save an option letter, your recommendation alone or a claim that the user said
something they did not say. Keep the answer in the project's language. The
agent identity remains the author; mention that it records the user's decision
where useful without copying personal information unnecessarily.

Check the command result. On a conflict or uncertain response, reread the
questions before retrying. Verify the saved answer with
`pa question list --issue ISSUE --all --json`, following pagination, then
briefly acknowledge it and proceed. A failed write is not an answered question.

Answering can make an issue workable for other agents immediately, but it does
not necessarily remove all blockers. This skill does not claim, implement,
close, review, change readiness or rewrite issues. If the user requests a
description change as well, apply only that requested change using the current
version guard. Do not create commits or publish internal questions.

Finish with counts of answered, skipped, externally answered and still-open
questions, and identify any failed saves. A later invocation reads the current
server state rather than replaying an old queue.
