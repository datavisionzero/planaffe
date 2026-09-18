---
name: planaffe-plan-epic
description: Turn an existing planaffe epic into implementable issues, asking the user only for necessary decisions. Use for breaking down an epic; without an epic key, select the oldest open epic with no issues. Does not implement the issues.
---

# Plan an epic

Produce a coherent set of issues that covers the epic's outcome, with explicit
acceptance criteria and blockers. Work in the user's current project unless
they name another one.

## Establish the context

Before any other `pa` command, run `pa me`. Continue only if it identifies an
agent; otherwise report the identity mismatch and stop. Do not switch to a
user's credentials. Resolve the project from the working repository or the
user's request. If neither provides one, ask; never fall through to an
instance-wide selection. Pass `--project PROJECT` explicitly on subsequent
commands. Read `pa project view PROJECT --json`, repository instructions and
the relevant code and architecture decisions. Use the installed CLI's `--help`
when a command or flag is uncertain.

With an epic key, read `pa epic view EPIC --json` and verify its project. If it
is closed, ask before reopening or extending it. Without a key:

1. Read every page of `pa epic list --status open --limit 200 --json`.
   Follow `has_more` and `next_cursor` with `--cursor`; the list is newest first.
2. Keep epics with `progress.total == 0`, including no done or canceled issues.
   Choose the oldest `created_at`, breaking ties by the numeric part of the
   epic key. Deleted issues are absent from normal counts.
3. Read that epic and announce the selection. If none qualifies, report that
   there is no empty open epic and stop without creating an epic.

Read every page of `pa issue list --epic EPIC --limit 200 --json`, with no
status filter. For an explicitly selected epic with existing issues, inspect
their full context and results and plan only missing work. Do not duplicate
completed, canceled or already planned scope without a new user decision.

## Resolve and create

Use the epic description as shared context. Inspect the code before proposing
work already implemented. Split the outcome into independently verifiable
issues, usually small enough for one focused commit. Each description states
the intended behavior, scope, acceptance criteria and useful validation.
Represent actual sequencing with `blocked_by`, not just prose or priority.
Use existing project labels and priority conventions; default to priority 2
when no convention or urgency says otherwise.

Ask only questions that materially affect scope, behavior or acceptance and
cannot be answered from existing evidence. Offer concrete alternatives with a
recommendation and consequences, while allowing a free-form answer. Continue
independent planning while waiting, but never interpret silence as a decision.
Once clear, create the issues without a separate approval ceremony.

Prefer `pa issue create --file FILE` for a related set: it creates issues and
their blocker edges in one transaction. The JSON body has an `issues` array;
each item can contain `ref`, `title`, `description`, `epic`, `priority`, `ready`,
`labels` and `blocked_by`. Blockers can use another item's `ref` or an existing
issue key. Single issues can use:

```sh
pa issue create "Title" --epic EPIC --description-file - --priority 2 --ready
```

Preserve the current repository's label where appropriate. For an epic spanning
repositories, assign each issue to the right existing repo label rather than
blindly inheriting the current one; `--repo none` disables implicit addition.

Every created issue is either implementable and `ready`, or has a blocking
human decision and immediately receives `pa issue ask ISSUE "Question"`.
Leaving `ready` false alone does not block selection when triage is disabled.
If an essential answer is pending, defer creation of the affected issue or
create it with the question; do not invent the answer. Do not claim issues
merely to plan them.

Immediately before writing, reread the epic and its issues to detect concurrent
planning. Epics have no claim, so this is not an atomic planning lock. If
another planner is active, coordinate with the user instead of creating a
competing breakdown. After an uncertain write, inspect the server before
retrying; never replay a batch blindly.

Keep agreed shared decisions in the epic description where needed, preserving
existing content with `pa epic edit EPIC --description-file - --if-match VERSION`
using the last read `updated_at`. On a stale response, reread and reconcile.
Keep temporary plans and internal issue content out of tracked files unless
publication was requested.

Finish by verifying the created issues, epic membership and blockers. Summarize
the issue keys, implementation order and outstanding questions. Do not implement,
commit, push, merge or close the epic as part of planning.
