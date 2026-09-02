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
([ADR 0009](docs/adr/0009-the-mvp-is-built-in-three-cuts.md)), and the first
cut is the backend:

- The HTTP API of cut one exists and is tested against a real Postgres: users,
  agents and tokens; projects and labels; issues with their bulk create, the
  two shapes, the guarded change and the edges; the claim, its expiry and its
  takeover; `next`; close, review, reopen and parking; comments, questions and
  the history; epics; soft deletion with the purge; the `Idempotency-Key`.
- The CLI (`pa`) covers the whole of cut one — `next`, the issue verbs and the
  acts, questions, projects, labels, epics, identities — and the container image
  and the Compose file exist.
- The web application is a shell: navigation over the seven views, project
  switcher, account menu, command palette, one route per view with the filter
  in the URL, a drawer on a phone, and Markdown rendered in the browser. The
  screens behind the views arrive one ticket at a time.

The contract is [`docs/api/openapi.json`](docs/api/openapi.json), captured from
a running instance and checked in; [`docs/api.md`](docs/api.md) describes it in
prose and [`docs/storage.md`](docs/storage.md) what it stands on.

## Running it

One `docker compose up`, against the image the trunk publishes:

```sh
cp deploy/.env.example deploy/.env      # set POSTGRES_PASSWORD and the two bootstrap values
docker compose -f deploy/docker-compose.yml up -d
```

The instance applies its own migrations and, on the first start, creates the
first administrator and their token from `PLANAFFE_BOOTSTRAP_ADMIN` and
`PLANAFFE_BOOTSTRAP_TOKEN`. From there, `pa project create` and `pa issue
create` are the fifth minute — once the CLI exists. Until then the API is what
there is; [`docs/operations.md`](docs/operations.md) has every variable, the
upgrade and the backup.

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
