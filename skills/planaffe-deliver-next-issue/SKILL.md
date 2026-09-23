---
name: planaffe-deliver-next-issue
description: Deliver exactly one planaffe issue selected by pa next --claim as one goal, including implementation, validation, commit, push and verified merge. Use when the user asks for the next workable issue, whether or not it belongs to an epic; do not use to deliver a whole epic or drain an issue queue.
---

# Deliver the next issue

Select and deliver exactly one issue. An explicit request to use this workflow
authorizes its commit, push and merge under repository rules. Automatic skill
discovery during discussion does not authorize publication. Preserve narrower
user limits and required human approvals. Never force-push, bypass protections
or disable checks.

## Select once

Before any other `pa` command, run `pa me`. Continue only for an agent identity;
otherwise report the mismatch and stop. Resolve the project from the working
repository or user request; ask if neither supplies it. Pass `--project PROJECT`
explicitly on subsequent commands. Read `pa project view PROJECT --json`,
repository instructions, relevant architecture decisions, git status, branch
and remotes. Preserve unrelated work and the current checkout's repository
filter. If no suitable checkout or repository scope is known, resolve that
before claiming.

Check the harness's active goal first. If it already names one issue from this
workflow, resume that issue and its existing branch or pull or merge request;
do not select another. Do not silently replace an unrelated active goal. If the
harness has no goal facility, report that the requested one-goal workflow is
unavailable and stop before claiming.

For a new run, call `pa next --claim --project PROJECT --json` exactly once,
with the current repository filter and no epic or extra ready filter. The
server selects and claims the highest-ranked *workable* issue atomically:
priority first, then an epic nobody else is working in, then age. Workable
means `todo`, unclaimed, permitted by project triage, and free of questions,
open blockers and other gates. A ticket may have an epic or none. Do not pick
the oldest row from `pa issue list`, claim a different issue by key, or pull
another issue after finishing this one. Exit 8 means no issue is workable in
scope; report the returned reasons and stop without creating an empty goal.
`pa` itself repeats a claim whose connection dropped, with the same key, and
the instance answers that repetition with the issue it already claimed. If
`next --claim` still ends with exit 10, a new invocation is a new request:
inspect your claims before calling it again rather than risk a second issue.

Read `pa issue view ISSUE --json` before editing. Announce the selected issue
and its completion condition. Create one goal with `create_goal`; its objective
names only this issue's acceptance criteria, appropriate checks, its commit,
push, actual merge and required acceptance. Set a budget only when the user
specified one. If goal creation fails after claiming, record the state and
release your claim before stopping. Never replace an unrelated goal.
If the issue context cannot be read, release your claim and stop as well.

## Implement this issue

Use an existing suitable branch or a short-lived branch targeting the normal
integration branch. Isolate unrelated working-tree changes. Read the issue's
full context, including its epic description when present, but do not expand
the goal to other tickets in that epic. Check for already committed work before
repeating it. Renew a long-running claim with `pa issue claim ISSUE`; if the
claim is lost, stop writing and reconcile ownership. Never take over somebody
else's claim.

Implement the acceptance criteria, run appropriate checks and make one focused
commit for this issue. Do not create an empty commit or include unrelated
changes. Record validation and the commit in the issue result or a non-blocking
comment. Keep internal issue keys and contents out of public branch names,
commits and request descriptions unless the user explicitly asks to publish
them.

If work needs a human decision, use `pa issue ask ISSUE "Question"`; a comment
does not block selection. Do not answer your own question. Record progress and
release your own claim when leaving unfinished work. Stop with this issue;
do not claim a replacement. Use the installed CLI's help for uncertain flags.

Respect the project's definition of delivered. If implementation and checks
already satisfy it, use `pa issue close ISSUE --done --result-file -`. If done
requires merge, record progress and use `pa issue review ISSUE --result-file -`
for handoff, or maintain the claim while actively delivering it. Do not mark a
local commit done. Required human review remains a human decision.

## Publish and finish

Run final checks and inspect the staged diff and every commit to be pushed for
secrets and private information. Push only this issue's authorized work.
Create or update its pull or merge request against the correct target, with a
public description of behavior and validation. Follow CI, fix failures caused
by this change and address actionable review feedback within this issue's
scope. Wait for required approvals without bypassing them.

Merge with the repository's supported method when permitted. If auto-merge is
enabled, continue until the host reports an actual merge. Verify the merge
commit is reachable from the target branch. If credentials, approvals or an
external failure prevent delivery, preserve the work and report the exact
remaining action without repeatedly retrying a denied operation.

After merge, reconcile the issue result and close it under project rules.
Complete the goal only when this one issue is delivered and accepted and its
work is merged. Report the request URL, merge commit, validation and final
issue state, or the precise outstanding work. Do not claim a second issue,
publish a release or deploy unless separately requested.
