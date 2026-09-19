---
name: planaffe-deliver-standalone-issues
description: Implement all currently workable, ready planaffe issues without an epic in one goal, with a commit per implemented issue, then push and follow the work through CI and actual merge. Use for end-to-end delivery of standalone issues, not epic delivery or a read-only overview.
---

# Deliver standalone issues

Drain the ready, workable issues without an epic in the selected project and
repository scope. An explicit request for this workflow authorizes its commits,
push and merge under repository rules. Automatic discovery during discussion
does not authorize publication. Respect narrower user limits and required
human approvals. Never force-push, bypass protections or disable checks.

## Establish the scope and goal

Before any other `pa` command, run `pa me`. Continue only for an agent identity;
otherwise report the mismatch and stop. Resolve the project from the working
repository or the user request; ask if neither supplies it. Pass
`--project PROJECT` explicitly to subsequent commands. Read
`pa project view PROJECT --json`, repository instructions, relevant architecture
decisions and the current git status, branch and remotes. Preserve unrelated
work. Default to the current checkout's repository filter; work across
repositories only when the user requested that scope and the needed checkouts
are available.

Preview with `pa next --ready --epic none --json` in the selected repository
scope. This is the authoritative workable set and order. `--ready` is required
even when project triage is off. `--epic none` must be accepted by the installed
CLI and server; if either rejects it, stop and report the version limitation.
Never claim from an unfiltered `next` call. Read every page of
`pa issue list --epic none --ready --status todo --status in_progress --status review --limit 200 --json`,
following `has_more` and `next_cursor` with `--cursor`. The list reveals claims,
reviews and blockers; its `ready` flag alone does not establish workability.
Unlike `next`, issue list has no repository filter. Interpret its labels under
the project's `repo` label group: an issue without a repo-group label belongs
to the current checkout's scope too.
Do not include issues attached to an epic, parked in backlog, or with
`ready=false`. Check for existing commits and pull or merge requests for this
scope before starting, so a resumed run does not repeat delivered work.
Announce the initial scope and the completion condition. If no issue is
workable and there is no matching delivery to finish, explain what the scoped
list and `next` reasons show and stop without claiming or creating an empty
goal.

Inspect the active goal and call the harness's `create_goal` facility once if
available. Its objective is delivery of the scoped ready standalone queue,
including a focused commit for each implemented issue, appropriate checks,
pushed commits, actual merge and
required issue acceptance. Reuse a matching active goal; never replace an
unrelated active goal silently. Set a budget only if the user specified one.
If no goal facility exists, disclose that limitation and maintain an explicit
progress checklist; do not claim a goal was created. Use an existing suitable
branch or a short-lived branch targeting the normal integration branch.

## Implement the queue

Take one issue at a time with `pa next --claim --ready --epic none --json`,
preserving the repository filter. Read `pa issue view ISSUE --json` before
editing and verify it still has no epic and remains ready. If its scope changed,
reconcile and release your claim without implementing it. Claim only work about
to begin and never take over another holder's claim. Renew a long-running claim
with `pa issue claim ISSUE`; on claim loss, stop writing and reconcile
ownership. Use the installed CLI's help for
uncertain flags.

Implement the issue's acceptance criteria, run appropriate checks and make one
focused commit for that issue's changes. Do not make empty commits or fold
unrelated changes into a ticket. If a ticket needs no code change, record the
reason instead of fabricating a commit. Record validation and the commit in its
result or a non-blocking comment. Keep internal issue keys and contents out of
public branch names, commits and request descriptions unless the user
explicitly asks to publish them.

When work cannot continue without a decision, use `pa issue ask ISSUE
"Question"`. A comment does not block selection. Do not answer your own
question. Continue with other workable standalone issues when possible. If
leaving unfinished work, record its state and use `pa issue release ISSUE` for
your own claim. Never release somebody else's claim.

Use the project's definition of delivered. When implementation and validation
already satisfy it, use `pa issue close ISSUE --done --result-file -`. If done
requires merge, record progress and use `pa issue review ISSUE --result-file -`
for handoff, or maintain the claim while actively delivering it; do not call a
local commit done. A required human review remains a human decision. Review
does not unblock dependent issues. Where a dependency needs acceptance before
the next issue becomes workable, merge the completed slice and obtain the
required acceptance before continuing.

Repeat the scoped `next --claim` until it returns exit 8. Exit 8 means nothing
is currently workable, not that every ready standalone issue is delivered.
Inspect the complete scoped list for questions, claims, review, blockers and
repository labels. Do not clear blockers, broaden scope or cancel issues to
force an empty queue. Stop after a fresh empty scoped preview; do not wait for
new issues to arrive. If human or external action prevents completion, retain
the goal and report the precise outstanding work.

## Publish and verify delivery

Run required final checks. Inspect the staged diff and every commit to be
pushed for secrets and private information. Push only authorized work. Create
or update the matching pull or merge request against the correct target,
describing behavior and validation without exposing internal planning.

Follow CI, fix failures caused by this work and address actionable review
feedback in scope. Wait for required approvals. Merge using the repository's
supported method when permitted. If auto-merge is enabled, continue until the
host reports an actual merge. Verify the merge commit is reachable from the
target branch. If credentials, approvals or an external failure stop delivery,
preserve the work and name the remaining action without repeatedly retrying a
denied operation.

After merge, reconcile issue results and close delivered issues under the
project's rules. Recheck the scoped ready standalone list and `next` preview;
distinguish delivered work from issues still blocked, claimed by others or in
review. Complete the goal only when every issue taken in this run is delivered
and accepted, the work is merged, and no scoped ready standalone issue remains
workable. Report the request URL, merge commit, validation and any outstanding
issues. Do not publish a release or deploy unless separately requested.
