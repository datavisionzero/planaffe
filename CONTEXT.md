# planaffe Domain Language

This glossary defines the canonical product language of planaffe. It says what
each term **is** and which competing words are not used, without prescribing an
implementation.

[`VISION.md`](VISION.md) holds the reasoning, [`docs/adr/`](docs/adr/) the
decisions behind the contested ones, and [`docs/codebase.md`](docs/codebase.md)
where each concept lives in the code. A term appears here once the decision
behind it has been made; the roadmap's vocabulary does not.

**The entry heading is the canonical name; running prose writes it in ordinary
case.** Code, identifiers, the HTTP contract and the CLI use the canonical name
without exception — that is what this file is for. Prose may keep an `_Avoid_`
word where it reads better and the entry says so.

## The instance and who acts in it

**Instance**:
One deployed planaffe: the application, its Postgres, and everything both of them
hold. Deadlines and grace periods are set per instance, never per project.
_Avoid_: installation, deployment, server, tenant, node

**Identity**:
Whoever acts. Every record of who did something — the claim, the author of an
issue or comment, every history entry — points at exactly one, and an identity is
either a **User** or an **Agent**.
_Avoid_: actor, principal, account, subject

**User**:
A human identity, authenticated by a session in the browser or by a **User
Token** at the console, holding access to a set of projects and possibly the
administrator role.
_Avoid_: person, member, account, human

**Agent**:
An AI identity that reaches the instance through the API under exactly one
**Agent Token**, and is owned by the user who created it. What an agent *is* — a
window, an installation, a harness — is deliberately not fixed; what it reports
about itself is kept as history rather than as truth.
_Avoid_: bot, assistant, client, integration, machine user

**Agent Token**:
The credential of an agent **and** its record: it carries the agent's name and
the metadata the agent reports about itself, is created by a human, shown once,
and revoked rather than deleted, so that everything it ever signed keeps its
author.
_Avoid_: API key, secret, service account, credential

**User Token**:
A user's key to the CLI: it authenticates as the user and is no identity of its
own — no name, no metadata, no back channel. Created by the user, shown once,
revoked rather than deleted.
_Avoid_: personal access token, API key, session token, login

**Administrator**:
The role that administers the instance itself — users, projects, and everything
outside a single project's content. Held by users only; an agent token never
carries it, whoever owns it.
_Avoid_: owner, superuser, root, maintainer

## The work

**Project**:
The bracket every piece of content belongs to, carrying the **project key** that
prefixes everything in it. It is not a repository: normally one project is one
repository, it may span several, and planaffe models no repository either way.
_Avoid_: workspace, team, space, repository

**Project file**:
The `.planaffe` file in the root of a repository, checked in, pointing from that
repository at exactly one project by its project key and optionally naming the
`repo` label of this repository. Never the other way round: a project does not
list its repositories.
_Avoid_: config file, manifest, workspace file, dotfile

**Issue**:
The unit of work. Everything else in this section is either a bracket around
issues or something that hangs on one.
_Avoid in code, the contract and the CLI_: ticket, task, story, card, item.
_In prose_ "ticket" is fine and often reads better.

**Sub-issue**:
An issue with a parent, exactly one level deep. It is a full issue — its own key,
status, claim and result — that inherits only the parent's epic, starts with the
parent's priority, and is gated by the parent: not workable while the parent is
parked, closed or blocked, and shipped in the parent's release.
_Avoid_: subtask, child issue, checklist item

**Epic**:
A theme several issues hang under, and a description that is the shared context
for whoever works on them — a living document that whoever works under it keeps
current. A bracket, not a unit of work: no assignee, no priority, no claim, and
a status that gates nothing — closing it leaves its issues workable.
_Avoid_: initiative, theme, feature, parent, milestone

**Release**:
A named version of a project, and a record of what shipped in it rather than a
plan for what should. Closed issues collect in the one open release, which has
no name until publishing names it, freezes it and opens the next; a sub-issue
ships with its parent.
_Avoid_: milestone, version, sprint, iteration

**Label**:
A free tag defined per project, optionally carrying a one-line description of
what it means there, and the only extensibility the product offers.
_Avoid_: tag, category, custom field

**Label group**:
A name several labels share, within which only one applies at a time — setting
another replaces the previous one. This is what replaces an issue type.
_Avoid_: category, dimension, field, enum

**Comment**:
A note on an issue that forces nobody to act. Whoever can go on comments.
_Avoid_: note, remark, discussion, thread

**Question**:
An open state on an issue: what somebody needs to know before the work can go on,
with an answer that closes it. It is not a comment, because "are there open
questions?" is a state rather than a text search. Whoever cannot go on asks;
asking does not release the claim, and the asker may wait for the answer.
_Avoid_: query, inquiry, blocker, clarification, needs-info

**History**:
The record of every change to an issue — who, when, which field, from what to
what — written by the instance, never edited and never deleted.
_Avoid_: audit log, activity, timeline, changelog

## Keys

**Project key**:
The short prefix identifying a project and beginning every key inside it, e.g.
`PLAN`.
_Avoid_: slug, code, prefix, identifier

**Issue key**:
The project-scoped identifier of an issue, e.g. `PLAN-42`. Issues and sub-issues
draw from one sequence per project, and a key is never reused — not after a
deletion either.
_Avoid_: id, number, ticket number, reference

**Epic key**:
The project-scoped identifier of an epic, e.g. `PLAN-E3`, drawn from a sequence
of its own.
_Avoid_: id, number, reference

## States and acts

**Status**:
The one fixed set an issue moves through: `backlog`, `todo`, `in_progress`,
`review`, `done`, `canceled`. It is not configurable and has no variants.
`backlog` and `todo` answer *when* (parked, or due); an issue is born in `todo`.
_Avoid_: state, workflow state, stage, column

**Review**:
The status between delivered and accepted: the claim is released, the result is
written, and the issue waits for a human — neither workable nor claimed. It
leaves to `done`, to `canceled`, or back to `todo` with a comment. Every close by
an agent — `canceled` included — lands here where **review required** is on;
handing in explicitly works whatever the switch says.
_Avoid_: QA, verification, acceptance, pending, awaiting approval

**Done**:
The work the issue asked for is delivered the way the project delivers — its
convention, not planaffe's, which checks nothing. Where review is required,
`done` is a human's word; otherwise it is the agent's.
_Avoid_: completed, finished, merged, shipped, resolved

**Closed**:
Derived, not a status: an issue whose status is `done` or `canceled`. Everything
else is open.
_Avoid_: resolved, completed, finished, archived

**Ready**:
The field, and a statement about the issue rather than a permission: it is
concrete enough that somebody can implement it without asking first. Whoever
writes the issue sets it — unless **triage required** is on, when an agent may
clear it and only a user may set it.
_Avoid_: approved, groomed, refined, triaged

**Workable**:
Derived: an issue **next** may hand out, which requires all of the conditions in
`VISION.md` 10 at once — `ready` being only one of them, and only where triage is
required.
_Avoid_: ready (that is the field), available, eligible, actionable

**Triage required**:
The project switch that makes `ready` binding for **workable** and a user's word:
on, an agent may clear the flag and never set it. Off by default, because a solo
developer who trusts whoever writes the issues should not have to flag them. It
guards the entrance; **review required** guards the exit, with the same
asymmetry — an agent hands in, a human accepts.
_Avoid_: review, approval, gate, moderation

**Review required**:
The project switch that makes every close by an agent — `canceled` included —
land in **review** instead of `done`. Off by default, for the same reason as triage required: whoever trusts
their agents should not have to accept every issue by hand. A user's close goes
to `done` either way.
_Avoid_: approval, sign-off, QA gate, acceptance

**Claim**:
The exclusive hold one identity takes on an issue, won atomically by exactly one
claimant, expiring by itself after its holder's inactivity when an agent holds it
and never when a user does, and released by handing the issue over — into review
or to a close — or by letting go, which lands in `todo`. Taken directly on any
open unclaimed issue except one in review, held several at a time by one
identity, and taken over a user's head only by a user. It says who is working
**now**.
_Avoid_: lock, reservation, assignment, checkout

**Assignee**:
Who should be responsible for an issue — set by hand, persistent, and present
even when nobody is working. It says who it **belongs to**, which is not what a
claim says. Most issues have none, and that is the normal case.
_Avoid_: owner, responsible, claimant

**Blocker**:
An issue another issue waits for. The relationship is directed and read from both
ends (`blocks`, `blocked_by`), it may cross projects, and it dissolves on its own
when the blocker closes.
_Avoid_: dependency, prerequisite, relation, link

**Description**:
The assignment on an issue: what is to be done, as Markdown.
_Avoid_: body, content, details, summary

**Result**:
What was actually done, filled in when the issue closes — or, on `canceled`, why
it will not be. Expected but never enforced.
_Avoid_: resolution, outcome, closing comment, summary

**Deleted**:
An issue removed by somebody but kept out of sight rather than destroyed:
invisible everywhere, restorable for a grace period, and only afterwards gone.
Distinct from `canceled`, which is a decision that stays visible.
_Avoid_: archived, trashed, hidden, removed

**Next**:
The act at the centre of the product: hand out the highest-ranked workable issue
and claim it, as one operation that cannot be split. Priority first, then an epic
nobody is working in, then the older issue.
_Avoid_: pull, fetch, assign, dequeue, take

**Needs you**:
Derived: the list of what only a human can resolve — open questions, issues in
review, issues without `ready` where triage is required, and issues whose chain
of blockers ends in something no agent can pull. A list of the API, not only a
screen, and the one a human waits on.
_Avoid_: inbox, attention, alerts, triage list, work queue
