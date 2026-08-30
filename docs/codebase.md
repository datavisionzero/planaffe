# The Codebase

`VISION.md` describes what planaffe does. This one describes where that will
live: how the repository is laid out, which project holds what, which way the
dependencies point, and what is built by which toolchain.

**Nothing under `src/` exists yet.** This document is the plan the first commits
follow, and it is kept accurate from then on — a file that lands somewhere this
document does not describe means one of the two is wrong.

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
│  └─ api/openapi.json        the HTTP contract, checked in
├─ deploy/                    the Dockerfile, Compose, and nothing else
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
order it sorts in, the seven conditions of readiness, the claim and its expiry,
the label and the group that admits one value at a time, the epic as a bracket,
the release as a record rather than a plan, the question that is a state rather
than a comment. The test of whether something belongs here is stated in ADR
0002: **anything the vision already states as a rule.** A claim that can be
constructed without an expiry, or a status that can reach `done` without closing
the issue, is a rule that escaped.

**`Planaffe.Application` holds the use cases and the ports.** Creating issues —
several related ones in one act, which is the moment the vision calls the most
important (VISION 10) — reading an issue as the context package an agent gets,
taking the next ready issue and claiming it in one operation, releasing it,
asking and answering a question, closing with a result, publishing a release,
searching and filtering, the token and project acts. Beside them the ports:
stores for the rows, the identity of the caller, the id source, the wake-up
channel a waiting client listens on, and the clock — which is `TimeProvider` from
the base class libraries rather than a port of ours.

**`Planaffe.Infrastructure` answers those ports.** EF Core declares the tables
and owns the self-applying migrations, so there is exactly one place that creates
schema. The acts that have to be atomic are written as the conditional updates
they are, in one transaction, close to the SQL rather than assembled by a
caller — claiming is the whole reason this product exists (VISION 11). Waiting is
`LISTEN`/`NOTIFY` on its own connection outside any pool, with a deadline as the
fallback (VISION 13). The two log sinks live here as well
([ADR 0008](./adr/0008-planaffe-logs-into-logaffe-and-serilog-is-the-way-out.md)).

**`Planaffe.Api` is the adapters and the composition root.** The HTTP endpoints,
authentication of a session and of an agent token, the rate limits, the static
files of the built SPA, and — later — the MCP server, which will be a second
adapter over the same use cases and not a second way into the data.

## The CLI is a client, not a layer

`src/cli/` is an ordinary Go module. It references nothing in `src/` and knows
the installation only through the generated client of
`docs/api/openapi.json` ([ADR 0005](./adr/0005-the-contract-is-checked-in-and-both-clients-are-generated-from-it.md)).
It ships as its own release artifact — a static binary per platform, built by the
release workflow — and is versioned with the server it was cut from.

Operational verbs that need the database are **not** here: migrations, backups
and the first account belong to the .NET binary that has the connection string.
`pa` is a client of the public API and nothing else, which is what lets it run
from a laptop, a CI runner or an agent's container.

## The frontend is built separately and joined once

`src/web/` is an ordinary Vite project with its own `package.json`, and nothing
in the .NET build knows it exists. Development runs the two side by side — the
Vite dev server against `dotnet run` — and the only place they are joined is the
`Dockerfile`, which builds the SPA in a Node stage and copies it into the
published output.

Its own layout follows the shell
([ADR 0006](./adr/0006-the-web-application-is-a-shell-before-it-is-a-screen.md)):
one folder per area — `shell`, `issues`, `epics`, `releases`, `projects`,
`settings`, `session`, `shared`, `api` — where each area owns its routes.

## The HTTP contract is an artifact, not an intention

`docs/api/openapi.json` is checked in, captured from a running installation, and
verified by CI against the document the installation serves. Both clients are
generated from it before every build, typecheck and test, and neither generated
output is committed
([ADR 0005](./adr/0005-the-contract-is-checked-in-and-both-clients-are-generated-from-it.md)).

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

## What is deliberately not here

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
