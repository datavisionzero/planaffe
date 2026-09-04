# The Codebase

`VISION.md` describes what planaffe does. This one describes where that will
live: how the repository is laid out, which project holds what, which way the
dependencies point, and what is built by which toolchain.

**`src/` holds the four .NET projects, the Go CLI and the web application.**
Domain carries the types of cut one, named
after `CONTEXT.md`; Infrastructure carries the schema (`Persistence/`: the
context, one configuration per table, the migrations and the migrator that
applies them); Api is the host that runs the migrator before it serves. The
skeleton was built before there was anything to break so that CI could be green
from the first commit (ADR 0001). This document is kept accurate from here on: a
file that lands somewhere it does not describe means one of the two is wrong.

Three decisions shape all of it: the backend is **four layers**
([ADR 0002](./adr/0002-the-backend-is-four-layers-not-one-project.md)), the
frontend is **React on its own toolchain**
([ADR 0004](./adr/0004-the-frontend-is-react-not-blazor.md)), and the CLI is
**Go** ([ADR 0003](./adr/0003-the-cli-is-go-not-a-second-dotnet-binary.md)). The
repository therefore carries three languages, and the artifact an operator runs
carries two of them.

## The shape of the repository

```
planaffe/
├─ .github/workflows/         ci on every push, release on every tag
├─ docs/                      the product, the decisions, and this
│  ├─ adr/
│  ├─ research/
│  ├─ storage.md              the data model: tables, constraints, what is derived on read
│  ├─ api.md                  the HTTP surface: endpoints, shapes, errors, exit codes
│  ├─ human-interface.md      the screens, browser actions and permission matrix
│  ├─ operations.md           running, upgrading, the variables, backups
│  ├─ cli.md                  pa: configuration, exit codes, verbs
│  └─ api/openapi.json        the HTTP contract, checked in
├─ deploy/                    the Dockerfile, Compose (production and development), and nothing else
├─ src/
│  ├─ Planaffe.Domain/        the rules
│  ├─ Planaffe.Application/   the use cases and their ports
│  ├─ Planaffe.Infrastructure/ Postgres, the notifier, the log sinks
│  ├─ Planaffe.Api/           HTTP and the composition root
│  ├─ cli/                    the Go CLI — `pa`
│  └─ web/                    the single-page application
├─ tests/
│  ├─ Planaffe.UnitTests/
│  └─ Planaffe.IntegrationTests/
└─ Planaffe.slnx              plus global.json and the Directory.* properties
```

`src/` and `tests/` is the convention a .NET contributor arrives expecting, and
the open-source intent of `VISION.md` is reason enough to meet it rather than
invent something more descriptive.

## The four layers

Dependencies point inward and only inward: Domain depends on nothing, Application
on Domain, Infrastructure on Application, and Api on both of the outer two as the
composition root. Domain carries no package references at all, which is the
cheapest possible check that nothing has leaked into it.

**`Planaffe.Domain` holds the rules.** The issue with its closed field set and
its key, the status set and what closes an issue, the priority scale and the
order it sorts in, the eight conditions of workability, the claim and its expiry,
the label and the group that admits one value at a time, the epic as a bracket,
the release as a record rather than a plan, the question that is a state rather
than a comment. The test of whether something belongs here is stated in ADR
0002: **anything the vision already states as a rule.** A claim that can be
constructed without an expiry, or a status that can reach `done` without closing
the issue, is a rule that escaped.

**Every type here is a term in [`CONTEXT.md`](../CONTEXT.md)**, spelled the way
that file spells it. This is the layer the glossary is written for: a type named
after an `_Avoid_` word is a naming bug rather than a preference, and a concept
that needs a name the glossary does not have gets added there first.

**`Planaffe.Application` holds the acts and the ports** — `Acts/` and `Ports/`,
named as the vision names them: an act is one thing a caller does, a port is
one thing the acts need answered. Creating issues —
several related ones in one act, which is the moment the vision calls the most
important (VISION 10) — reading an issue as the context package an agent gets,
taking the next ready issue and claiming it in one operation, releasing it,
asking and answering a question, closing with a result, publishing a release,
searching and filtering, the token and project acts. Beside them the ports:
stores for the rows, the identity of the caller, the id source, the wake-up
channel a waiting client listens on, and the clock — which is `TimeProvider` from
the base class libraries rather than a port of ours.

The agent metadata back channel is one of those acts: an agent changes only its
own last report, while Infrastructure writes the same snapshot to its history
in the same transaction. The history deliberately has no read port yet; cut two
keeps it for later without opening a second surface before one is needed.

**`Planaffe.Infrastructure` answers those ports.** EF Core declares the tables
and owns the self-applying migrations, so there is exactly one place that creates
schema. A migration is added with the pinned tool in `.config/dotnet-tools.json`
— `dotnet tool restore`, then `dotnet ef migrations add <Name> --project
src/Planaffe.Infrastructure` — without a running instance anywhere; what the
model cannot say, the `issue_read` view and the expression index on an
identity's name, is SQL inside the migration that created it. The acts that have to be atomic are written as the conditional updates
they are, in one transaction, close to the SQL rather than assembled by a
caller — claiming is the whole reason this product exists (VISION 11). The two
rules that are derived on read rather than written — an expired claim, and a
soft-deleted row ([ADR 0013](./adr/0013-deleting-is-a-soft-delete-with-a-floor-and-identities-are-never-deleted.md)) —
live here in one place each, because a query that forgets either of them is how
both decisions fail. Waiting is
`LISTEN`/`NOTIFY` on its own connection outside any pool, with a deadline as the
fallback (VISION 13). The two log sinks live here as well
([ADR 0008](./adr/0008-planaffe-logs-into-logaffe-and-serilog-is-the-way-out.md)).

**`Planaffe.Api` is the adapters and the composition root.** `Http/` holds the
endpoints, bearer and browser-session authentication that answer the caller port, the version
header and the one place a refusal becomes a problem document; `Hosting/` the
services that run before anything is served — the migrations, the bootstrap.
It also owns browser-session authentication, CSRF and login rate limits, the
central direct-key project-scope door, the static files of the built SPA and
SMTP composition around the application's email port. Collection acts carry
the same scope into their store queries, so search and unfiltered lists cannot
step around it. The later MCP server will be a second adapter over the same acts and
not a second way into the data.

## The CLI is a client, not a layer

`src/cli/` is an ordinary Go module (`cmd/pa` the binary, `internal/` the
packages). It references nothing in `src/` and knows the installation only
through the generated client of `docs/api/openapi.json`
([ADR 0005](./adr/0005-the-contract-is-checked-in-and-both-clients-are-generated-from-it.md)),
produced into `internal/api/` by `go generate` and never committed;
[`cli.md`](./cli.md) has the rest.
It ships as its own release artifact — a static binary per platform, built by the
release workflow — and is versioned with the server it was cut from.

Operational verbs that need the database are **not** here: migrations, backups
and the first account belong to the .NET binary that has the connection string.
`pa` is a client of the public API and nothing else, which is what lets it run
from a laptop, a CI runner or an agent's container.

## The frontend is built separately and joined once

`src/web/` is an ordinary Vite project with its own `package.json`, and nothing
in the .NET build knows it exists. It is drawn by Tailwind and Base UI in
components the repository owns
([ADR 0017](./adr/0017-the-web-application-is-drawn-by-tailwind-and-base-ui-components-the-repository-owns.md)). Development runs the two side by side — the
Vite dev server against `dotnet run` — and the only place they are joined is the
`Dockerfile`, which builds the SPA in a Node stage and copies it into the
published output.

Its own layout follows the shell
([ADR 0006](./adr/0006-the-web-application-is-a-shell-before-it-is-a-screen.md)):
one folder per area — `shell`, `issues`, `epics`, `releases`, `projects`,
`settings`, `session`, `shared`, `api` — where each area owns its screens and
`shell/Shell.tsx` owns the routes. `components/ui/` is what the shadcn CLI
generated and `index.css` is the token layer; both are the repository's to
edit. `api/client.ts` is the one way to the instance — `openapi-fetch` over the
types `npm run generate` writes from the contract — and it adds the CSRF proof
to cookie-authenticated writes.

The application signs in with email and password and keeps only an opaque
session cookie (`session/`). The bootstrap user token may be exchanged once to
set the first administrator's password, but is never kept in browser storage.
Bearer tokens remain the CLI and direct API door (ADR 0015). A local `npm run build` lands in
`src/Planaffe.Api/wwwroot/`, which the API serves with every path no endpoint
took falling back to `index.html`, so that `/PLAN/ready` is a link that works.

## The HTTP contract is an artifact, not an intention

`docs/api/openapi.json` is checked in, captured from a running installation, and
verified by CI against the document the installation serves. Both clients are
generated from it before every build, typecheck and test, and neither generated
output is committed
([ADR 0005](./adr/0005-the-contract-is-checked-in-and-both-clients-are-generated-from-it.md)).

A change to an endpoint is a change to the document, in the same commit. The
integration test `ContractTests` compares what the instance serves with the
checked-in file, structurally, and fails until they agree; regenerating is the
same test with the switch that writes the file:

```sh
PLANAFFE_CAPTURE_CONTRACT=1 dotnet test tests/Planaffe.IntegrationTests --filter ContractTests
```

It writes the same bytes CI's capture step would, so the two never differ by
formatting.

## Tests are split by what they need

**`Planaffe.UnitTests`** runs in seconds and needs nothing installed: the rules
of Domain and the use cases of Application against substituted ports.
**`Planaffe.IntegrationTests`** brings up Postgres with Testcontainers, because
the parts no substitute can vouch for are precisely the ones this product is
about — that two concurrent claims produce exactly one winner, that an expired
claim makes an issue selectable again, that readiness is evaluated as the vision
states it, that migrations apply, and that a waiting client is woken. The split
is by what a test needs rather than by what it covers, because that is the
distinction CI has to act on.

The frontend carries its own tests inside `src/web/`, and the CLI its own inside
`src/cli/`, each run by the CI job that builds it.

## One workflow, and it is the gate

`.github/workflows/ci.yml` runs on every push to `main`, every pull request and
on demand. There is no review step and no environment between a commit and a
release ([ADR 0001](./adr/0001-the-repository-is-a-trunk.md)), so that workflow
is the only thing standing between a mistake and the trunk: unit tests,
integration tests on Testcontainers, the web build, the CLI build, and the
contract check that fails when the installation serves a document other than the
one checked in. A trunk commit that passes all of them publishes the image to
`ghcr.io/datavisionzero/planaffe` under `:main` and under the commit; a pull
request builds it and pushes nothing.

The jobs whose subject does not exist yet **skip rather than fail** — the
workflow was written whole before the four toolchains were, and a first job asks
for the one file each subject cannot exist without. Nothing has to come back and
enable them: `src/cli/go.mod` arriving with the CLI is what starts the Go job.

## What is deliberately not here

- **No second issue shape beyond the two that are declared.** A list returns the
  slim issue, a single read returns the complete one, and both are named types in
  the contract ([ADR 0012](./adr/0012-a-list-returns-a-slim-issue-and-only-a-single-issue-is-complete.md)).
- **No shared types across the three languages.** Settled in ADR 0004 and ADR
  0003; the contract is `docs/api/openapi.json` and it is checked.
- **No second read path and no second write path.** Every adapter — HTTP today,
  MCP later — calls the same use cases.
- **No context split.** This is a single-context repository, and `src/` is laid
  out by layer rather than by bounded context.
- **No generated code checked in.** The two API clients are generated at build
  time; the document is the artifact, its output is not.
- **No prototypes on `main`.** Code that measures something not yet decided
  lives on its own branch and stays unmerged.
