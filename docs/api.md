# The HTTP API

This is the surface of cut one ([ADR 0009](./adr/0009-the-mvp-is-built-in-three-cuts.md)):
every endpoint, the shapes it takes and returns, the filters each collection
accepts, pagination, the error model and the exit codes the CLI derives from it,
idempotency, and the rules each act enforces. [`storage.md`](./storage.md) is
what it stands on; [`docs/api/openapi.json`](./api/openapi.json), once captured
([ADR 0005](./adr/0005-the-contract-is-checked-in-and-both-clients-are-generated-from-it.md)),
is the contract this document describes in prose. Where the two disagree, the
captured document is what the clients compile against and this one is wrong.

The CLI is a client of this API and nothing else (`codebase.md`), so every
verb of `pa` is a line in the tables below. The CLI's own shape — command tree,
flags, output — is PLAN-0012's; what it can do is decided here.

## Conventions

- **Paths are flat and addressed by key.** An issue key is unique across the
  instance, because the project key is, so an issue is `/issues/PLAN-42` and an
  epic `/epics/PLAN-E3` with no project in the path. Collections take the
  project as a filter. Identities, tokens, comments and questions have UUIDs.
- **JSON in, JSON out**, `snake_case` fields, timestamps as RFC 3339 in UTC with
  microseconds (`2026-09-02T14:03:07.123456Z`). Markdown fields are plain
  strings, never rendered by the server ([ADR 0007](./adr/0007-markdown-is-rendered-in-the-browser-and-never-as-html.md)).
- **No version in the path** ([ADR 0011](./adr/0011-the-api-carries-no-version-and-migrations-only-run-forward.md)).
  Every response carries `Planaffe-Version: <semver>`, `GET /version` returns
  the same, and the CLI sends `User-Agent: pa/<semver> (<os>/<arch>)`. The
  server rejects nothing on that basis; the CLI compares and reports skew.
- **Authentication is `Authorization: Bearer <token>`** on everything except
  `GET /version`. The server tells a user token from an agent token by the row
  it finds ([ADR 0015](./adr/0015-a-token-is-an-agent-or-a-users-key-and-an-agent-is-never-an-administrator.md));
  the client never says which it holds.
- **Writes are `POST`, `PATCH` and `DELETE`.** `PATCH` takes a partial object
  and changes only the fields present; a field set to `null` clears it. Every
  write accepts `Idempotency-Key` (below). Every write to an issue or an epic
  writes history.
- **Two issue shapes** ([ADR 0012](./adr/0012-a-list-returns-a-slim-issue-and-only-a-single-issue-is-complete.md)):
  `IssueSummary` from every collection, `Issue` from a single read and from
  every act that returns the issue it acted on. The same holds for
  `EpicSummary` and `Epic`.
- **Limits.** `title` at most 200 characters; `description`, `result`, a
  comment, a question and an answer at most 1 MiB each; a project `name` and
  an identity `name` at most 100. Above that is a validation error.

## Shapes

`IdentityRef` is how every identity appears inside another object:

```json
{ "id": "…", "kind": "agent", "name": "quiet-otter-42" }
```

`IssueSummary` — the slim issue every list returns:

```json
{
  "key": "PLAN-42",
  "project": "PLAN",
  "title": "Settle the claim's four columns",
  "status": "in_progress",
  "ready": true,
  "priority": 3,
  "labels": ["feature", "cut-1"],
  "epic": "PLAN-E2",
  "assignee": null,
  "claim": { "holder": { "id": "…", "kind": "agent", "name": "quiet-otter-42" },
             "since": "2026-09-02T14:03:07.123456Z",
             "expires_at": "2026-09-02T18:03:07.123456Z" },
  "blocked_by": [ { "key": "PLAN-40", "open": true } ],
  "open_questions": 0,
  "open_blockers": 1,
  "open_sub_issues": 0,
  "created_at": "…", "updated_at": "…", "closed_at": null
}
```

`status` and `claim` are the derived values of `issue_read` (`storage.md`): an
expired claim is `null` here and the status is `todo`. `expires_at` is `null`
for a user's claim. Cut two adds `parent` (the parent's key, or `null`) and
`release` (the name of the release the issue is in, `unreleased` for the open
one, or `null`) to the summary, and makes `open_sub_issues` count; it was in
the shape from the start because ADR 0012 puts it there and the clients should
not change when it starts counting.

`Issue` — the complete issue, everything above plus:

```json
{
  "description": "…markdown…",
  "result": null,
  "author": { "id": "…", "kind": "user", "name": "maintainer" },
  "labels": [ { "name": "feature", "group": "kind", "description": "…" } ],
  "epic": { "key": "PLAN-E2", "title": "Backend and data model", "description": "…markdown…", "status": "open" },
  "blocked_by": [ { "key": "PLAN-40", "title": "…", "status": "todo", "open": true } ],
  "blocks":     [ { "key": "PLAN-44", "title": "…", "status": "todo", "open": true } ],
  "comments":   [ { "id": "…", "author": {…}, "body": "…", "created_at": "…" } ],
  "questions":  [ { "id": "…", "question": "…", "asked_by": {…}, "asked_at": "…",
                    "answer": null, "answered_by": null, "answered_at": null } ],
  "project": { "key": "PLAN", "name": "planaffe", "triage_required": false, "review_required": false,
               "labels": [ { "name": "bug", "group": "kind", "description": "…" }, … ] }
}
```

This is the context package of cut one (VISION 15.5): the ticket, its comments
and questions, the epic's description, and the project's labels with their
descriptions — everything an agent gets in one read. `labels` and `epic` are
the full objects here and the key or name in the summary; the two shapes are
named apart in the contract so a client always knows which it holds. The
history is not part of it — it can be long and is read for a different reason —
and has its own endpoint.

`EpicSummary` and `Epic`:

```json
{ "key": "PLAN-E2", "project": "PLAN", "title": "…", "status": "open", "labels": ["feature"],
  "progress": { "total": 7, "closed": 5, "done": 4, "canceled": 1 },
  "created_at": "…", "updated_at": "…", "closed_at": null }
```

`Epic` adds `description`, `author`, and the full label objects. Progress
counts issues that are not deleted; the issues themselves are
`GET /issues?epic=PLAN-E2`.

`Project`:

```json
{ "key": "PLAN", "name": "planaffe", "triage_required": false, "review_required": false,
  "created_at": "…", "updated_at": "…" }
```

## Pagination

Every collection returns a page:

```json
{ "items": [ … ], "total": 412, "has_more": true, "next_cursor": "eyJ…" }
```

`limit` defaults to 50 and may be at most 200; a larger value is refused with a
validation error, not truncated (ADR 0012). `cursor` is opaque to the client —
the base64 of the sort key and id of the last item — and is only valid with the
same filters and sort it was issued for; the server refuses a cursor that does
not fit. `total` counts everything the filters match, `has_more` says whether a
next page exists, and `next_cursor` is `null` on the last page.

Collections of issues sort by `sort=updated|created|priority` with
`order=asc|desc`, default `updated desc`; `priority` sorts `priority desc,
created_at asc` regardless of `order`, which is the order `next` uses.

## Errors

Every error is `application/problem+json` (RFC 9457):

```json
{ "type": "/problems/claim-held", "title": "The issue is claimed by somebody else",
  "status": 409, "detail": "PLAN-42 is held by quiet-otter-42 since 14:03; pass force to take it over.",
  "instance": "/issues/PLAN-42/claim",
  "holder": { "id": "…", "kind": "agent", "name": "quiet-otter-42" } }
```

`type` is a stable, relative URI whose last segment is the code a client
switches on; `status` is the HTTP status; `title` and `detail` are for a
person. Extension members carry what the code needs — the holder on
`claim-held`, the offending fields on `validation`, the current object on
`stale`. The CLI prints `detail` to stderr and maps the code to an exit code.

| status | type | when |
|---|---|---|
| 400 | `validation` | a field is missing, malformed or over its limit; `errors` maps field to message |
| 400 | `cursor-invalid` | the cursor does not fit the filters or is not one the server issued |
| 401 | `unauthenticated` | no token, an unknown token, or a revoked one |
| 403 | `forbidden` | the identity may not do this — an agent creating a project, a non-administrator creating a user, an agent setting `ready` under triage required (`ready-requires-user`), an agent forcing a user's claim (`claim-protected`) |
| 404 | `not-found` | the key or id names nothing the caller can see |
| 404 | `deleted` | the issue exists but is in its grace period; `restorable_until` says how long |
| 409 | `claim-held` | the issue is held by somebody else and the act needs the claim, or `claim` was called without `force` |
| 409 | `claim-lost` | the caller's claim has expired and somebody else holds the issue now |
| 409 | `idempotency-mismatch` | the `Idempotency-Key` was used for a different request |
| 412 | `stale` | `If-Match` does not match the object's `updated_at`; the body carries the current object under `current` |
| 422 | `transition` | the status does not allow the act — closing a closed issue, claiming one in `review` |
| 422 | `cycle` | the blocker would close a cycle; `path` lists the keys |
| 422 | `has-issues` | the epic cannot be deleted while issues reference it; `count` says how many |
| 422 | `unknown-label` | `repo` or a label filter names a label the project does not have |
| 500 | `internal` | a bug; the response carries nothing else |

Three things an agent has to tell apart (VISION 6.1) are three different rows:
a lost claim is `claim-lost`, a version mismatch is `stale`, and a network
failure never reaches the server and produces no problem document at all.

### Exit codes of the CLI

The CLI derives its exit code from the status and the type, so that a script
can branch without parsing:

| exit | meaning | from |
|---|---|---|
| 0 | success | 2xx |
| 1 | unexpected | 500, a response the CLI cannot parse, a bug in the CLI |
| 2 | usage | bad arguments, `PLANAFFE_URL` or `PLANAFFE_TOKEN` unset, a `.planaffe` file the CLI cannot read |
| 3 | not found | 404 `not-found`, 404 `deleted` |
| 4 | refused | 400 `validation`, 422 of every type |
| 5 | conflict | 409 `claim-held`, 409 `claim-lost`, 409 `idempotency-mismatch` |
| 6 | stale | 412 `stale` |
| 7 | denied | 401, 403 |
| 8 | empty | `next` found nothing; in cut two also a `--wait` that reached its deadline |
| 9 | version skew | the CLI is too old or too new for the installation (ADR 0011) |
| 10 | unreachable | the installation could not be reached: DNS, connection refused, timeout, TLS |

`8` is not an error of the API — `next` answers 200 with no issue — but it is
the answer a loop most often branches on, so it has a code. `9` and `10` are
decided by the CLI before or without a response.

## Idempotency

Every write accepts an `Idempotency-Key` header: an opaque string of at most
200 characters, chosen by the client. The server stores the response under the
caller's identity and the key for 24 hours and answers a replay with the same
key and the same request — same method, path and body — from the store, with
the original status and body. The same key with a different request is refused
as `idempotency-mismatch`. Keys of different identities never meet.

**The CLI sends one on every write, generated by itself** — a UUID per
invocation, retried with the same key when the connection fails before a
response arrives. No agent has to know the header exists. This is what makes a
retry safe on the two writes where it is not safe by nature: a bulk create
replayed without it creates the seven issues twice, and `next` replayed without
it claims a second issue while the first is held under a response nobody
received.

The claim by key needs none (VISION 11): a second claim by the holder on the
same issue succeeds and extends. The CLI sends the header anyway, for
uniformity; the server answers from the store or the act, and both answers are
the same.

## Concurrency on text fields

An issue's and an epic's `updated_at` is the object's version. A `PATCH` may
carry `If-Match: "<updated_at>"` — the value as the client last read it,
quoted — and is refused with `stale` when the object has changed since,
carrying the current object so the client can merge and try again. Without the
header the write goes through.

This is the guard the epic's description needs as a living document that
several agents edit (VISION 7): a read-modify-write with the header cannot
silently overwrite what another agent wrote in between. `updated_at` moves on
every change to the object and its attachments — a comment on the issue moves
the issue's — so a stale refusal can be a false alarm; re-reading costs one
call and the refusal is never wrong in the direction that loses text. The CLI
offers `--if-match <updated_at>` on the edit verbs and sends nothing without it.

## Who may do what

The permission model is the coarse one of VISION 12, and cut one has one
dividing line and one role:

- **An agent works in projects; a user administers them.** An agent may create,
  read, change, comment on, close, claim and delete issues, epics, labels and
  questions in every project, and read projects and its own identity. It may
  not create or change a project, a user, an agent or a token, and may not
  list agents or tokens ([ADR 0015](./adr/0015-a-token-is-an-agent-or-a-users-key-and-an-agent-is-never-an-administrator.md)).
- **A user may do everything an agent may**, plus create projects and change
  their switches, create agents and their own tokens, rename and revoke agents
  they own, and list agents.
- **An administrator** may in addition create users, rename and revoke any
  agent, and delete projects. Whoever bootstrapped the instance is one.
- **Project access** is cut three; until then every identity sees every project,
  and nothing in `blocked_by` is ever hidden. The shape already allows it —
  `key` and `status` are nullable in a blocker reference, and a hidden one is
  `{ "key": null, "status": null, "open": true }` — so the contract does not
  change when access arrives.

Where a rule differs between a user and an agent, the endpoint below says so.

## Endpoints

### The instance and the caller

| method | path | who | does |
|---|---|---|---|
| `GET` | `/version` | anyone | `{ "version": "0.3.0" }`, no authentication |
| `GET` | `/me` | any | the caller: `IdentityRef` plus `administrator`, `owner` (for an agent) and `token: { "prefix": "pa_a1b2c", "created_at": "…" }` |

### Users, agents and tokens

| method | path | who | does |
|---|---|---|---|
| `POST` | `/users` | administrator | `{ name, administrator? }` → 201 with the user and, once, `token: { prefix, secret }`: the user's first user token, which the administrator hands over. This is the invitation of cut one; cut three replaces the secret in this response with the one-time link of VISION 12 |
| `GET` | `/users` | user | every user, `IdentityRef` plus `administrator` and `created_at`; no pagination, the list is people |
| `POST` | `/agents` | user | `{ name? }` → 201 with the agent (`IdentityRef` plus `owner`, `created_at`) and, once, `token: { prefix, secret }`. An omitted name is assigned |
| `GET` | `/agents` | user | every agent with its owner, prefix, `created_at` and `revoked_at`; no pagination |
| `PATCH` | `/agents/{id}` | owner or administrator | `{ name }` — rename; the history keeps the id, so old entries show the new name |
| `DELETE` | `/agents/{id}` | owner or administrator | revoke: `revoked_at` set, 204. The identity stays (ADR 0013) |
| `GET` | `/tokens` | user | the caller's own user tokens: id, prefix, `created_at`, `revoked_at` |
| `POST` | `/tokens` | user | 201 with `{ id, prefix, secret }` — a further key for the caller, shown once |
| `DELETE` | `/tokens/{id}` | user | revoke one of the caller's own; 204 |

A revoked token answers `unauthenticated` from the next request on. The
metadata back channel (`PATCH /me/metadata`) is cut two.

### Projects

| method | path | who | does |
|---|---|---|---|
| `POST` | `/projects` | user | `{ key, name, triage_required?, review_required? }` → 201 `Project`, with the `kind` label group created |
| `GET` | `/projects` | any | every project the caller sees; no pagination |
| `GET` | `/projects/{key}` | any | `Project` |
| `PATCH` | `/projects/{key}` | user | `{ name?, triage_required?, review_required? }`; the key is immutable |
| `DELETE` | `/projects/{key}` | administrator | soft delete of the project and everything in it; 204. The CLI asks for the key to be typed; the API does not |
| `POST` | `/projects/{key}/restore` | administrator | back, with everything in it, into whatever state it was |

### Labels

| method | path | who | does |
|---|---|---|---|
| `GET` | `/projects/{key}/labels` | any | every label of the project with `name`, `group`, `description`; no pagination |
| `POST` | `/projects/{key}/labels` | any | `{ name, group?, description? }` → 201 |
| `PATCH` | `/projects/{key}/labels/{name}` | any | `{ name?, group?, description? }`. Changing the group is refused with `validation` when an issue would end up with two labels of the new group; `issues` lists them |
| `DELETE` | `/projects/{key}/labels/{name}` | any | soft delete; the label vanishes from every issue; 204 |
| `POST` | `/projects/{key}/labels/{name}/restore` | any | back, with its attachments |

### Next

| method | path | who | does |
|---|---|---|---|
| `GET` | `/projects/{key}/next` | any | what the caller would be handed, in that order, as a page of `IssueSummary` — the "ready for agents" list — plus `reasons` |
| `POST` | `/projects/{key}/next` | any | the act: take the highest-ranked workable issue and claim it for the caller, in one transaction. 200 with `{ issue: Issue | null, reasons }` |

Both take the same filters, as query parameters on `GET` and as a JSON body on
`POST`:

| filter | meaning |
|---|---|
| `ready` | `true`: only flagged issues, whatever the switch says (VISION 6.1) |
| `epic` | only this epic's issues |
| `label` | repeatable; only issues carrying every named label |
| `repo` | the `.planaffe` file's label: only issues carrying it or carrying no label of the `repo` group at all (VISION 13). A name the project does not have is `unknown-label` |

**Workable** is the eight conditions of VISION 10 evaluated for the caller —
conditions 5 and 8 are vacuous in cut one — and the order is priority
descending, then issues whose epic nobody else is working in before issues
whose epic somebody is (an issue without an epic counts as nobody), then
`created_at`, then the number. The `POST` selects with `for update skip
locked` and updates the row it locked, so two agents asking at once get two
different issues and neither waits for the other. The claim it takes is the
ordinary one: `in_progress`, the caller as holder, the expiry by the holder's
kind, a history entry — with `note: expired` when the issue's previous claim
had lapsed.

`reasons` is why the rest was not handed out, and every count is independent —
an issue can count under several:

```json
{ "blocked": 3, "waiting_for_answer": 2, "in_progress": 4, "in_review": 5,
  "parked": 6, "not_ready": 1, "assigned_elsewhere": 0 }
```

`not_ready` counts only where triage is required or `ready` was asked for.
When `POST` finds nothing, `issue` is `null`, `reasons` says why, the status is
200, and the CLI exits 8.

### Issues

| method | path | who | does |
|---|---|---|---|
| `POST` | `/issues` | any | create one or several wired-up issues in one transaction (below) → 201 with `{ items: [Issue] }` in the order given |
| `GET` | `/issues` | any | a page of `IssueSummary`, filtered (below) |
| `GET` | `/issues/{key}` | any | `Issue`. A deleted issue is 404 `deleted` |
| `PATCH` | `/issues/{key}` | any | `{ title?, description?, result?, priority?, ready?, assignee?, epic?, labels?, status? }`, `If-Match` honoured. `assignee` is a name or `null`; `epic` a key or `null`, and attaching to a closed epic reopens it; `labels` replaces the whole set, groups enforced; `status` accepts only `backlog` and `todo`, on an open, unclaimed issue that is not in `review` — parking and unparking are the two moves that carry no rule, and every other move is an act below ([ADR 0016](./adr/0016-status-transitions-are-acts-not-a-field-you-write.md)) |
| `DELETE` | `/issues/{key}` | any | soft delete; the claim is let go; 204 |
| `POST` | `/issues/{key}/restore` | any | back into whatever state it was in, without its claim |
| `GET` | `/issues/{key}/history` | any | every entry, oldest first, `{ id, actor, at, field, old_value, new_value, note }`; identities rendered as `IdentityRef` in `actor` and, for `assignee` and `claim`, in the values. Not paginated |

**Filters of `GET /issues`.** `project` (key), `status` (repeatable), `ready`,
`priority` (`min` and `max` as `priority_min`, `priority_max`), `label`
(repeatable, all must match), `epic` (key, or `none`), `assignee` (name, or
`none`, or `me`), `claimed` (`true`, `false`, or a name, or `me`), `author`
(name or `me`), `blocked` (`true`: has an open blocker), `has_open_question`,
`deleted` (`true`: only issues in the grace period, with `deleted_at` and
`deleted_by` added to each item — the `--deleted` list of ADR 0013; it is the
one read that sees deleted rows), `sort`, `order`, `cursor`, `limit`. `status`
matches the derived status, so `status=todo` includes issues whose claim has
expired.

**Creating several issues in one act** is one request whose body is an array;
a single issue is an array of one, and there is no second shape:

```json
{ "project": "PLAN",
  "issues": [
    { "ref": "schema",   "title": "…", "description": "…", "priority": 3, "ready": true,
      "labels": ["feature", "cut-1"], "epic": "PLAN-E2" },
    { "ref": "contract", "title": "…", "blocked_by": ["schema", "PLAN-6"] },
    { "ref": "cli",      "title": "…", "blocked_by": ["contract"], "assignee": "quiet-otter-42" }
  ] }
```

`ref` is a handle valid inside this request only; `blocked_by` and `blocks`
take refs and existing keys alike. The transaction allocates the keys in one
increment, inserts the issues, then the edges — refusing a cycle among them —
and commits, or refuses the whole request: seven issues and their wiring or
none, because blockers pointing at issues that do not exist break `next`. The
`repo` label of a `.planaffe` file arrives as an ordinary label in `labels`;
the CLI puts it there unless told `--repo none` (VISION 13). Issues are born in
`todo` (VISION 9); `status: "backlog"` in an item parks it from birth.

Under triage required, an agent's `ready: true` — on create or on `PATCH` — is
refused as `ready-requires-user`; `ready: false` goes through (VISION 10).

### The acts on an issue

Every act returns the complete `Issue` it acted on with 200, writes history,
and — when the caller is the holder — extends the claim. An act that needs the
issue to be unclaimed or held by the caller is refused with `claim-held` when
somebody else holds an unexpired claim, except that **a user may act over any
claim** — a human's word is not stopped by an agent's hold, and a user's hold is
protected only against agents, which `claim-protected` says.

| method | path | body | does |
|---|---|---|---|
| `POST` | `/issues/{key}/claim` | `{ force? }` | claim (VISION 11). On an open issue not in `review`, whatever else it is — parked, blocked, waiting on a question. Unclaimed or expired: taken. Held by the caller: extended, 200. Held by somebody else: `claim-held`, unless `force`, which takes it over with `note: forced` — over a user's claim only by a user. Sets `in_progress` |
| `POST` | `/issues/{key}/release` | — | let go: the claim is cleared and the status is `todo`, wherever the claim started. Only the holder, or a user; `transition` when nobody holds it |
| `POST` | `/issues/{key}/close` | `{ status: "done" \| "canceled", result? }` | close. From any open status. A user's close lands where it says; an agent's lands there too unless **review required** is on, when it lands in `review` — `canceled` included — with the `result` kept for the reviewer ([ADR 0014](./adr/0014-review-is-a-status-and-a-project-switch.md)). Clears the claim, sets `closed_at` on a real close. `result` overwrites; omitting it keeps what is there, and a close with none is pointed out by the CLI, never refused |
| `POST` | `/issues/{key}/review` | `{ result? }` | hand in explicitly, whatever the switch says. From any open status but `review`. Clears the claim, no `closed_at` |
| `POST` | `/issues/{key}/reopen` | `{ comment? }` | one movement from `review`, `done` or `canceled` to `todo`: `closed_at` cleared, no claim, `result` kept. `comment` is written as a comment first and expected on the way back from `review` — pointed out when missing, never refused |
| `POST` | `/issues/{key}/comments` | `{ body }` | 201 with the comment. On any issue, by anyone, claimed or not — a comment forces nobody to act (VISION 7) |
| `POST` | `/issues/{key}/questions` | `{ question }` | ask. 201 with the question. Does not release the claim (VISION 10). On any open issue |
| `POST` | `/questions/{id}/answer` | `{ answer }` | answer an open question; a second answer is `transition`. 200 with the question. Users and agents alike — the convention that an agent answers only when told to is a convention |
| `POST` | `/issues/{key}/labels/{name}` | — | add one label, replacing another of its group; 200 `Issue` |
| `DELETE` | `/issues/{key}/labels/{name}` | — | remove one; 200 `Issue` |
| `POST` | `/issues/{key}/blocked-by/{blockerKey}` | — | add a blocker, across projects if need be; `cycle` when it closes one; 200 `Issue` |
| `DELETE` | `/issues/{key}/blocked-by/{blockerKey}` | — | remove it; 200 `Issue` |

What a status allows, in one table — rows are where the issue is, columns what
is asked of it:

| from \ act | `claim` | `release` | `close` | `review` | `reopen` | `status: backlog` | `status: todo` |
|---|---|---|---|---|---|---|---|
| `backlog` | yes | — | yes | yes | — | — | yes |
| `todo` | yes | — | yes | yes | — | yes | — |
| `in_progress` | holder: extend | holder or user | yes | yes | — | — | — |
| `review` | — | — | yes, see below | — | yes | — | — |
| `done`, `canceled` | — | — | — | — | yes | — | — |

An empty cell is `transition`. A `claim` on `review` is refused because the
issue has been handed over; whoever wants it sends it back to `todo` first
(VISION 11). "Yes" in `in_progress` means with the claim: the holder, or a
user over an agent's hold.

**Closing out of `review`** is the reviewer's act: a user's close lands in
`done` or `canceled`. An agent's close from `review` goes through only where
review is not required — there the agent's word is what closes issues anyway;
where it is required, it is refused as `transition`, because the issue is
already where an agent's close lands and a human accepts it from here (ADR
0014).

### Questions across the project

| method | path | who | does |
|---|---|---|---|
| `GET` | `/questions` | any | a page of questions with their issue's key and title: `project`, `open` (`true` default), `issue` (key), `cursor`, `limit`; oldest first |

This is the "are there open questions?" of VISION 7 as a list. The full "needs
you" list, with the blocker-chain rule, is `GET /projects/{key}/needs-you`
(cut two, below); until it exists it is this list plus
`GET /issues?status=review`.

### Epics

| method | path | who | does |
|---|---|---|---|
| `POST` | `/epics` | any | `{ project, title, description?, labels? }` → 201 `Epic` |
| `GET` | `/epics` | any | a page of `EpicSummary`: `project`, `status` (`open` default, or `closed`, or `all`), `label`, `cursor`, `limit`; newest first |
| `GET` | `/epics/{key}` | any | `Epic` |
| `PATCH` | `/epics/{key}` | any | `{ title?, description?, labels? }`, `If-Match` honoured; the description is the living document of VISION 7, and the history records that it changed |
| `POST` | `/epics/{key}/close` | any | `closed`, `closed_at` set, whatever is still open — the response carries `progress`, and the CLI lists what is open and offers to cancel or park it. Gates nothing (VISION 7) |
| `POST` | `/epics/{key}/reopen` | any | back to `open` |
| `DELETE` | `/epics/{key}` | any | soft delete, refused with `has-issues` while any issue, deleted ones included, references it |
| `POST` | `/epics/{key}/restore` | any | back |

## What cut two adds

Designed in PLAN-32 (2026-09-02), on the schema `docs/storage.md` describes
under the same heading. Nothing above changes shape; every addition is a new
endpoint, a new filter, or a new field beside the existing ones.

### Sub-issues

`parent` on the issue: the parent's key in `IssueSummary`, the parent's
`IssueRef` in `Issue`, and `sub_issues` — a list of `IssueRef` — in `Issue`.
Set at creation (`parent` in an item of the bulk body, a key or a ref) or by
`PATCH` (`parent`, a key or `null`), on an open issue only. One level: a
parent that has a parent is `one-level`; another project is `other-project`;
`epic` on a sub-issue is `epic-inherited`, because the sub-issue's epic is its
parent's and follows it. Priority is copied at birth.

`next` applies conditions 5 and 8 of VISION 10: a sub-issue whose parent is
parked, closed or blocked is not workable, and counts under `parent_gated` in
`reasons` — a new count beside the existing ones. Deleting a parent is
`has-sub-issues` while a sub-issue, deleted ones included, references it. The
CLI: `pa issue create --parent PLAN-42`, and `pa issue view` lists the
sub-issues under the description.

### Releases

| method | path | who | does |
|---|---|---|---|
| `GET` | `/projects/{key}/releases` | any | every release of the project, the open one first, then published ones newest first: `{ name, status, description, published_at, published_by, issues }` with `issues` a count. Not paginated |
| `GET` | `/projects/{key}/releases/{name}` | any | one release with its issues as `IssueSummary`, sub-issues after their parent; `unreleased` names the open one |
| `PATCH` | `/projects/{key}/releases/{name}` | any | `{ description? }` — the release notes are a living document until published, and editable after, because a record can be annotated |
| `POST` | `/projects/{key}/releases/publish` | any | `{ name, description? }`: names the open release, freezes it, sets the date and the caller, and creates the next open one, in one transaction. 201 with the published release. A name the project has is `release-exists`; `unreleased` and `none` are reserved |

`GET /issues` gains `release` (a name, `unreleased`, or `none`). An issue's
membership follows the acts (VISION 7): `done` lands in the open release,
`canceled` in none, reopening leaves an open release and stays in a published
one, a sub-issue ships with its parent. Deleting an issue in a published
release is `in-published-release`. The CLI: `pa release list`, `pa release
view NAME`, `pa release publish NAME [--description-file F]`, and `pa release
notes NAME`, which prints the issues as Markdown — `- PLAN-42 Title` with
sub-issues indented under their parent — and is composed by the CLI from the
view, not by the instance.

### Needs you

| method | path | who | does |
|---|---|---|---|
| `GET` | `/projects/{key}/needs-you` | any | what only a human can resolve, in this order: open questions, issues in `review`, then — only where triage is required — issues without `ready`, then stuck issues. A page of `{ issue: IssueSummary, because }` with `because` one of `question`, `review`, `unready`, `stuck` |

**Stuck** is the blocker-chain rule of VISION 10: a blocked issue is on the
list only when a chain of open blockers from it ends in a dead end — an issue
that is parked, or has an open question, or is in a project with no agent —
because a blocker an agent will pull needs nobody and is noise here. The
predicate is one recursive query over the open edges, evaluated in SQL beside
`Workable`. The CLI: `pa needs-you`, which prints the four groups under their
headings.

### Waiting

`wait`, in seconds, on three reads — one mechanism, three doors (VISION 6.1):

| on | as | returns when |
|---|---|---|
| `POST /projects/{key}/next` | `wait` in the body | an issue was handed out, or the deadline passed — then the same `{ issue: null, reasons }` as without `wait` |
| `GET /questions/{id}` | `wait` in the query | the question was answered, or the deadline passed — then the question as it is |
| `GET /projects/{key}/needs-you` | `wait` in the query, with `If-None-Match` carrying the `ETag` of the last answer | the list differs from the one the caller has, or the deadline passed — then `304` |

The query is run first, and only when it finds nothing to return does the
request wait: on the project's notification channel (`docs/storage.md`,
Wake-ups), re-running the query on every notification, until it finds
something or the deadline passes. `wait` is at most `3600`; a larger value is
`wait-too-long`, and the CLI's `--wait` takes any number of seconds and asks
in rounds. Nothing about the answer depends on whether it waited, which is
what lets `pa next --claim --wait 60` be a loop without a `sleep`. What an
operator's proxy has to allow is in `docs/operations.md`.

The CLI: `pa next --wait S`, `pa issue ask KEY "…" --wait S` — which holds
the claim and waits for the answer, for at most the rest of the claim (VISION
10) — and `pa needs-you --wait S`.

### Search

`q` on `GET /issues` and `GET /questions`: a full-text filter in the words a
search box takes — `claim expired`, `"for update"`, `-flaky` — matched with
`websearch_to_tsquery` against the `simple` configuration, so identifiers
survive (`docs/storage.md`, Full-text search). On issues it matches the title,
the description, the result, and the issue's comments and questions; on
questions the question and its answer. A filter, not a ranking: the list keeps
its order. The CLI: `pa issue list -q "…"`, `pa question list -q "…"`.

### The agent's metadata

| method | path | who | does |
|---|---|---|---|
| `PATCH` | `/me/metadata` | agent | `{ kind?, harness?, environment?, version? }` — each a string of at most 100 characters or `null` to clear; a field left out is unchanged; any other field is `unknown-field`. 200 with `Me`, which gains `metadata` and `metadata_reported_at`. A user token is `forbidden`: a user has no metadata (ADR 0015) |

The back channel of VISION 12, one way: an agent writes about itself and reads
nothing of anyone. `AgentSummary` gains the same two fields, so `pa agent
list` and `pa agent view` show what an agent last said about itself and when.
The CLI: `pa me set --kind claude-code --harness cli --version 2.1`.

### Bulk changes

| method | path | who | does |
|---|---|---|---|
| `PATCH` | `/issues` | any | `{ keys: [...], changes: { …the body of the single PATCH… } }`: the same change on every key, in one transaction, all or none. 200 with `{ items: [Issue] }` in the order given |
| `DELETE` | `/issues` | any | `{ keys: [...] }`: soft-deletes every key, all or none. 204 |

Every key is the single act with its rules, and the first refusal refuses the
whole request with the problem it would have had alone plus `key`, the issue
it stopped at. All or nothing, like bulk create, because the use is "close
these twelve duplicates" and half of that is the worst state to be left in; a
caller repeats the whole request after fixing the one. At most 100 keys
(`too-many`); `If-Match` is not honoured on a bulk change. The acts — close,
reopen, claim — stay one issue at a time (ADR 0016). The CLI: `pa issue edit
KEY KEY… --…`, `pa issue delete KEY KEY…`.

### The export

Not an endpoint. `pa export --json [--project KEY]` reads the lists that exist
— the project, its labels, its epics, its releases, every issue with its
comments and questions, and every issue's history — and writes one JSON
document per project:

```json
{ "exported_at": "…", "planaffe": "1.2.0",
  "project": { … }, "labels": [ … ], "epics": [ … ], "releases": [ … ],
  "issues": [ { …Issue…, "history": [ … ] } ] }
```

The way out of the product (VISION 13): `pg_dump` is the backup, this is the
readable copy. There is no importer; an agent given this document and
`docs/cli.md` recreates the project through `pa issue create --file`, which is
the ability the product is built for. A separate endpoint would be a second
way to read what the lists already say (ADR 0012).
