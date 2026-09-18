---
name: planaffe-project-overview
description: Give a quick, read-only overview of the current planaffe project's unfinished work, open epics, questions, workable issues and partially finished epics, with a recommendation for what to tackle next. Does not change or implement anything.
---

# Overview of the current project

Help the user see what remains, what needs their attention and what could move
next. Return a concise snapshot in the conversation, not a planning session or
an implementation run.

## Resolve the project

Before any other `pa` command, run `pa me`. Continue only for an agent identity;
otherwise report the mismatch and stop. Never switch credentials. Resolve the
project from the working repository or explicit request. If neither identifies
one, ask instead of listing the whole instance. Pass `--project PROJECT`
explicitly on subsequent commands and read `pa project view PROJECT --json`
for the project instructions and switches.

Cover the whole project, across epics and repository labels. In particular,
use `--repo none` on `next` to override the current repository's implicit label.
Use the installed CLI's `--help` if commands or flags differ. Do not use the
instance-wide `pa standing` as a substitute for a project-scoped overview.

## Read the snapshot

Use these read-only sources with the resolved `--project PROJECT`:

```sh
pa epic list --status open --limit 200 --json
pa issue list --status backlog --status todo --status in_progress --status review --limit 200 --json
pa question list --limit 200 --json
pa needs-you --limit 200 --json
pa next --repo none --json
```

For paginated lists, follow `has_more` and `next_cursor` with `--cursor` until
complete. `next` is a preview without `--claim`; it has no pagination flags.
Do not add `--ready`: that would hide workable issues when triage is disabled.
An empty preview means no workable issues; a failed read does not mean zero.
If any source is unavailable or incomplete, identify the missing information
and qualify the counts and recommendation.

Use slim issue rows and epic progress for counts. Read `pa epic view EPIC
--json` when needed to explain an epic's purpose, and `pa issue view ISSUE
--json` only for the few issues whose context matters to the recommendation.
Avoid fetching every issue's description, comments and history.

Interpret the evidence carefully:

- Open issues are `backlog`, `todo`, `in_progress` and `review`. Include issues
  without an epic and open issues in closed epics; an epic's status does not
  close its issues or gate workability. Count each issue key once, including
  sub-issues without adding their parent's sub-issue counts again.
- Use the server's `next` preview for what this agent could take now, and its
  order for the default implementation recommendation. `ready`, `todo` or the
  absence of questions alone does not establish workability.
- Count open questions separately from the distinct issues carrying them.
  Group the most relevant questions by issue and summarize what decision is
  missing. Use `needs-you` to distinguish reviews, triage and stuck blocker
  chains. These categories may overlap; do not add them into an issue total.
- Distinguish empty open epics, epics with work remaining and some issues done,
  epics with work in progress or review, and open epics whose issues are all
  closed. Keep done and canceled counts separate: canceled is not delivered.
  All issues closed suggests checking whether the epic can close, not proof
  that its intended outcome was achieved.
- A parked backlog or a large issue count is not itself poor maintenance.
  Do not invent a standing or call work stale solely because it is old.

## Give the overview and stop

Lead with the number of open epics and open issues, how much is workable now,
and how many open questions affect how many issues. Give one short line per
open epic with its key, purpose, progress and main remaining obstacle or next
step. For a large project, show the most relevant epics and explicitly count
those omitted so the summary stays quick to read.

Briefly highlight human decisions, reviews, partly finished epics worth
finishing, empty epics needing a breakdown, and issues outside open epics when
present. Recommend one concrete next step with its key and a short reason.
Prefer resolving an obstacle that unlocks useful work or finishing a partly
delivered epic when the evidence supports it; otherwise use the first workable
issue. Explain if this differs from `next` order. Respect existing claims and
priorities. If nothing is workable, name the actual obstacle and who can
resolve it. If nothing remains, say so without inventing follow-up work.

Keep recommendations separate from facts and do not infer implementation or
merge completion from tracker status alone. This is a snapshot, not an atomic
report; reconcile materially inconsistent reads before making a strong claim.

Stop after reporting. Do not claim, create, edit, answer, comment on, close or
release anything; do not start another skill, implement, commit, push or merge.
Keep project content in the conversation, out of tracked files and public
issues. A suggested next step is not authorization to execute it.
