# Operations

How an instance is run, upgraded and configured. Operations are meant to consist
of Postgres backups and nothing else (VISION 16); everything else here is one
command.

## Running

```sh
git clone https://github.com/datavisionzero/planaffe
cd planaffe
cp deploy/.env.example deploy/.env      # set POSTGRES_PASSWORD and the two bootstrap values
docker compose -f deploy/docker-compose.yml up -d
```

Two services come up: the instance and its Postgres. The instance applies its
migrations, creates the first administrator and their token from the two
bootstrap variables, and listens on port 8080. `GET /version` answers without a
token; everything else takes `Authorization: Bearer <token>`.

The same port serves the web application: open it in a browser and paste a
user token — the bootstrap token, or one from `pa token create`. It stays in
that browser until signed out or revoked; the instance keeps no session.

The bootstrap token is the first administrator's user token — what the CLI
carries as `PLANAFFE_TOKEN`. The first agent gets its own token from that
administrator, one command later (`POST /agents`, `pa agent create`).

The three waiting commands (`pa next --claim --wait`, `pa issue ask --wait`,
and `pa needs-you --wait`) hold one HTTP request open for as long as one hour.
A reverse proxy in front of the instance must therefore allow request and
upstream-response timeouts of at least 3610 seconds. The client adds another
30 seconds for transport overhead; no inbound connection to the client is
needed.

## Upgrading

Migrations run only forward; there is no downgrade path (ADR 0011). The way
back is the backup taken before the upgrade, so it comes first:

```sh
docker compose -f deploy/docker-compose.yml exec db pg_dump -U planaffe planaffe > planaffe-$(date +%F).sql
docker compose -f deploy/docker-compose.yml pull
docker compose -f deploy/docker-compose.yml up -d
```

An instance started against a database a newer version has already migrated
refuses to start and says so, rather than serving a shape it misunderstands.

## Variables

Set in `deploy/.env`; read once, at start.

| variable | required | default | meaning |
|---|---|---|---|
| `POSTGRES_PASSWORD` | yes | | the database password, shared by both services |
| `PLANAFFE_BOOTSTRAP_ADMIN` | first start | | the name of the first administrator |
| `PLANAFFE_BOOTSTRAP_TOKEN` | first start | | their user token, at least 32 characters; shorter refuses the start |
| `PLANAFFE_CLAIM_EXPIRY_HOURS` | no | `4` | how long an agent's claim lives without a write of the holder's (VISION 11); a user's never expires |
| `PLANAFFE_DELETION_GRACE_DAYS` | no | `7` | how long a deleted issue, epic, label or project can be restored before the purge may take it (ADR 0013); a floor, not a deadline |
| `PLANAFFE_LOG_ENDPOINT` | no | | a logaffe instance to log into, scheme and host; set together with the token (ADR 0008) |
| `PLANAFFE_LOG_TOKEN` | no | | the ingest token of the logaffe project the entries belong to |
| `PLANAFFE_LOG_LEVEL` | no | `Information` | the floor: `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal` |
| `PLANAFFE_PORT` | no | `8080` | the host port the instance is published on |
| `PLANAFFE_IMAGE` | no | `ghcr.io/datavisionzero/planaffe:main` | the image; name a `sha-<commit>` tag to pin an installation |

The two bootstrap variables are ignored on every start after the first — the
instance already has identities, and a bootstrap happens once. Changing the
token in the environment changes nothing; a lost token is recovered through the
server binary, which is not in cut one.

Inside the container the connection string is `ConnectionStrings__Postgres`,
which the Compose file assembles from the password; a deployment that is not
this Compose file sets it directly.

## Logging

The instance logs to the console always — the container's log — and, with
`PLANAFFE_LOG_ENDPOINT` and `PLANAFFE_LOG_TOKEN` set, into logaffe as well;
without them, into a rolling file under `/app/logs`, a day per file, seven files
kept. A logaffe that cannot be reached never becomes an outage: entries queue
in memory, the oldest are dropped under pressure, and what could not be
delivered is a line on standard error. No log line carries a request body.

## Backups

The database is everything. `pg_dump` of it, on whatever schedule the
installation deserves, is the whole of operations:

```sh
docker compose -f deploy/docker-compose.yml exec db pg_dump -U planaffe planaffe > planaffe.sql
```

## Development

Postgres alone, for `dotnet run`:

```sh
docker compose -f deploy/docker-compose.dev.yml up -d
dotnet run --project src/Planaffe.Api
```

`appsettings.Development.json` carries the matching connection string and a
bootstrap administrator with a token of no consequence.
