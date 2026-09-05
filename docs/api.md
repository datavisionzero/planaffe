# The HTTP API

This is the surface through cut three ([ADR 0009](./adr/0009-the-mvp-is-built-in-three-cuts.md)):
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
- **A newline in a Markdown field is a line break**, and a blank line separates
  paragraphs, so a paragraph is sent as one line however long it is. Text that
  arrives hard-wrapped is stored and returned exactly as it was sent — the
  server changes nothing — and renders as the staircase it now says it is
  ([ADR 0020](./adr/0020-a-newline-is-a-line-break-and-stored-text-is-not-hard-wrapped.md)).
- **No version in the path** ([ADR 0011](./adr/0011-the-api-carries-no-version-and-migrations-only-run-forward.md)).
  Every response carries `Planaffe-Version: <semver>`, `GET /version` returns
  the same, and the CLI sends `User-Agent: pa/<semver> (<os>/<arch>)`. The
  server rejects nothing on that basis; the CLI compares and reports skew.
- **Authentication is either `Authorization: Bearer <token>` or the browser
  session cookie.** The server tells a user token from an agent token by the row
  it finds ([ADR 0015](./adr/0015-a-token-is-an-agent-or-a-users-key-and-an-agent-is-never-an-administrator.md));
  a browser session resolves to the same user caller. `GET /version` and the
  explicit sign-in, activation and recovery endpoints are public.
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
  an identity `name` and email address at most 100. A password is at least 12
  and at most 1,024 characters. Above that is a validation error.

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

`PageSummary` and `Page`:

```json
{ "slug": "architecture", "project": "PLAN", "title": "Architecture",
  "labels": ["reference"],
  "updated_by": { "id": "…", "kind": "user", "name": "maintainer" },
  "created_at": "…", "updated_at": "…" }
```

`Page` adds `body` — the Markdown — `author`, and the full label objects. The
summary carries no body on purpose: a wiki of thirty pages would otherwise be a
context eater for anything that only wanted to know what is there
(ADR 0012). `updated_by` is in both, because the list is read for who touched
what last.

`Project`:

```json
{ "key": "PLAN", "name": "planaffe", "triage_required": false, "review_required": false,
  "created_at": "…", "updated_at": "…" }
```

`User`, `BrowserSession` and `SmtpStatus`:

```json
{ "id": "…", "name": "maintainer", "email": "maintainer@example.test",
  "state": "active", "administrator": true, "created_at": "…" }
{ "id": "…", "created_at": "…", "last_used_at": "…", "expires_at": "…",
  "current": true }
{ "configured": true, "host": "smtp.example.test", "port": 587,
  "security": "starttls", "sender": "planaffe <no-reply@example.test>" }
```

SMTP status never carries a username, password or connection string.

## Browser request protection

The production cookie is named `__Host-planaffe_session` and is `HttpOnly`,
`Secure`, `SameSite=Lax`, `Path=/`, with no `Domain`. Explicit local development
uses `planaffe_session` without `Secure`; no other mode may weaken it. The cookie
contains only the opaque session secret.

Every `POST`, `PATCH` or `DELETE` authenticated by that cookie also requires
`X-Planaffe-CSRF: 1` and an `Origin` exactly equal to the configured public
origin. Missing or mismatched protection is `csrf`. A Bearer-authenticated
request is not subject to either browser check. If both credentials arrive,
Bearer authentication wins and the cookie is ignored.

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

Collections of issues sort by `sort=updated|created|priority|epic` with
`order=asc|desc`, default `updated desc`; `priority` sorts `priority desc,
created_at asc` regardless of `order`, which is the order `next` uses.

`epic` makes the epic the first sort key, so that a group opens exactly once
and never a second time on the next page — a list that groups by epic groups by
sorting, not by cutting up the page it happens to hold. The chain is the epic
key ascending in the two halves it is made of, the project's key and then the
epic's number, so that `PLAN-E9` comes before `PLAN-E10`; the issues under no
epic are one group at the end. Within a group it is `priority desc, number
asc` — within a theme the question is what is up next, the same question `next`
answers. Like `priority`, `epic` ignores `order`. The secondary sort is named
here so that it is read as a decision rather than as an accident of the
implementation.

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
| 400 | `unknown-field` | a closed request object contains a field it does not define; `field` names it |
| 400 | `cursor-invalid` | the cursor does not fit the filters or is not one the server issued |
| 401 | `unauthenticated` | no token, an unknown token, or a revoked one |
| 403 | `csrf` | a cookie-authenticated write has no CSRF header or the wrong origin |
| 403 | `forbidden` | the identity may not do this — an agent creating a project, a non-administrator creating a user, an agent forcing a user's claim (`claim-protected`) |
| 404 | `not-found` | the key or id names nothing the caller can see |
| 404 | `deleted` | the issue exists but is in its grace period; `restorable_until` says how long |
| 409 | `claim-held` | the issue is held by somebody else and the act needs the claim, or `claim` was called without `force` |
| 409 | `claim-lost` | the caller's claim has expired and somebody else holds the issue now |
| 409 | `idempotency-mismatch` | the `Idempotency-Key` was used for a different request |
| 409 | `release-exists` | the project already has a release with that case-insensitive name |
| 409 | `last-administrator` | deactivation or demotion would leave no active administrator |
| 409 | `email-exists` | an invitation or confirmed email would duplicate a normalized address |
| 410 | `secret-expired` | an invitation, recovery or email-change secret is used, replaced or expired |
| 412 | `stale` | `If-Match` does not match the object's `updated_at`; the body carries the current object under `current` |
| 422 | `transition` | the status does not allow the act — closing a closed issue, claiming one in `review` |
| 422 | `cycle` | the blocker would close a cycle; `path` lists the keys |
| 422 | `has-issues` | the epic cannot be deleted while issues reference it; `count` says how many |
| 422 | `in-published-release` | the issue is part of an immutable published release and cannot be deleted |
| 422 | `unknown-label` | `repo` or a label filter names a label the project does not have |
| 422 | `wait-too-long` | `wait` exceeds the server's one-hour ceiling; `maximum` is 3600 |
| 422 | `too-many` | a bulk change contains more than 100 issue keys |
| 422 | `smtp-not-configured` | an action that must send email cannot do so |
| 429 | `login-throttled` | too many failed sign-ins for the account or source address; `Retry-After` is set |
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
| 4 | refused | 400 `validation`, 400 `unknown-field`, 422 of every type |
| 5 | conflict | 409 `claim-held`, 409 `claim-lost`, 409 `idempotency-mismatch`, 409 `release-exists` |
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

An issue's, an epic's and a page's `updated_at` is the object's version. A `PATCH` may
carry `If-Match: "<updated_at>"` — the value as the client last read it,
quoted — and is refused with `stale` when the object has changed since,
carrying the current object so the client can merge and try again. Without the
header the write goes through.

This is the guard the epic's description and a page's body need as living
documents that several agents — and a human in the browser — edit (VISION 7): a read-modify-write with the header cannot
silently overwrite what another agent wrote in between. `updated_at` moves on
every change to the object and its attachments — a comment on the issue moves
the issue's — so a stale refusal can be a false alarm; re-reading costs one
call and the refusal is never wrong in the direction that loses text. The CLI
offers `--if-match <updated_at>` on the edit verbs and sends nothing without it.

## Who may do what

The permission model is the coarse one of VISION 12, with one dividing line,
one role and project access:

- **An agent works in projects; a user administers them.** An agent may create,
  read, change, comment on, close, claim and delete issues, epics, pages, labels
  and questions in every project, and read projects and its own identity. It may
  not create or change a project, a user, an agent or a token, and may not
  list agents or tokens ([ADR 0015](./adr/0015-a-token-is-an-agent-or-a-users-key-and-an-agent-is-never-an-administrator.md)).
- **A user may do everything an agent may**, plus create projects and change
  their switches, create agents and their own tokens, rename and revoke agents
  they own, and list agents.
- **An administrator** may in addition create users, rename and revoke any
  agent, and delete projects. Whoever bootstrapped the instance is one.
- **Project access belongs to a user.** The user and all their agents see and
  work in exactly those projects. Administrators manage assignments but the role
  grants no implicit content access. A hidden blocker is
  `{ "key": null, "status": null, "open": true }`: it still affects
  workability without revealing project data. Every list, search, direct key,
  export, `next` and `needs-you` read uses the same project scope.
- **A deactivated user cannot authenticate**, through a browser session, user
  token or owned agent. Reactivation restores credentials that were not
  separately revoked. At least one active administrator must remain.

Where a rule differs between a user and an agent, the endpoint below says so.

## Endpoints

### The instance and the caller

| method | path | who | does |
|---|---|---|---|
| `GET` | `/version` | anyone | `{ "version": "0.3.0" }`, no authentication |
| `GET` | `/me` | any | the caller: `IdentityRef` plus `administrator`, `email` (for a user), `owner` (for an agent) and the presented `token` reference for Bearer authentication; `token` is null for a browser session |

### Users, agents and tokens

| method | path | who | does |
|---|---|---|---|
| `POST` | `/users` | administrator | `{ name, email, administrator? }` → 201 `User`; creates an invited user and sends the activation link. No user token is created |
| `GET` | `/users` | administrator | every user, including invited and deactivated; no pagination, the list is people |
| `POST` | `/users/{id}/invitation` | administrator | replace the live invitation and send a new link; invited users only → 202 |
| `POST` | `/users/{id}/deactivate` | administrator | suspend the user, revoke every browser session, and prevent their user and agent tokens from authenticating |
| `POST` | `/users/{id}/reactivate` | administrator | reactivate a deactivated user; separately revoked tokens stay revoked |
| `PATCH` | `/users/{id}` | administrator | `{ administrator }` — grant or revoke the instance role; demoting the last active administrator is refused |
| `POST` | `/me/email` | user | `{ email }` — send a one-hour confirmation link to the new, still-unused address → 202 |
| `POST` | `/email-changes/confirm` | anyone | `{ secret }` — consume the link and make its address effective → 204 |
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
| `POST` | `/projects` | user | `{ key, name, triage_required?, review_required? }` → 201 `Project`, with the `kind` label group and the creator's project access created |
| `GET` | `/projects` | any | every project the caller sees; no pagination |
| `GET` | `/projects/{key}` | any | `Project` |
| `PATCH` | `/projects/{key}` | assigned user | `{ name?, triage_required?, review_required? }`; the key is immutable |
| `DELETE` | `/projects/{key}` | administrator | soft delete of the project and everything in it; 204. The CLI asks for the key to be typed; the API does not |
| `POST` | `/projects/{key}/restore` | administrator | back, with everything in it, into whatever state it was |

### Labels

| method | path | who | does |
|---|---|---|---|
| `GET` | `/projects/{key}/labels` | any | every label of the project with `name`, `group`, `description`; no pagination |
| `POST` | `/projects/{key}/labels` | any | `{ name, group?, description? }` → 201 |
| `PATCH` | `/projects/{key}/labels/{name}` | any | `{ name?, group?, description? }`. Changing the group is refused with `validation` when an issue or an epic would end up with two labels of the new group; `issues` and `epics` list them |
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

The `POST` body additionally accepts `wait`, a positive number of seconds. It
first checks for an issue, then waits and checks again after every project
change, returning the ordinary empty answer when the deadline passes. The
server accepts at most 3600 seconds; clients may continue in another request.

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

`ready` is written by anybody who may change the issue, whatever the caller's
kind and whatever the project's triage switch says: the switch decides what
`next` hands out, not who writes the flag (ADR 0019). The history records who
did.

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
| `GET` | `/questions/{id}` | any | one question; `wait` optionally holds the request until it is answered or the deadline passes |

This is the "are there open questions?" of VISION 7 as a list. The full "needs
you" list, with the blocker-chain rule, is `GET /projects/{key}/needs-you`.

### Epics

| method | path | who | does |
|---|---|---|---|
| `POST` | `/epics` | any | `{ project, title, description?, labels? }` → 201 `Epic`; `labels` has its groups enforced as on an issue |
| `GET` | `/epics` | any | a page of `EpicSummary`: `project`, `status` (`open` default, or `closed`, or `all`), `label`, `cursor`, `limit`; newest first |
| `GET` | `/epics/{key}` | any | `Epic` |
| `PATCH` | `/epics/{key}` | any | `{ title?, description?, labels? }`, `If-Match` honoured; `labels` replaces the whole set, groups enforced as on an issue; the description is the living document of VISION 7, and the history records that it changed |
| `POST` | `/epics/{key}/close` | any | `closed`, `closed_at` set, whatever is still open — the response carries `progress`, and the CLI lists what is open and offers to cancel or park it. Gates nothing (VISION 7) |
| `POST` | `/epics/{key}/reopen` | any | back to `open` |
| `DELETE` | `/epics/{key}` | any | soft delete, refused with `has-issues` while any issue, deleted ones included, references it |
| `POST` | `/epics/{key}/restore` | any | back |

### Pages

| method | path | who | does |
|---|---|---|---|
| `GET` | `/projects/{key}/pages` | any | every page of the project as `PageSummary`, by slug, without the bodies; `q` is the full-text filter over title and body, `label` repeatable and all must match. Not paginated |
| `GET` | `/projects/{key}/pages/{slug}` | any | `Page` |
| `POST` | `/projects/{key}/pages` | any | `{ slug, title, body?, labels? }` → 201 `Page`; the slug is given, never derived from the title (ADR 0021) |
| `PATCH` | `/projects/{key}/pages/{slug}` | any | `{ slug?, title?, body?, labels? }`, `If-Match` honoured; `slug` renames, `body` set to `null` empties the document, `labels` replaces the whole set with the groups enforced as on an issue |
| `DELETE` | `/projects/{key}/pages/{slug}` | any | soft delete; 204 |
| `POST` | `/projects/{key}/pages/{slug}/restore` | any | back, under the slug it kept |

The page sits under the project rather than at `/pages` because it is named
within a project instead of carrying a key that already says which one — the
same place labels and releases have, for the same reason.

**The list is not paginated and takes no cursor.** The wiki is flat by decision
(VISION 7), a project's pages are few, and what would make a list expensive is
the body, which is not in it. The full-text search is what replaces navigation
here, not a page of results.

**A taken slug is `validation` on `slug`**, and a slug belonging to a deleted
page says so — it stays spent until the purge, so that a restore can never land
on a name somebody else took in the meantime. A slug that could not be one
(`Not A Slug`) is `not-found` rather than `validation` when it arrives in the
path: nothing is named there, and the path is not a field.

**A rename leaves nothing behind.** The old address is gone the moment the
`PATCH` returns, nothing forwards, and the history's `slug` entry with both
values is the one place the old name survives (ADR 0021).

There is no comment endpoint and no history endpoint on a page. Whoever has
something to do makes a ticket, which is what keeps the discussion in one place;
the history is written and read from the database, as an epic's is.

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
| `PATCH` | `/projects/{key}/releases/{name}` | any | `{ name?, description? }` — a field left out is left alone. The notes are a living document until published, and editable after, because a record can be annotated. `name` corrects the newest publication only: an older one or the open release is `transition`, a name the project has is `release-exists` |
| `POST` | `/projects/{key}/releases/publish` | any | `{ name, description? }`: names the open release, freezes it, sets the date and the caller, and creates the next open one, in one transaction. 201 with the published release. A name the project has is `release-exists`; `unreleased` and `none` are reserved |
| `POST` | `/projects/{key}/releases/{name}/retract` | any | takes the newest publication back: the release is the open one again with the same issues, and the empty open release goes. `transition` where another publication followed it, where work has closed into the open release since, or where the named release was never published |
| `PUT` | `/projects/{key}/releases/{name}/issues/{issue}` | any | puts the issue into the open release by hand (VISION 7). The answer is the release. `in-published-release` where the named release is published or the issue shipped already |
| `DELETE` | `/projects/{key}/releases/{name}/issues/{issue}` | any | takes the issue out of the open release: it has not shipped yet and does not belong. The answer is the release. `in-published-release` where the named release is published |

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

Within each group, higher priority comes first and age breaks a tie. The page
uses the ordinary `cursor` and `limit` query parameters and always carries an
ETag, including when it is empty.

Beside the list the answer carries `agents`: how many agents could pick work up
at all. At `0` nothing on this list will be worked off, whatever it says — and
the thing to do about it is to create an agent token, one act that has nothing
to do with any issue on the list. That is why it is said once beside the list
rather than turned into entries on it; it is VISION 10's "an empty result
explains itself" for this list. It is part of the ETag, so a long poll is told
when the instance gains or loses its last agent. Until project assignment
arrives in cut three, agents can work in every project, so the number is the
instance's unrevoked agent tokens; it becomes project-scoped later and the
shape does not change.

**Stuck** is the blocker-chain rule of VISION 10: a blocked issue is on the
list only when a chain of open blockers from it ends in a dead end — an issue
that is parked, or has an open question — because a blocker an agent will pull
needs nobody and is noise here. The predicate is one recursive query over the
open edges, evaluated in SQL beside `Workable`. A **parked issue is never on
the list as `stuck` itself**: parking is the explicit decision that it is not
up yet, so there is nothing there for a human to resolve. Waiting *behind* a
parked blocker still counts, and a parked issue with an open question is on the
list as `question`. The CLI: `pa needs-you`, which prints the four groups under
their headings.

### Waiting

`wait`, in seconds, on three reads — one mechanism, three doors (VISION 6.1):

| on | as | returns when |
|---|---|---|
| `POST /projects/{key}/next` | `wait` in the body | an issue was handed out, or the deadline passed — then the same `{ issue: null, reasons }` as without `wait` |
| `GET /questions/{id}` | `wait` in the query | the question was answered, or the deadline passed — then the question as it is |
| `GET /projects/{key}/needs-you` | `wait` in the query, with `If-None-Match` carrying the `ETag` of the last answer | the list differs from the one the caller has, or the deadline passed — then `304` |

The query is run first. `next` waits only while it finds nothing, a question
only while it is open, and `needs-you` while its ETag still matches
`If-None-Match` (an absent validator establishes the empty page as its
baseline). Waiting happens on the project's notification channel
(`docs/storage.md`, Wake-ups); every notification re-runs the original query.
At the deadline, `next` returns its empty answer, a question returns still
open, and an unchanged `needs-you` returns `304`. `wait` is at most `3600`; a
larger value is `wait-too-long`, and the CLI's `--wait` takes any number of
seconds and asks in rounds. What an operator's proxy has to allow is in
`docs/operations.md`.

The CLI: `pa next --claim --wait S`, `pa issue ask KEY "…" --wait S` — which holds
the claim and waits for the answer, for at most the rest of the claim (VISION
10) — and `pa needs-you --wait S`.

### Search

`q` on `GET /issues`, `GET /questions` and `GET /projects/{key}/pages`: a
full-text filter in the words a search box takes — `claim expired`,
`"for update"`, `-flaky` — matched with `websearch_to_tsquery` against the
`simple` configuration, so identifiers survive (`docs/storage.md`, Full-text
search). On issues it matches the title, the description, the result, and the
issue's comments and questions; on questions the question and its answer; on
pages the title and the body. A filter, not a ranking: the list keeps its
order. The CLI: `pa issue list -q "…"`, `pa question list -q "…"`,
`pa page list -q "…"`.

**A page has to be findable this way**, more than anything else here does: the
wiki is flat because the search is what replaces the navigation a hierarchy
would have given it (VISION 7). The command palette therefore asks both lists
and shows the hits under headings that say which is which, because a hit that
does not say what kind of thing it is is a poor hit. Deleted pages are absent
from it as they are from every other read (ADR 0013).

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

## What cut three adds

### Browser identity

| method | path | who | does |
|---|---|---|---|
| `POST` | `/session` | anyone | `{ email, password }` → 204 and a browser cookie. Unknown user, wrong password and inactive user have the same `unauthenticated` response and timing class |
| `DELETE` | `/session` | browser user | revoke the server-side session and expire the cookie; 204 |
| `POST` | `/session/bootstrap` | anyone | `{ token, password }` → 204 and a browser cookie; once per bootstrap user token, and never stores that token in the browser |
| `POST` | `/invitations/accept` | anyone | `{ secret, password }` → 204 and a browser cookie; consumes the invitation and activates the user |
| `POST` | `/password-recovery` | anyone | `{ email }` → 202 in every case; sends a one-hour link only for an active matching user |
| `POST` | `/password-recovery/complete` | anyone | `{ secret, password }` → 204; consumes the secret, changes the password and revokes every browser session |
| `GET` | `/sessions` | user | the caller's `BrowserSession` values, current first |
| `DELETE` | `/sessions/{id}` | user | revoke one of the caller's sessions; 204 |
| `DELETE` | `/sessions` | user | revoke every session except the current one; 204 |
| `POST` | `/me/password` | user | `{ current_password, password }` → 204; revokes every other session |
| `PATCH` | `/me` | user | `{ name }` → 200 `User`; email and password have their own confirmation-aware acts |
| `POST` | `/me/email` | user | `{ email }` → 202; sends a confirmation link to the new address while the old remains active |
| `POST` | `/me/email/confirm` | user | `{ secret }` → 200 `User`; consumes the link and changes the address |

Login failures are throttled over 15 minutes after five attempts for a normalized
account or 20 for a source address. The public recovery response is deliberately
indistinguishable for unknown, invited, deactivated and active addresses. If
SMTP itself is absent it returns `smtp-not-configured` for every address.
Passwords never appear in response bodies or logs.

### User lifecycle and project access

| method | path | who | does |
|---|---|---|---|
| `POST` | `/users/{id}/invitation` | administrator | replace the live invitation and resend it; 202 |
| `POST` | `/users/{id}/deactivate` | administrator | set `deactivated`, revoke all sessions and suspend user and owned agent authentication; the last active administrator is protected |
| `POST` | `/users/{id}/reactivate` | administrator | set `active`; separately revoked tokens and agents stay revoked |
| `PATCH` | `/users/{id}` | administrator | `{ administrator? }`; changing name or email for oneself uses the personal endpoints; the last active administrator is protected |
| `GET` | `/projects/{key}/users` | assigned user or administrator | assigned users as `User`; an administrator need not have project access for this administrative metadata |
| `PUT` | `/projects/{key}/users/{id}` | administrator | grant project access; 204 and idempotent |
| `DELETE` | `/projects/{key}/users/{id}` | administrator | remove project access; 204 and idempotent |

Project access is checked before loading project content. A caller without it
gets `not-found`, not `forbidden`, so keys cannot be probed. The only exceptions
are the administrator's explicit project/user assignment endpoints and the list
of all projects including deleted ones below; neither returns project content.
An agent resolves access through its owner and never receives an administrator
role.

`GET /projects` remains the content list and returns only assigned projects.
`GET /admin/projects?deleted=true|false|all` lists project identity and deletion
metadata for administrators, without issues, epics, releases or labels.

### SMTP administration

| method | path | who | does |
|---|---|---|---|
| `GET` | `/admin/smtp` | administrator | `SmtpStatus`: configured state and non-secret connection facts |
| `POST` | `/admin/smtp/test` | administrator | `{ email }` → 202 after sending a test message; the address becomes optional and defaults to the caller's address once user email is persisted |

Sending is synchronous up to acceptance by the configured SMTP server. Failure
returns a problem response and is logged without credentials, secrets or message
bodies. There is no delivery queue or automatic retry
([ADR 0018](./adr/0018-transactional-email-is-an-optional-instance-capability.md)).

### Filters for the shared issue list

`GET /issues` accepts the complete cut-three list state: `project`, `q`, one or
more `status`, `priority`, `label`, `epic`, `assignee`, `claimed`, `author`,
`blocked`, `ready`, `sort`, `order`, `cursor` and `limit`. Filter values for
labels, epics, users and agents come from their existing server collections; the
browser does not reconstruct them from loaded rows. Ready and In progress are
named client presets, not new endpoints. Needs you retains its own endpoint and
row reason.

The default issue order for the Ready preset is the business order used by
`next`; other issue lists default to most recently updated. Alternative sorts
are updated, created, priority and epic. A cursor binds every filter and
ordering choice, so changing URL state starts a new page sequence.
