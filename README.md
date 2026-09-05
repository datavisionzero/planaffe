# planaffe

A lean, self-hosted issue tracker for software development in the agentic age.
Humans work in a web interface and agents work through a CLI, both over the same
API, and the one act at the centre of it — hand out the next workable issue and
claim it, atomically — is what the product is built around.

It is opinionated on purpose: few fields, one fixed workflow, no configuration.
See [VISION.md](VISION.md) for what planaffe is and, just as importantly, what it
deliberately is not.

## Who it is for

Solo developers and small teams who host their own tools and get most of their
implementation work done with AI agents. Whoever uses `git`, `gh` and a coding
agent every day should feel at home; enterprise portfolio management and
helpdesk work are not what this is for.

## Status

**Pre-release, nothing published yet.** The product is being built in three cuts
([ADR 0009](docs/adr/0009-the-mvp-is-built-in-three-cuts.md)), and all three
are standing:

- **Cut one, the backend**, exists and is tested against a real Postgres: users,
  agents and tokens; projects and labels; issues with their bulk create, the
  two shapes, the guarded change and the edges; the claim, its expiry and its
  takeover; `next`; close, review, reopen and parking; comments, questions and
  the history; epics; soft deletion with the purge; the `Idempotency-Key`.
- **Cut two, unattended operation**: sub-issues and the recursive blocker-chain
  rule, `--wait` on `LISTEN`/`NOTIFY` for `next`, for a question's answer and
  for the paginated `needs-you` list, releases, full-text search, the agent
  metadata back channel, bulk changes, and `pa export --json`. The CLI (`pa`)
  covers all of it, and so do the container image and the Compose file.
- **Cut three, the interface for humans**: every screen of the matrix in
  [`docs/human-interface.md`](docs/human-interface.md) is built — the shell with
  its project switcher, command palette and shortcuts, the shared issue list
  with its filters in the URL, the issue, epic and release screens, needs-you,
  labels, and the three administration areas. Bulk changes, export and the
  waiting operations stay with the CLI on purpose.

What is left is not a missing area but the polish inside one: the screens are
sharpened ticket by ticket until 1.0 is worth publishing.

The contract is [`docs/api/openapi.json`](docs/api/openapi.json), captured from
a running instance and checked in; [`docs/api.md`](docs/api.md) describes it in
prose and [`docs/storage.md`](docs/storage.md) what it stands on.

## Running it

**Have your agent do it.** Give it this address and nothing else:

```
https://github.com/datavisionzero/planaffe/blob/main/docs/install.md
```

[`docs/install.md`](docs/install.md) is a sequence from nothing to the first
ticket, written for an agent to execute: one command per step, the output that
means it worked, and the condition under which it stops and asks you. The four
things only a person can decide — the administrator's email, a password, DNS, a
certificate — are marked as exactly that. It names no agent and no harness.

Or type it yourself. It is two commands:

```sh
cp deploy/.env.example deploy/.env      # set POSTGRES_PASSWORD and the three bootstrap values
docker compose -f deploy/docker-compose.yml up -d
```

The instance applies its own migrations and, on the first start, creates the
first administrator and their token from `PLANAFFE_BOOTSTRAP_ADMIN`,
`PLANAFFE_BOOTSTRAP_EMAIL` and `PLANAFFE_BOOTSTRAP_TOKEN`. From there, `pa init`
connects a repository to the instance and `pa issue create` is the fifth minute;
[`docs/cli.md`](docs/cli.md) has the CLI and
[`docs/operations.md`](docs/operations.md) every variable, the upgrade and the
backup.

## Working on it

The .NET 10 SDK and Docker. The integration tests bring up their own Postgres
with Testcontainers; the unit tests need nothing installed.

```
dotnet build Planaffe.slnx
dotnet test tests/Planaffe.UnitTests
dotnet test tests/Planaffe.IntegrationTests
```

[`docs/codebase.md`](docs/codebase.md) says where everything lives and which way
the dependencies point; [`docs/adr/`](docs/adr/) holds the decisions;
[`CONTEXT.md`](CONTEXT.md) is the language the code, the contract and the CLI
speak. Contributions arrive as pull requests from forks
([`CLAUDE.md`](CLAUDE.md) has the house rules).

## Security

planaffe is meant to be reachable by the agents and people that use it, over the
network. To report a vulnerability, see [SECURITY.md](SECURITY.md).

## License

planaffe is released under the MIT License. See [LICENSE](LICENSE).

## Trademark

"planaffe" is a trademark of datavisionzero. The MIT License covers the source
code and grants no rights to the project name or logo. Forks and derivative
works are welcome, but please distribute them under a different name so that
users can tell whose software they are running.
