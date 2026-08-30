# The CLI Is Go, Not a Second .NET Binary

`pa` is written in Go and ships as a single static binary per platform. The
obvious alternative, given a .NET backend, is the one the sibling project took:
the server binary is the CLI, one toolchain, one language, shared types with the
domain it talks about. Here it loses on distribution.

The vision makes the CLI the primary surface of the product (guiding principle 1)
and puts it on machines that have nothing to do with the installation — a
developer's laptop, a CI runner, a container an agent runs in. What that surface
has to be is a file somebody can drop on a PATH: no runtime, no SDK, no
prerequisite. Go cross-compiles every target from one machine with `GOOS`/`GOARCH`
and produces a static binary of a few megabytes; .NET NativeAOT produces
something comparable in size but has to be linked on a host of the target
platform, which turns a release into a build matrix and a Windows job into its
own problem. Go's release tooling and its install paths — `go install`, Homebrew,
Scoop, a tarball from a release page — are the ones this audience already has.

Two smaller things point the same way. `pa next --wait` holds an outbound
connection open for minutes and is the mechanism the whole wake-up story is built
on (VISION 15.9); a goroutine and a context deadline are the plainest possible
expression of it. And the audience of a self-hosted developer tool brings Go for
a CLI far more readily than C#.

## Consequences

**A third language in the repository**, after C# and TypeScript, with its own
toolchain, its own CI job and its own release artifacts.

**The CLI shares no types with the backend** — the same cost ADR 0004 accepts for
the frontend, answered the same way: the OpenAPI document is the contract and
the Go client is generated from it
([ADR 0005](./0005-the-contract-is-checked-in-and-both-clients-are-generated-from-it.md)).
Two generated clients out of one checked-in document is a better trade than one
shared assembly and one generated client.

**The server keeps its own verbs.** Operational acts that belong to an
installation — migrations, a backup, creating the first account — stay in the
.NET binary where the database is. `pa` is a client of the public API and nothing
else, which is exactly what makes it usable from anywhere.
