---
name: planaffe-deliver-epic
description: Implement a planaffe epic as one goal, preferably committing each issue, then push, open a pull or merge request, and follow it through CI and actual merge. Use when the user asks to deliver an epic end to end, not just plan it or inspect its status.
---

# Deliver an epic

Carry one epic through implementation and verified merge. An explicit request
to use this workflow includes commits, pushing the epic's work and merging it
under the repository's rules. Automatic discovery during discussion does not
authorize publication. Preserve narrower user limits and required human
approvals. Never force-push, bypass protections or turn off required checks.

## Select the scope and goal

Before any other `pa` command, run `pa me`. Continue only for an agent identity;
otherwise report the mismatch and stop. Resolve the project from the working
repository or user request; ask if neither supplies it. Use
`--project PROJECT` explicitly for subsequent commands. Read
`pa project view PROJECT --json`, repository instructions, the relevant
architecture decisions and the current git status, branch and remotes.

With an epic key, read `pa epic view EPIC --json` and verify its project.
Without one, read `pa next --json` without claiming, preserve its ordering and
choose the epic of the first workable issue that belongs to an open epic.
Skip issues without an epic and closed epics. The current CLI preview is limited
and has no cursor. If it has `has_more: true` but contains no eligible epic,
report that selection is inconclusive and ask for an epic key; do not claim
unrelated issues to advance the preview or say that no candidate exists.
If an exhaustive preview has no candidate, explain the returned reasons and
stop; do not create or select an empty epic instead.
An explicitly selected closed epic needs a user decision before reopening.

Read all pages of `pa issue list --epic EPIC --limit 200 --json`, with no status
filter. Follow `has_more` and `next_cursor` through `--cursor`. Inspect full
issues as needed, including completed results, reviews and blockers. Announce
the selected epic and the completion condition.

Create one goal through the harness's goal facility if available. Its objective
includes the epic's acceptance criteria, appropriate checks, pushed commits,
actual merge and required issue acceptance. Reuse a matching active goal; never
replace an unrelated active goal silently. Set a budget only when the user
specified one. If no goal facility exists, disclose that limitation and keep an
explicit progress checklist; do not claim that a goal was created.

Use an existing suitable branch or a short-lived branch targeting the
repository's normal integration branch. Preserve unrelated work; isolate this
epic when necessary. Detect existing commits and pull or merge requests before
starting so a resumed run does not repeat delivered work. Epics can span
repositories: identify that early and limit work to authorized checkouts. Never
call the whole epic complete while work in another repository remains.

## Implement one issue at a time

Take the next issue with `pa next --claim --epic EPIC --json`, preserving the
repository filter. Read `pa issue view ISSUE --json` before editing. Claim only
work about to begin; do not take over someone else's claim. Maintain the claim
during long work with `pa issue claim ISSUE`; on claim loss, stop writing and
reconcile ownership. Use the installed CLI's help for uncertain flags.

Implement the acceptance criteria, run appropriate checks and preferably make
one focused commit per issue. Do not create empty commits or include unrelated
changes to satisfy that preference. Record validation and the commit in the
issue result or a non-blocking comment. Internal issue keys and content stay
out of public branch names, commits and request descriptions unless the user
explicitly wants them published.

When blocked on a human decision, use `pa issue ask ISSUE "Question"`. A comment
does not block selection. Do not answer your own question without the user's
decision. Continue other workable issues within the epic where possible. If
leaving unfinished work, record its state and release its claim with
`pa issue release ISSUE`. Never release a claim owned by someone else.

Respect the project's definition of delivered. If implementation and validation
satisfy it, use `pa issue close ISSUE --done --result-file -`. Where review is
required, this lands in `review` and requires a human; never impersonate one.
If delivery requires merge, do not mark a local commit done. Record progress
and use `pa issue review ISSUE --result-file -` for handoff until merge, or keep
the claim while actively working toward delivery. Review is not done and does
not unblock dependent issues. If this prevents further progress, merge a
completed slice and obtain required acceptance before continuing; explain why
the preferred single final push must give way to the project's delivery rule.

Exit 8 from `next` means no issue is workable, not that the epic is finished.
Inspect the complete epic membership: open questions, review, claims, parked
issues, repo labels and external blockers can all explain an empty selection.
Do not remove blockers, broaden repository scope or cancel work to force
completion. Report a required human or external action when progress needs it.

## Publish and verify delivery

After the implementation is ready, run the required final checks, inspect the
staged diff and all commits to be pushed for secrets and private information,
and push only the authorized work. Create or update the matching pull or merge
request against the correct remote and target, describing final behavior and
validation without exposing internal planning.

Follow CI, resolve failures caused by this change and address actionable review
feedback within scope. Wait for required approvals without bypassing them.
Merge with the repository's supported method when permitted. If auto-merge is
used, continue until the host reports the request actually merged. Confirm the
merge commit is reachable from the target branch. Do not report merely pushed,
request opened or auto-merge enabled as completion. If credentials, required
approvals or an external failure prevent delivery, preserve the work and state
the exact remaining action; do not retry a denied operation indefinitely.

After merge, reconcile issue results and close delivered issues according to
the project rules. Refresh the epic and verify every intended acceptance
criterion, including any issues added during the run. Respect canceled scope
as an explicit decision, never as proof it was implemented. Only close the epic
with `pa epic close EPIC` once all required work is delivered and accepted; do
not use `--cancel-open` or `--park-open` to hide unfinished work.

Complete the goal only when that completion condition holds. Report the request
URL, merge commit, validation and final epic state, or the specific outstanding
work. Do not publish a release or deploy unless separately requested.
