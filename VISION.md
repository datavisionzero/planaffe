# Product Vision

> **Status:** The hierarchy (7.), the field set (8.) and the workflow (9.) are settled after research — evidence in [`docs/research/hierarchy-and-fields.md`](docs/research/hierarchy-and-fields.md). How an agent learns that it is its turn (15.9) is settled after research — evidence in [`docs/research/waking-agents-and-triggers.md`](docs/research/waking-agents-and-triggers.md). What is still open is in section 17.

## 1. Elevator Pitch

A lean, self-hosted issue tracker for software development in the agentic age.
Open source (MIT), started in minutes with `docker compose up`, usable through a fast web interface for humans **and** a complete CLI for AI agents.

We are deliberately **not** building a do-everything machine, but an **opinionated** product: few, well-chosen fields, one simple workflow, no configuration orgy. Whoever uses it adapts to the product — and gets something in return that is understood in five minutes, by human and agent alike.

## 2. Problem

Existing issue trackers are built for a world in which humans write, plan, move and work off tickets:

- **Too hard to host.** Self-hosting often means a dozen containers, a message broker, object storage, a search index and the operational knowledge to run all of it.
- **Too much ceremony.** Status transitions with mandatory-field dialogs, sprint planning, story points, workflow designers, custom fields — overhead that creates no value whatsoever for a solo developer working with agents.
- **Not built for agents.** APIs exist, but they are an afterthought: no CLI that was thought through, no clear semantics for an agent taking a ticket exclusively, no concept of "this ticket is ready for an agent".
- **Too broad.** Helpdesk, product management, roadmapping, portfolio levels — a feature surface a two-person development team never touches but has to click past every day.

### 2.1 Why not GitHub Issues, GitLab, or Markdown in the repository?

These three do not meet the criticism above: `gh` is free, mature and already installed for our target group, GitLab can be self-hosted, and Markdown tickets in the repository do not even need a server. The answer comes in four parts:

**One place for tickets across all projects, wherever the code lives.** The real starting point is scattered: one public repository on GitHub, two private ones on GitLab, the next one somewhere else. Whoever tracks tickets at the git host switches issue tracker with every project, sets up labels and conventions from scratch each time — and has to explain to every `AGENTS.md` all over again how tickets work *here*. No host can provide that bracket; it only knows its own repositories. The same goes for blockers that cross repository boundaries: a repository is not a project.

**What is self-hosted and MIT-licensed stays free.** No plan change pushes a feature behind a paywall, no head count makes the instance read-only. That is not a theoretical risk: on GitLab, epics are Premium — even self-hosted — a private namespace on GitLab.com becomes read-only from the sixth user, and newly created free accounts are limited to three top-level groups. Exactly the order of magnitude our target group works in. And whoever keeps their issue tracker at the host loses it when they move: the host has become interchangeable, the issues do not travel.

**In a public repository, every issue is public.** Half-finished ideas, security topics, abandoned rewrites, roadmap fragments that are read as promises the moment they appear — plus AI-written tickets, which get long (6.2) and are meant to be a working level, not a shop window. The usual workaround is a second, private repository just for issues. That is precisely the one a dedicated issue tracker makes unnecessary.

**The agent cycle cannot be built anywhere else.** An assignee is not an atomic take-over: two agents can assign themselves the same issue, nobody wins exactly once, and nothing expires when a run crashes. "Ready for an agent" exists only as a re-created label, a question back only as one comment among many. `gh issue list` cannot do "give me the next ready issue and claim it" (11.). With Markdown in the repository, the atomic claim is missing entirely — two agents in two worktrees see the same state — and every status change becomes a commit that produces a conflict on the next parallel run, in files nobody wants to merge. On top of that, two things that only become apparent in daily operation: a token an agent uses to drive `gh issue` can almost always push and create releases as well; a planaffe token can create tickets. And GitHub caps write access (80 per minute, 500 per hour) — a Postgres instance on your own network does not.

**What it costs us.** The automatic link to the code is lost: "Fixes #12" closes the issue on GitHub; here, the ticket ID in the branch name and the commit subject is a convention until there is a forge integration. External contributors have no account with us. And one more container runs.

**So we do not replace GitHub Issues, we work next to it:** GitHub Issues stays the public intake, planaffe is the internal working level where things are broken down, claimed and worked off. Where GitHub Issues is enough, use it.

## 3. Target Group

**Primarily:** solo developers and small teams (1–5 people) who want to host their tools themselves and get most of their implementation work done with AI agents.

**Characteristics:** at home on the console. Whoever uses `git`, `gh`/`glab` and Claude Code or comparable agents daily should feel at home here immediately.

**Not the target group (in the MVP):** non-technical stakeholders, support and helpdesk operations, enterprise portfolio management, compliance-driven organisations with granular permission models.

## 4. Guiding Principles

1. **Agent-first.** The AI agent is a first-class user, not a second-class API consumer. Every function a human has in the UI has to be reachable from the CLI.
2. **Easy to host.** Postgres plus the app. Two to three containers, one `docker-compose.yml`, no external dependencies. No object storage, no queue, no separate search index in the MVP.
3. **Opinionated instead of configurable.** One field set, one workflow core. We deliberately refuse custom fields and workflow designers. Flexibility comes from labels, not from schema configuration.
4. **Frictionless transitions.** A status change is one command or one click. Never an intermediate dialog with mandatory fields.
5. **Structure only where it works.** Whatever the system itself has to evaluate — filter, count, react to — gets a field of its own. Whatever is only read stays Markdown. A field nobody needs stays in the schema forever; a heading nobody needs is deleted from the template.
6. **Text only.** Issue content is Markdown in the database. No uploads, no images, no user-generated content outside Postgres — in the MVP.
7. **Fast.** The interface puts the essentials at the centre: the issue and the list of open issues. Keyboard-operable, without a loading-spinner performance.
8. **MIT licence.** Fully open source, with no open-core restrictions in the core.

## 5. Non-Goals (Deliberate Boundaries)

- No helpdesk or ticketing for end customers.
- No product management for non-technical users (roadmaps, OKRs, portfolios).
- No sprint or capacity planning in the MVP — the work is done by agents, not by humans in two-week rhythms.
- No custom fields, no configurable workflow engines, no automation rules in the MVP.
- No native mobile app. The web app is responsive and works well on a phone — nothing more.
- No time tracking, no billing, no reports or BI.
- No git hosting. We may integrate with GitHub/GitLab later, but we do not replace them.

## 6. Product Components

### 6.1 CLI (priority 1)

The CLI is the interface for agents and for console-minded humans. It has to be complete:

- Create, read, change, comment on and close issues — one at a time or several related ones in one go.
- Search and filter issues (status, label, assignee, epic, project, full text).
- Create and read epics, assign issues, query progress.
- Create releases, publish them, print their content as Markdown.
- **Claim** issues and release them again.
- Ask questions, retrieve open questions, answer them.
- Output either human-readable or machine-readable (JSON), so that agents can parse deterministically.
- Authentication via API token, usable across projects, with a clearly set "current project".
- Description texts are passed as Markdown via stdin or a file — no forced editor, agent-friendly.

The goal: an agent can carry out its entire working cycle without ever touching the UI — find a suitable issue, claim it, work on it, document the result, close it.

**Cleaning up is not a feature, it is a question of the interface.** A backlog that agents fill grows faster and contains more duplicates and stale entries than one humans type. The answer to that is not a maintenance tool in the product but a CLI good enough that an agent does the maintenance itself: search and filter what the system knows, and change, re-hang or close what it found in one go. Whoever wants duplicates found sends an agent — not us a feature request (guiding principle 3).

**Shape:** `planaffe <object> <verb>`, like `gh` and `glab` — `planaffe issue list`, `issue view PLAN-42`, `issue claim PLAN-42`, `epic view PLAN-E3`. Short alias `pa`.

```
pa next --ready --claim        # take the next ready issue and claim it
pa next --claim --wait 60      # the same, waiting up to 60 s for supply
pa issue view PLAN-42 --json   # the complete issue as JSON, including the epic description
pa issue ask PLAN-42 "Which…"  # ask a question; the ticket waits for an answer
pa issue close PLAN-42 --result-file - # done, result as Markdown, claim released
```

Commitments that matter to agents:

- **`--json` prints the complete object**, not a selection of fields. No guessing field names, no second call.
- **Errors go to stderr, data to stdout.** Always, in the error case too.
- **Speaking exit codes.** A lost claim is a different code than a network error — an agent has to be able to tell whether to try again.
- **Configuration through environment variables** (`PLANAFFE_URL`, `PLANAFFE_TOKEN`) plus an optional `.planaffe` file in the repository that fixes the project. Whoever is in the repository never has to name the project.
- **Never interactive when stdin is not a terminal.** No editor, no prompt, no pager — an agent must never hang.
- **`--wait` instead of polling.** `pa next --wait <seconds>` blocks until a matching ticket is there or the deadline passes, and then returns exactly what it returns without it. That turns a loop with `sleep` into one line without idle time — and later, the same line into the wake-up mechanism from 15.9.

### 6.2 Web interface (for humans)

- **Issue list** as the central view: dense, fast, filterable, keyboard-operable.
- **Issue detail** with Markdown rendering at the centre — content before metadata.
- **Epic view:** description at the top, the issues belonging to it with their progress below. The issue list can be grouped by epic.
- **Release view:** what is in the open release, what was in the previous ones — as a list that can be copied out as a changelog.
- **Fast project switching** (switcher, keyboard shortcut) — multi-project is not a special case but the normal one.
- Visible at a glance what an agent is doing right now: who claimed what, and since when.
- Works well on a phone (read, triage, change status, comment).
- Inspiration for the interaction model: Linear (speed, keyboard-first, density) — without copying its feature surface.

**The detail view answers one question first: what does this ticket want from me right now?**

Tickets written by AI get long. Whoever opens the interface usually does not do it to read all of it, but because something is stuck — and then what is stuck belongs at the top, not the beginning of a three-page text.

Concretely, as far as the data allows:

- **Open question:** the question sits at the very top with an answer field, the ticket description collapsed below it. Whoever can answer the question without the context does not have to read the rest.
- **Blocked:** the blocker sits at the top, with title and status. The only relevant information is what is being waited for.
- **In progress:** who is working on it and since when — visible before you scroll.
- **Long descriptions** are collapsed after the first paragraphs, opened fully with one click.

Deliberately **no** summarisation logic, no heuristic guessing what is important. Only the re-ordering of what is already structured: labels, blockers, claims, comments. If the information lives in prose alone, we show the prose.

### 6.3 HTTP API

The CLI is a client of the public API. Everything the CLI can do, a directly connected agent or a script can do too.

## 7. Domain Model

The structure is backed by research (see [`docs/research/hierarchy-and-fields.md`](docs/research/hierarchy-and-fields.md)) and deliberately flat:

```
Instance
└── Project           (key, e.g. PLAN → PLAN-42)
    └── Epic          (optional — groups issues under one theme)
        └── Issue     (the unit of work)
            └── Sub-issue  (optional, exactly one level deep)
Comments              (on issues)
History               (on issues, written by the system)
Questions             (on issues, open or answered)
Labels                (defined per project)
Releases              (per project, across epics)
Users                 (human or agent)
```

That is the same shape Jira (epic → story → subtask) and GitLab (epic → issue → task) have.

**Refinements:**

- Epic, parent and sub-issue stay within **one project**. That keeps ID allocation and permission checks trivial.
- **Relationships** (`blocks`) between issues, on the other hand, may cross projects. A blocker in a project the caller cannot read is still evaluated for workability — otherwise "ready" would be a lie — but shown without key, title and status: only as "blocked by a ticket in another project".
- Issue IDs are project-scoped short IDs (`PLAN-42`), quotable in branch names, commits and pull requests.
- **Sub-issues are full issues:** their own key from the same sequence (`PLAN-43`, not `PLAN-42.1`), their own status, their own claim, their own `result`. The only thing they inherit from the parent is the epic, and they cannot set a different one — a theme that applies to the parent applies to its parts.
- **A parent does not close itself** when its last child closes. It only becomes workable again (see 10.): usually some assembly, an acceptance step or at least a `result` remains to be written. Whoever does not need that closes it with one command.

### The Epic

An epic is a theme several issues hang under — "auth rewrite", "migration to Postgres 17". It comes up constantly when developing with agents: a feature is broken into several tickets, and something has to hold those tickets together.

The epic is deliberately thin — it is not a unit of work but a bracket with a description:

| Field | |
|---|---|
| `key` | its own ID within the project, e.g. `PLAN-E3` |
| `title` | one line |
| `description` | Markdown — this is where the plan, the design, the architecture decision lives |
| `status` | only `open` / `closed` |
| `labels` | optional |

No assignee, no priority, no claim, no due date. Work happens on issues, not on epics.

**Rules:**

- An issue belongs to **at most one** epic. The field is optional — most issues have none.
- Epics are **not nested**. One level, no more.
- **Progress is derived**, not maintained: `PLAN-E3 · 3 of 7 done`.
- An epic can be closed while issues are still open — with a warning, but without a blockade (guiding principle 4).
- The epic's description is the **shared context for agents**: whoever claims an issue of the epic gets it delivered along with the ticket by the CLI.

### The Question

When an agent cannot go on, that is not a comment and not a label but a thing of its own on the ticket: **a question with an answer.**

| | |
|---|---|
| `question` | Markdown — what the agent needs to know |
| `asked_by`, `asked_at` | who, when |
| `answer` | Markdown; as long as it is empty, the question is open |
| `answered_by`, `answered_at` | who, when |

A ticket can carry several questions, each open or answered on its own.

Why this deserves structure and does not stay a comment: "are there open questions?" is a state, not a text search. The human wants the list of all open questions in the project, not the tickets that might contain one. And the agent that picks the ticket up later needs question and answer as a pair — not a comment thread it has to search.

Two things follow from that:

- **`needs-human-triage` as a label is unnecessary.** A ticket needs a human when an open question hangs on it. Derived instead of set, therefore impossible to forget.
- **Whoever is stuck has to say on what.** "Something is wrong here" is not a question. That is deliberately strict.

### The Comment

| Field | |
|---|---|
| `body` | Markdown |
| `author`, `created_at` | who, when |

Nothing more is needed. The only interesting part is the boundary against the question, because an agent has to be able to draw it:

A **comment** is a note on the ticket that forces nobody to act — an interim state, an observation, the reasoning behind a decision. A **question** is a state: the ticket waits and is not workable until it is answered (see 10.).

The rule of thumb we give agents: **whoever can go on comments. Whoever cannot go on asks.**

### The History

Every change to an issue is recorded: who, when, which field, from which value to which. Written by the system, not editable, not deletable.

This is not a compliance feature but a debugging feature. When working with agents, "what did it actually do there?" is the most frequent question you ask a ticket — and without a record you only see the final state. Who took over someone else's claim (see 11.), who set `ready`, when a ticket changed epic: you only ask that once something has gone wrong, and then the data is either there or gone for good.

Deliberately narrow: one entry per change, no diffs on text fields (for `description` and `result` it records *that* they changed, not how), no retention rules, no view of its own beyond a history under the ticket.

### The Release

We build software, so the question "what was in version 1.2?" belongs in the system. A release is a named version of a project:

| Field | |
|---|---|
| `name` | `v1.2.0` |
| `description` | Markdown — release notes |
| `status` | `open` / `published`, with a date |

**The decisive difference from a milestone: a release is not planned, it is recorded.** A milestone is a prediction ("this should go into 1.2") that rots the moment something slips — and then nobody maintains it. A release is a record ("this was in 1.2"), and that is always true.

Which is why it fills itself:

- Every project has exactly **one open release**. When a ticket is closed as `done`, it lands there automatically. `canceled` does not — what was not built belongs in no release notes.
- **Publishing freezes the state**, sets the date and creates the next open release.
- Moving a ticket by hand still works — a ticket that has not shipped yet simply does not belong.
- A ticket belongs to at most one **open** release.
- **When a ticket is reopened**, it leaves a release that is still open. If it sits in one already **published**, it stays there — it did ship, and you do not rewrite a record. When it is closed again, it additionally enters the current release. A ticket appearing in v1.2.0 *and* v1.2.1 is not a bug but exactly the story: shipped once, fixed once.
- `pa release notes v1.2.0` prints the contained tickets as Markdown. That is half the changelog work, and an agent can turn it into a readable changelog in one step.

Epic and release are orthogonal and do not fight: the **epic says what belongs together**, the **release says what shipped together**. An epic can stretch over several releases, a release contains tickets from many epics.

**Deliberately left out:**

| Left out | Reason |
|---|---|
| Milestones as a planning target | The milestone of the comparison systems mixes two things: thematic grouping (our epic does that) and shipping (our release does that). Separated, both are clearer, and neither rots. |
| Sprints / cycles | No comparison system makes them mandatory; for agents that do not work in two-week rhythms they mean nothing. |
| Portfolio levels (initiative, objective) | Consistently the most expensive features of the comparison systems. Irrelevant for 1–5 people. |
| Groups above projects | Only GitLab has this, and warns about deep nesting itself. A flat project list is enough. |
| Boards as a persisted level | A board is a saved view, not a membership — not a data model. |

## 8. Issue Fields

Seventeen fields — comparable to Linear's core, considerably leaner than GitLab or Jira:

| Field | Required | Range |
|---|---|---|
| `key` | assigned | `PLAN-42` |
| `project` | **required** | exactly 1 |
| `title` | **required** | one line |
| `description` | optional | Markdown, no images — the assignment |
| `result` | optional | Markdown — what was done |
| `status` | default `backlog` | see 9. |
| `ready` | default `false` | "implementable without asking first", see 10. |
| `priority` | default `0` | `0`–`4` |
| `labels` | optional | several, optionally grouped |
| `assignee` | optional | exactly 0 or 1 |
| `claim` | optional | identity plus timestamp, see 11. |
| `epic` | optional | 0 or 1, project-local |
| `release` | set on closing | 0 or 1, project-local |
| `parent` | optional | 0 or 1, project-local |
| `blocks` / `blocked_by` | optional | n:m, across projects |
| `author` | assigned | human or agent |
| `created_at` / `updated_at` / `closed_at` | assigned | timestamps |

**Priority is a fixed scale**, not a label: `0 = none`, `1 = low`, `2 = medium`, `3 = high`, `4 = urgent`. Monotonically increasing, so that `ORDER BY priority DESC` works without a special case (Linear encodes it the other way round and buys itself a sorting problem). The label route is out because it does not sort deterministically — GitLab has to document a heuristic for it that ends in "ties are broken arbitrarily". For an agent that is supposed to reliably fetch "the next most important issue", that is unusable. Tellingly, GitHub is currently leaving the label route and shipping priority as a native field.

**`blocks` / `blocked_by` is an MVP field, not a label.** All four comparison systems have a typed blocking relationship; GitHub retrofitted one recently. A label cannot say *what* is being waited for, and it does not dissolve when the blocker closes — a human notices that while reviewing, an agent does not. Without this field, "give me the next ready issue" cannot keep its promise, because *ready* does not only mean well specified but also unblocked. Technically a directed edge with two reading directions.

**Assignment and result are two fields.** `description` says what is to be done; `result` says what was done, and is filled when closing. Two different authors at two different times, read on two different occasions. Without a field of its own, the agent appends its report to the description, and then assignment and result sit mixed together in the ticket. The result also feeds the release notes.

**`result` is expected but not enforced.** Closing without a result goes through; CLI and interface point it out but stop nobody. A mandatory field at the most frequent status change of all would be exactly the intermediate dialog guiding principle 4 rules out — and an enforced field gets filled with "done" anyway. On `canceled`, the `result` holds the reason instead of the outcome: same field, same question — why is this ticket closed?

**Labels can be collected into groups.** Within a group only one label applies at a time — setting another replaces the previous one. This is not a custom-field construction kit but a property of the label itself: it optionally carries a group name.

The benefit: a group "kind" with `bug`, `feature` and `chore` replaces the type field we left out, without a ticket being able to be `bug` and `chore` at the same time. The model is Linear, which has the same concept.

**Assignee is single-valued.** GitLab, Linear and Jira allow exactly one; GitLab even makes multiple assignment a paid add-on. For a system whose core feature is exclusive claiming, "several are responsible" would be contradictory anyway.

**Assignee and claim are not the same thing.** The assignee says *who should be responsible* — set by hand, persistent, even when nobody is working. The claim says *who is working right now* — set on access, expiring by itself. Most tickets never have an assignee, and that is the normal case: whoever has none is free for any agent. Whoever has one belongs to that single identity (see 10.).

**Deliberately left out:**

| Left out | Reason |
|---|---|
| Estimation / story points | A core field in none of the four. Story points plan *human* capacity across sprints — without sprints they have no addressee, and an agent derives nothing from "5 points" that is not already in the description. |
| Due date | A date is supposed to produce reminders; in the MVP there are no notifications. A date that wakes nobody goes stale. Priority does the ordering better. **Revisit together with notifications.** |
| Issue type (bug/feature/task) | Linear has none and explicitly points to labels. A label group "kind" with `bug`/`feature`/`chore` does the same without a second classification system. |
| `resolution` alongside status | `done` versus `canceled` covers the distinction. Jira's separation produces exactly the mandatory-field dialogs guiding principle 4 rules out. |
| Reporter separate from author | Only Jira separates the two — sensible in the helpdesk context we exclude. |
| Components, versions, start date | No cross-system consensus; labels cover the grouping. |
| Custom fields | Guiding principle 3. Notably: Linear, our interaction model, simply has none. |
| A field of its own for acceptance criteria | None of the four systems has one — all solve it with Markdown checkboxes in the description. An agent reads the description in full anyway. Where criteria are to be tracked individually, they are sub-issues. |

## 9. Workflow

**The stance:** a status change must never cost more than one action. No mandatory-field dialogs, no transition conditions, no approvals.

**A single status set, fixed in the schema** — not configurable, no selectable variants:

```
backlog → todo → in_progress → done
                             ↘ canceled
```

`done` and `canceled` set the issue to closed automatically. Whoever only wants `open`/`closed` uses `todo` and `done` and ignores the rest.

**Why no choice between workflow variants:** no comparison system offers such a thing — they either have a fixed set or full configurability. A choice would be a third route nobody takes, and it would force CLI and UI to support two status models. The set above is exactly Linear's default workflow and matches GitLab's default.

Everything else happens through **labels**, not through status — the practice that has proven itself in agentic work (GitLab workflow labels, the approach of Matt Pocock and others). With exactly one exception:

**"Ready for an agent" is a field, not a label** (`ready`, see 8.). Other systems solve this with a `ready-for-agent` label; for us that does not work, because `pa next` evaluates this state and the project's triage-required switch arms it. What the system itself evaluates gets a field (guiding principle 5) — as a label it would be renamable and deletable, and with it the triage requirement would break silently.

Three workflow labels other systems would have here are therefore unnecessary: `blocked`, because blocking is a field (see 8.); `needs-human-triage`, because a ticket with an open question needs a human anyway (see 7.); and `ready-for-agent`, because it is a field. The first two follow from the data instead of having to be set; the third cannot be accidentally configured away.

What follows from this for selecting the next ticket is in section 10.

## 10. What Is Next?

This is the most important question the system has to answer. A user starts their agents and says: "go into the project and take the next free ticket." From that point on nobody may have to think any more — neither the agent, nor the human watching.

Everything in the previous sections — status, priority, blockers, labels, claims — serves this one question in the end. It is therefore not a by-product of the filter logic but a feature of its own, with its own command, its own view and its own semantics.

### When is a ticket workable?

A ticket is **ready** when all of this holds:

1. Status is `todo` — not `backlog` (not up yet), not `in_progress` (already running).
   A ticket in `in_progress` whose **claim has expired** counts here like `todo`: the claim is evaluated on read (see 11.), and the status falls back with it. Without this rule, the ticket of a crashed agent would vanish from the selection permanently.
2. It is **not claimed**, or the claim has expired.
3. **No open blocker.** All tickets in `blocked_by` are closed.
4. **No open question.** As long as a question is unanswered, the ticket waits for a human.
5. **No open sub-issues.** A ticket with open children is a bracket, not a unit of work — the agent takes the children.
6. **`ready` is set**, if the project has triage required switched on.
7. **It is not assigned to somebody else.** A ticket without an assignee is there for everybody; one with an assignee only pulls for that identity. Otherwise an assignment would have no effect — the next agent along would beat it to it. This very rule later also carries assignment to a named agent (15.8), without a single additional rule.

### What `ready` means

Not "a human has approved it", but: **the ticket is concrete enough that somebody can implement it without asking first.** A statement about the quality of the ticket, not a permission.

That matters because tickets in this target group are usually **created by an agent**: the user says "create me seven issues for the auth rewrite", and the agent writes them. A few of them are crystal clear, a few are notes that still have to ripen. Whoever writes the tickets knows that best — so they set the flag themselves, per ticket. A human can correct it at any time, but does not have to click through seven tickets just to get going.

The way back matters just as much: if an agent claims a ticket and notices that it is too vague, it asks a **question** (see 7.) and releases the ticket. With that it is automatically no longer workable and lands in "needs you" — nobody has to remember to flip a flag.

**Triage happens in the chat, not in the interface.** Whoever sees that PLAN-14 is stuck does not open the web app and type around in it, but tells their agent: "answer the open question in PLAN-14 like this and make the ticket more concrete." The agent answers the question and rewrites the ticket. The interface shows where it is stuck; acting happens through the CLI.

### The switch: triage required

The dividing line is not human versus agent, but: do I trust whoever creates the tickets?

- **Off** (default): everything in `todo` is pulled. `ready` remains useful as a filter and a warning, but stops nothing. No clicking work for the solo developer.
- **On**: only flagged tickets are pulled. Useful when several people create tickets, or when you want to look over them yourself after a breakdown, before five agents set off.

### The breakdown is the most important moment

When an agent turns an assignment into seven tickets, almost everything is decided right there: what belongs together (epic), what waits on what (`blocks`), what is ready to go (`ready`) and what is not. Done well, the agents run through on their own afterwards. Done sloppily, no field in the world helps.

That is why the CLI has to be good at **creating several related tickets in one go** — with epic assignment and dependencies between them, without the agent having to fire ten individual commands afterwards to wire the tickets together.

### When several agents ask at once

- **Fetching and claiming is a single operation**, server-side, in one transaction. The client does not pick. A "fetch the list first, then claim" would be a race that puts two agents on the same ticket — exactly the failure the system exists to prevent.
- **Selection order:** highest priority first; on equal priority, an epic in which no other agent is currently working; on a tie there too, the older ticket.
- **Epics are kept apart — but only as a tie-breaker.** Two agents in the same theme usually work on the same code, and the conflicts do not arise in the issue tracker but in the repository. Priority still trumps: an urgent ticket does not sit around because its epic is occupied. Tickets **without** an epic are never kept apart — they belong to no theme, so they are not one. `--epic PLAN-E3` still forces a particular theme.
- **An empty result explains itself.** When nothing comes back, the answer says why: "3 blocked, 2 waiting for an answer, 4 already in progress." The agent knows whether to wait or to stop; the human knows what to clean up. Whoever does not want to stop waits with `--wait` (6.1) instead of asking in a loop.

### In the interface

The question belongs in a fixed place for humans as well:

- **"Ready for agents"** — what an agent would pull now, in exactly that order.
- **"In progress"** — who is working on what, since when, in which epic.
- **"Needs you"** — open questions first, then blocked tickets and those without `ready`. This is the human's work list: provide supply so the agents do not run dry.

## 11. Claiming — the Core Feature for Agent Operation

Several agents work in parallel. It must not happen that two touch the same issue at the same time.

- An issue can be **claimed**. The claim is atomic: exactly one claim wins, all others get a clear refusal — technically a conditional update, not a lock.
- A claim belongs to an identity (human or agent) and carries a timestamp — visible in the UI and the CLI.
- The typical agent cycle is one command: "give me the next ready issue and claim it for me".

**Claim and status belong together.** Claiming sets the issue to `in_progress`, releasing sets it back to `todo`, closing releases the claim. One step, not two (guiding principle 4).

**A claim expires after four hours of inactivity.** Every change to the issue — a comment, a status change, an edit — extends it. That way a crashed agent blocks nothing permanently.

Deliberately part of it:

- **No heartbeat.** A separate "I am still alive" command would be an additional concept agents forget. Work on the issue *is* the sign of life.
- **No background job.** Expired claims are evaluated on read, not deleted by a cleanup process — that saves a scheduler and fits guiding principle 2. An expired claim counts as absent everywhere, and the ticket counts as `todo` again — in `pa next`, in lists and in the interface. The status change is therefore derived, not written.
- **The deadline is fixed** (changeable per instance through an environment variable, not per project). Four hours is generous for an agent run and short enough that a crash does not cost the day.
- **`claim --force`** takes over someone else's claim. Not pretty, but there are situations for it — and the alternative is somebody poking around in the database. The previous holder sees it in the history.

## 12. Users and Permissions

Multi-user is built in from the start — even if the first user works alone, the point comes when a second person joins.

- User accounts for humans; API tokens for agents, bound to an identity, so that it stays traceable **who** claimed and changed what.
- The permission model is deliberately coarse: an assignment of **which user may see and edit which projects**. Plus an admin role for instance administration.
- No fine-grained permission system, no field- or status-level rights, no role matrix.

**One token per agent — and the token is the agent.** An API token is not a human's second key but the identity an agent works under. That holds from the MVP on, because none of it can be retrofitted later without devaluing the history:

- **Do not reuse.** Whoever runs Claude Code and Codex side by side gives each its own token. We cannot enforce it — but everything the system will ever know about agents depends on it being true. So we say it clearly and set the CLI up so that the convenient path is also the correct one.
- **Every token has a name.** Randomly assigned on creation, changeable at any time. That way the history, the claim display and the lists do not say "Token 7f3a…" but something readable — and the agent becomes something you can talk about.
- **The agent records metadata about itself.** Through the CLI it writes to its own token what it is: which kind of agent, which harness, which environment. Only stable things. What changes per run — model, reasoning level, token usage — belongs on the ticket (15.2), not on the token.
- **The back channel is one-way.** An agent may write about itself. It may neither read nor change its token, may not create another one, and may not see anyone else's. Leaving something about yourself is harmless; anything else would be a privilege escalation through the back door.
- **"An agent" is fuzzy, and we do not pretend otherwise.** A window? An installation? A harness on a machine? We are not fixing that now. That is why the token does not store *the* truth about its agent, but **what was last reported back, as history**. If a pattern emerges over time, something unambiguous can grow out of it. The other way round — fix it first, then discover that reality looks different — would not work.

## 13. Technical Guard Rails

- **Licence:** MIT.
- **Storage:** PostgreSQL. All content, Markdown included, lives in the database. Backup = `pg_dump`.
- **Frontend:** React.
- **Deployment:** `docker compose up`. The target is two containers (app plus Postgres), three at most. No queue, no Redis, no S3, no Elasticsearch in the MVP.
- **Search:** Postgres's own means (full-text search), no separate index.
- **Configuration:** few environment variables, sensible defaults, migrations run on startup.
- **CLI distribution:** a single, easily installed binary or package that talks to any instance.
- **Waiting is solved in Postgres, not next to it.** Wherever a client waits for an event (`--wait`, see 6.1 and 15.9), `LISTEN`/`NOTIFY` wakes it with a deadline as the fallback — no broker, no Redis, no scheduler. The price is stated in the research and is worth paying, but it is real: `LISTEN` does not get along with transaction pooling and needs its own connection outside every pool; whoever puts the app behind a reverse proxy has to raise its timeouts. Both belong in the documentation before they belong in support requests.
- **Ticket content is not trustworthy.** What planaffe delivers to an agent was often written by another agent. Anthropic explicitly wraps such payloads as "untrusted data", OpenAI warns to sanitise input from issue text. That is already true today and changes nothing about the architecture — but it belongs in the documentation, not in a footnote.

## 14. MVP Scope

**Included:**

- Create, switch, manage projects
- Issues: create, view, edit, comment, change status, close — as Markdown
- Questions: ask, list, answer — filterable across the project
- Epics: create, assign issues, see progress, close
- Releases: closed tickets collect automatically, publish, content as Markdown
- "Fetch the next ticket and claim it" as one operation (see 10.), optionally waiting (`--wait`)
- Sub-issues (one level) and `blocks`/`blocked_by` relationships
- Labels per project, with groups for mutually exclusive values, including a default group "kind" (`bug`/`feature`/`chore`)
- Claiming including release and expiry
- History on the issue: who changed which field when
- Filtering, sorting, full-text search
- User administration, project assignment, named API tokens per agent with the metadata back channel (see 12.)
- A complete CLI with machine-readable output
- A responsive web interface
- A Docker Compose setup and documentation

**Not included (deliberately deferred):**

- File and image attachments (requires external storage → after the MVP)
- Sprints, boards with persistence, capacity planning
- Notifications (e-mail, webhooks)
- Git integrations
- Custom fields, workflow designer, automations
- Due dates, estimations, issue types, milestones (see 7. and 8.)
- An MCP server (very likely comes soon after the MVP — the CLI is enough at first)

## 15. Roadmap After the MVP

None of this belongs in the MVP. It is here so that we keep it in mind while building and do not wall any doors shut.

### 15.1 Already noted

Attachments and images with interchangeable storage · an MCP server as a second agent interface · webhooks and notifications · git integration (issue references from commits and branches) · a light board view · due dates (together with notifications) · an optional sprint level for teams that need one.

Two of these have a narrower role after the research than they had when they were written down, and that is important enough to note here:

- **The MCP server is a read and write interface, not a wake-up mechanism.** It is still a good idea — but it cannot start an agent (reasoning in 15.9).
- **Webhooks are not the route to an automatic start**, but an add-on for users with reachable infrastructure. That is also in 15.9.

### 15.2 Agent metadata on closing

When an agent closes a ticket, it records how the work came about: which agent, which model, which reasoning level, which harness, how many tokens, how long. Not as prose in a comment, but as **structured data on the issue**, so that it can be analysed later.

What that is good for: what does an epic really cost? Which model handles which kind of ticket? Is the higher reasoning level worth it for bugs? Nobody can answer such questions today, because the data does not come together anywhere — and in an issue tracker it comes together naturally.

- **Voluntary by default.** An agent that does not supply the fields can still close.
- **Enforceable per project.** With the switch on, closing without metadata is refused — with an error message that says what is missing.
- **The agent learns this in time.** When claiming, it is told what will be expected of it on closing. It should not run into a wall at the end.
- The schema stays fixed and small — no custom-field construction kit through the back door (guiding principle 3).

### 15.3 Project-wide instructions for agents

The user stores a text on the project that is delivered to every agent with every ticket — a kind of system prompt for the project. "Tests run with `just test`", "no new dependencies without asking", "migrations always reversible".

Today that lives in a `CLAUDE.md` in the repository. That works, but has a gap: it applies to the repository, not to the ticket. Stored on the project, it applies to every agent, every harness and every ticket, without anyone copying the file.

- The text is delivered on `issue view` and on claiming, together with the ticket.
- Conceivable **on the epic** as well, in addition to the project: "in this rewrite, the following also applies …". Project and epic text complement each other.
- Plain Markdown, no template system.

### 15.4 The stub — tickets that are not written out yet

A good thought rarely arrives at the desk. Today that means: either you type a bad ticket on the go, or you forget it. Both cost — and both are a problem from the era in which a badly written ticket stayed badly written.

**Quick capture in the interface:** pick a project, dictate or type the text, save. No title, no fields, nothing mandatory. The result is a ticket in the state **stub** — visibly marked, in no work list, unreachable for any implementing agent.

**It is written out by an agent.** It takes the stub and turns it into a title, a description, a kind and a priority, proposes an epic and dependencies — and asks a question (see 7.) when the thought is too thin, instead of guessing. Only then is it an ordinary ticket and can become `ready`.

Why this belongs in the issue tracker and not in a note-taking app: the stub is immediately in the right project and the right history, and the agent that writes it out has the project instructions (15.3) and the other tickets at hand. A note has none of that — it first has to be found again and transferred.

- **Dictation is the operating system's job**, not ours. All we need for it is one big text field — and no mandatory-field dialog interrupting a dictation (guiding principle 4).
- **`stub` is probably not a status.** The status set from 9. is meant to stay fixed and small; more likely a state *before* the workflow, comparable to `ready`, only the other way round.
- **A queue of its own?** Writing out is different work from implementing. Whether `pa next` hands out a second kind of work or whether it becomes a command of its own is open — but it is the same mechanism.
- **A stub must never quietly seep away.** It belongs visibly in "needs you" or in a list of its own, otherwise quick capture becomes a graveyard.

### 15.5 The ticket as a context package

An issue tracker for humans is built for skimming: you see a list, click into it, scroll to the comments, open the epic on the side. An agent cannot do that — it has one pass, and what it does not get in that pass is missing while it works.

That is why reading a ticket, for an agent, is **one operation that delivers everything needed**: the ticket itself, its epic's description, the project instructions (15.3), the questions already answered, the handover state of a previous run (15.10) and the outcome of the tickets that blocked it. Not as references it has to load one by one, but together.

That is more than a convenience: every extra fetch is a place where an agent misses something the issue tracker has known all along. And claiming is the natural moment — whoever takes a ticket gets everything belonging to it with it.

- **Complete does not mean unlimited.** What is delivered is a defined set, not a dumping ground. The moment the package grows, the selection itself becomes a decision — including what does *not* belong in it.
- **The other direction is already right:** what the agent returns on closing (15.2) is already conceived as a structured set. The package is the same principle applied to reading.

### 15.6 Measure the cut instead of estimating it

Story points answer the question "how many human days?". That one is settled. The new unit is the **agent run**: does this ticket fit in one pass, or not?

The difference from classic estimation is that nobody has to guess. As soon as the data from 15.2 exists — which run touched which ticket how often until it was closed — it can be measured in hindsight and applied to the next cut: "tickets of this kind take you more than one run on average — break it down." That turns an estimate into an observation, and a planning ritual into a hint at exactly the place where everything is decided anyway (see 10., "the breakdown is the most important moment").

**For the MVP that means nothing more than keeping the door open:** the data model has to allow several runs to be booked on the same ticket later, and a ticket to keep its breakdown history. Nothing is collected at first, and nothing is analysed.

**The run will become an object of its own anyway.** Linear (`AgentSession`), Cursor (`runs`), A2A (`Task`) and even MCP's tasks extension arrived at the same unit independently of one another — and in this vision it is needed in three places: the closing data (15.2), the cut here, and the handover state (15.10). That is no reason to build it early, but a good one to know the name already.

### 15.7 Where agents wait

If agents can work around the clock, the only loop in the system that does not close by itself is the question: an agent asks, releases the ticket — and then it lies there until a human looks in. That is the bottleneck, and today nobody sees it.

Two things could make it visible: how long open questions lie on average, and how often `pa next` came back empty although tickets were only waiting for an answer. The benefit is concrete — it tells you whether to prepare more tickets or simply to look in more often.

**This stays deliberately optional and off by default.** A number about how fast a human answers quickly stops being an operational metric and becomes an assessment of the person — all the more so once a second human can see it. If we build it, then as a property of the system ("things are backing up here"), not as a property of the user ("you are slow"), switchable off and without comparison between people. In doubt, rather not at all.

### 15.8 The agent as a named colleague

Once every token has a name, a description and a history (12.), the next step is small: **assign a ticket to a specific bot.** "This one should be taken by the Codex agent the next time it runs."

The remarkable part is that the mechanism for it is already in place. Condition 7 in 10. says: a ticket with an assignee only pulls for that identity, a ticket without one is there for everybody. If the bot is an identity, then `pa next` under its token fetches exactly what is intended for it — without a single new rule. All that is missing is that the bot is a named, recognisable identity instead of an anonymous key. That is exactly what 12. sets up.

Where this leads: agents become addressable like colleagues — one is set up for migrations, the other for frontend work. Together with the data from 15.2 and 15.6, that eventually becomes a justified allocation instead of a preference: who reliably handles which kind of ticket in one run?

- **The gesture is an industry standard.** GitHub (assign an issue to Copilot), GitLab, Jira (the Rovo agent in the assignee field), Linear (delegation) and Devin all start a run through an assignment. Five independent systems, the same movement — planaffe invents nothing here, it only has to settle who starts the run.
- **It costs waiting time — and the deadline is the duty, waking is the bonus.** A ticket waiting for a specific agent lies there until exactly that agent runs again. Of the two possible ways out, only one is reliable: **after a deadline the ticket is available to everybody again**, exactly as with the claim (11.). That costs one field and one condition in `pa next`, hangs on no foreign interface, and makes the assignment safe without any process having to run anywhere. The wake-up mechanism (15.9) makes the assignment *fast* — but it does not make it safe, because it too assumes the user has started something. **That is why the expiry deadline is built first.** Without it, the assignment is a trap, with or without a trigger.
- **It stays an assignment, not a role.** No capability profile, no skill matrix, no router deciding. Whoever assigns is a human (guiding principle 3).

### 15.9 How an agent learns that it is its turn

This is the gap 15.8 opens and that shows through everywhere else too: a ticket can be assigned — only the agent knows nothing about it. What is technically possible at all is worked out in [`docs/research/waking-agents-and-triggers.md`](docs/research/waking-agents-and-triggers.md).

**It is not the webhook.** A webhook needs a reachable target — Linear explicitly requires a publicly reachable HTTPS address for its agents, not `localhost`. That is exactly what the target group does not have: a developer with a laptop behind a router is not reachable, and no feature changes that. On top of that would come the whole delivery state — retries, failure history, disabling dead targets — and an SSRF surface that is particularly sharp in a `docker compose` setup, because the app container sits in the same network as Postgres. Outgoing webhooks remain sensible, but as an add-on for users with their own server, not as the route to an automatic start.

**It is not MCP either.** That is the correction to an expectation that creeps in easily. An MCP server over stdio is a child process of its client; it exists only while the agent is already running. And the standard is explicitly moving the other way: the July 2026 revision removed server-initiated requests, and even the tasks extension is polling. The MCP server from 15.1 remains right — as an interface for a running agent, not as a doorbell.

**It is the held outgoing connection.** Whoever had the same problem found the same answer: GitHub's self-hosted runners hold an outgoing connection open and therefore need, in their words, "no need for an inbound connection"; Claude Code's remote control makes "outbound HTTPS requests only and never opens inbound ports". The client asks and waits, the server answers when there is something. No open port at the user's end, no reachable target, no SSRF surface — and server-side, `LISTEN`/`NOTIFY` with a deadline as the fallback is enough. **The ticket table is the queue; a broker would only rebuild a persistence that is already there.**

**For the MVP that costs one flag.** `pa next --wait 60` blocks until a matching ticket is there or the deadline passes (6.1). With that, a user has the full benefit in one line, without planaffe shipping anything:

```
while :; do pa next --claim --wait 60 --json | my-agent; done
```

Whether the server behind it simply looks in a loop at first or uses `LISTEN`/`NOTIFY` straight away is a question of implementation behind the same interface — and therefore not one this vision has to answer.

**`pa watch` is the next stage, not the beginning.** A subcommand that holds the connection, starts a command on a matching ticket and writes its outcome back. That needs answers to questions one should only ask once `pa next --wait` exists: how many runs in parallel? What happens when the started process crashes — does the claim fall back? What starts the daemon after a reboot?

**What this explicitly does not solve.** A daemon at the user's end only moves "nobody wakes the agent" to "nobody started the daemon". The difference is real — you start the daemon once, the agent otherwise per ticket — but it is a gain in **latency and clarity, not in autonomy**. Which is why the expiry deadline from 15.8 remains the duty.

**And what we deliberately do not build.** There would be a shorter route: individual vendors now offer an HTTP endpoint that starts an agent run out of the user's subscription. That would be 15.8 in one go — but it applies to exactly one vendor, sits under "research preview" behind a dated beta header, has no key against double execution, and runs in someone else's cloud instead of at the user's workplace. An unusually large share of what exists in this field at all carries such a caveat. **That is the strongest argument for making planaffe's wake-up mechanism dependent on none of these interfaces** — and a recipe in the documentation is the right place for it, not a feature in the product.

### 15.10 Further ideas in the same direction

Unsorted, not yet decided:

- **A handover state on abort.** When a claim expires or an agent gives up, it leaves two sentences on how far it got and where it is stuck. The next one does not start from zero. Costs almost nothing and makes crashed runs considerably less painful.
- **Ticket templates per project.** A Markdown structure proposed on creation — requirement, acceptance criteria, testing notes, whatever the project needs. That is our answer to fixed ticket sections: changeable and deletable as a template, instead of in the schema forever (guiding principle 5).
- **An abort signal.** Today there is no way to tell a running agent to stop — `claim --force` takes the claim but stops no process. Linear and Cursor both have an explicit stop signal; planaffe has a gap here. As soon as the waiting connection from 15.9 exists, this is a second event on the same line and almost free.
- **A budget per project.** Once the token data from 15.2 exists, a ceiling per epic or project is the obvious next thing.
- **A commit and PR reference on closing.** The agent records branch and PR when it closes. The little brother of the git integration, and possible without it.

## 16. How We Measure Success

- From `git clone` to the first issue created in under five minutes, with a single command.
- An AI agent can carry out its complete working cycle through the CLI, without a human opening the UI.
- A status change and a claim cost exactly one action each.
- A new user understands the entire field set and workflow without a manual.
- Operations consist of Postgres backups — nothing else.

## 17. Open Points

The substantive questions are settled: hierarchy, epics, sub-issues, the field set, the priority scale, the status set, labels and label groups, `ready` as a field, claim semantics (11.), history and the CLI (6.1). Evidence for the field and hierarchy decisions: [`docs/research/hierarchy-and-fields.md`](docs/research/hierarchy-and-fields.md).

The wake-up mechanism is settled as well: a held outgoing connection instead of a webhook, `pa next --wait` as the smallest building block, the expiry deadline before the trigger (15.9). Evidence in [`docs/research/waking-agents-and-triggers.md`](docs/research/waking-agents-and-triggers.md).

Only one thing remains open, and it does not block the start:

- **Due date:** deliberately left out, because a date without a notification wakes nobody. The research on waking changed nothing about that; it only sharpened the coupling: a date needs a **time-triggered** event, and therefore a scheduler on the server — exactly what 11. avoids with good reason for claims. The waiting mechanism from 15.9 does not help here, because it waits for events somebody triggers anyway. Defer further, and do not design for it while building `pa next --wait`.

With that, the vision is ready to be decided on. The next step is the concrete data model and the API.
