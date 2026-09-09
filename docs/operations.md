# Operations

How an instance is run, upgraded and configured. Operations are meant to consist
of Postgres backups and nothing else (VISION 16); everything else here is one
command.

Installing for the first time is [`install.md`](./install.md): a sequence from
nothing to the first ticket, written for an agent to execute. This document is
the reference beside it, and what you come back to afterwards.

## Running

```sh
git clone https://github.com/datavisionzero/planaffe
cd planaffe
cp deploy/.env.example deploy/.env      # set POSTGRES_PASSWORD and the bootstrap values
docker compose -f deploy/docker-compose.yml up -d
```

The image carries `linux/amd64` and `linux/arm64`, so `docker compose pull`
resolves the right one on an ARM machine without anybody naming it — a
Raspberry Pi, an ARM VPS and a Mac on Apple Silicon all run it natively.

By default that is `:latest`, which moves on a stable release and never on a
prerelease. `docker compose pull` therefore upgrades between releases and not
with every commit; set `PLANAFFE_IMAGE` to `:main` to follow the trunk instead,
or to a version or a `sha-<commit>` to stand still.

Three services come up: Caddy, the instance and its Postgres. The instance
applies its migrations, creates the first administrator and their token from the
three bootstrap variables, and listens on port 8080 of the Compose network,
where Caddy is the only thing that can reach it. `GET /version` answers without
authentication. The CLI and direct API use `Authorization: Bearer <token>`; the
browser uses a server-side session cookie.

Caddy in front of it is the whole of the TLS decision, and it is one variable.
Unset, `PLANAFFE_SITE_ADDRESS` is `:80` and the instance answers plain HTTP on
`http://localhost` — a trial on a laptop and nothing more. Set it to a domain
and Caddy obtains a certificate for it, renews it, and redirects port 80 to it,
with nothing to install by hand. There is no third case.

That matters beyond convenience: the session cookie is `__Host-` and `Secure`,
which works over HTTPS and nowhere else, and an invitation is a link an email
carries through the open network with somebody's password at the end of it.
Neither of those is served by a port published in the clear. Set
`PLANAFFE_PUBLIC_URL` to the same address, and `deploy/Caddyfile` is the
configuration, all sixteen lines of it.

The same port serves the web application. On first use, exchange the bootstrap
user token once to set the administrator's password. The browser receives an
opaque cookie and never stores the user token. Later visits sign in with email
and password; sessions expire after seven idle days and absolutely after 30
days and can be revoked individually.

The bootstrap token is the first administrator's user token — what the CLI
carries as `PLANAFFE_TOKEN`. The first agent gets its own token from that
administrator, one command later (`POST /agents`, `pa agent create`).

After that, a human at the console signs in with `pa login` rather than keeping
a token in a shell profile: the device-code flow, confirmed in the browser, with
the token kept in the machine's keychain
([ADR 0025](./adr/0025-the-console-signs-in-through-a-browser-and-keeps-a-user-token.md)).
`PLANAFFE_TOKEN` keeps precedence over that keychain and stays the way an agent
and a CI job receive theirs.

The three waiting commands (`pa next --claim --wait`, `pa issue ask --wait`,
and `pa needs-you --wait`) hold one HTTP request open for as long as one hour.
A reverse proxy in front of the instance must therefore allow request and
upstream-response timeouts of at least 3610 seconds. The client adds another
30 seconds for transport overhead; no inbound connection to the client is
needed.

Caddy sets no such deadline unless it is told to, and `deploy/Caddyfile` tells
it nothing; a proxy that timed those requests out would turn Waiting into a
poll without anybody noticing.

`PLANAFFE_TRUSTED_PROXY` says which peers may speak for the caller — an
address, a CIDR network, or `all` where a Compose network renumbers the proxy
on every start. It is `all` in this Compose file, and that is safe for exactly
one reason: the instance publishes no port, so Caddy is the only thing that can
reach it. Unset, the instance reads the socket, and behind a proxy that is the
proxy's own address for every request: the failed-sign-in limit of twenty per
address in fifteen minutes then counts every caller together, and twenty bad
passwords by anybody stop everybody until the window passes. An installation
that runs the instance some other way names an address or a network instead,
and publishes no port its proxy does not own.

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
| `PLANAFFE_BOOTSTRAP_EMAIL` | first start | | the first administrator's sign-in email |
| `PLANAFFE_BOOTSTRAP_TOKEN` | first start | | their user token, at least 32 characters; shorter refuses the start |
| `PLANAFFE_PUBLIC_URL` | for browser or email | | canonical external origin, for Origin checks and links; for example `https://plan.example.com`, without a trailing slash |
| `PLANAFFE_TRUSTED_PROXY` | behind a proxy | | which peers may set `X-Forwarded-For` and `X-Forwarded-Proto`: addresses and CIDR networks, comma-separated, or `all` |
| `PLANAFFE_SMTP_HOST` | no | | SMTP host; when absent, transactional email is disabled |
| `PLANAFFE_SMTP_PORT` | with SMTP | `587` | SMTP port |
| `PLANAFFE_SMTP_USERNAME` | no | | SMTP authentication user; set with the password |
| `PLANAFFE_SMTP_PASSWORD` | no | | SMTP authentication password; set with the username |
| `PLANAFFE_SMTP_SECURITY` | with SMTP | `starttls` | `starttls`, `tls` or `none`; `none` is allowed only in Development |
| `PLANAFFE_SMTP_FROM_ADDRESS` | with SMTP | | sender email address |
| `PLANAFFE_SMTP_FROM_NAME` | no | `planaffe` | sender display name |
| `PLANAFFE_CLAIM_EXPIRY_HOURS` | no | `4` | how long an agent's claim lives without a write of the holder's (VISION 11); a user's never expires |
| `PLANAFFE_DELETION_GRACE_DAYS` | no | `7` | how long a deleted issue, epic, label or project can be restored before the purge may take it (ADR 0013); a floor, not a deadline |
| `PLANAFFE_LOG_ENDPOINT` | no | | a logaffe instance to log into, scheme and host; set together with the token (ADR 0008) |
| `PLANAFFE_LOG_TOKEN` | no | | the ingest token of the logaffe project the entries belong to |
| `PLANAFFE_LOG_LEVEL` | no | `Information` | the floor: `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal` |
| `PLANAFFE_SITE_ADDRESS` | no | `:80` | what Caddy answers as, and the whole of the TLS decision: a domain fetches and renews a certificate for itself and redirects port 80; `:80` is plain HTTP for a trial on loopback |
| `PLANAFFE_HTTP_PORT` | no | `80` | the host port Caddy's HTTP listener is published on; the whole left half, so `127.0.0.1:8080` binds on loopback alone |
| `PLANAFFE_HTTPS_PORT` | no | `443` | the same for HTTPS, TCP and UDP. Leave both alone where a certificate is to be fetched: the ACME challenge arrives on 80 and 443 |
| `PLANAFFE_IMAGE` | no | `ghcr.io/datavisionzero/planaffe:latest` | the image: `:latest` is the newest stable release, `:1.2` the newest patch of a minor, `:main` the trunk, and a `:1.2.3` or `:sha-<commit>` pins an installation to one build |

The three bootstrap variables are ignored on every start after the first — the
instance already has identities, and a bootstrap happens once. Changing the
token in the environment changes nothing. An active user who loses a user token
signs in through the browser and creates another; password recovery uses email.

Inside the container the connection string is `ConnectionStrings__Postgres`,
which the Compose file assembles from the password; a deployment that is not
this Compose file sets it directly.

SMTP is optional. Without it, bootstrap, existing sign-ins, the CLI and the API
work normally; invitation, password recovery, email change and the admin test
mail report that SMTP is not configured. Credentials are never returned by the
API or written to logs. Setting `PLANAFFE_SMTP_HOST` enables SMTP and requires
the sender and public URL; username and password must be supplied together. A
partial or invalid configuration refuses startup. The public URL is never
inferred from request headers.

Production must expose the public URL over HTTPS: the session cookie is
`Secure`, `HttpOnly`, `SameSite=Lax`, has no `Domain` and uses the `__Host-`
prefix. Only the explicit Development environment uses a non-Secure cookie with
a different name. A reverse proxy must preserve the original `Origin` and `Host`
headers — Caddy does, and needs nothing said about it.

A browser write carries `X-Planaffe-CSRF: 1`, which no cross-site form can set,
and an `Origin` the instance checks. With `PLANAFFE_PUBLIC_URL` set it is
checked whole; without it the scheme is left out, because a proxy that
terminates TLS forwards the request as `http` unless `PLANAFFE_TRUSTED_PROXY`
lets it say otherwise. The public URL itself is never taken from a header.

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

That dump is the backup and the exact way back into an instance. The way back
is one command more, and it is the half that is otherwise carried out for the
first time on the day it matters — stop the application, put the dump into an
empty database, start again:

```sh
docker compose -f deploy/docker-compose.yml stop planaffe
docker compose -f deploy/docker-compose.yml exec db dropdb -U planaffe planaffe
docker compose -f deploy/docker-compose.yml exec db createdb -U planaffe planaffe
docker compose -f deploy/docker-compose.yml exec -T db psql -v ON_ERROR_STOP=1 -U planaffe planaffe < planaffe.sql
docker compose -f deploy/docker-compose.yml start planaffe
```

The order matters in one place only: the application is down while the database
is replaced, because an instance that is running holds connections `dropdb`
refuses to work around. `ON_ERROR_STOP=1` is not decoration — without it `psql`
walks past a failed statement and exits `0` on a restore that did not happen.

Migrations only run forward (ADR 0011), and for a dump that means one thing in
each direction. A dump from an older planaffe restored under a newer one needs
nothing done to it: the start that follows migrates it, the way any upgrade
does. A dump from a newer planaffe under an older one has no answer at all —
the instance refuses to start rather than serve a shape it misunderstands. So
keep a dump beside the version it was taken from, and roll the image back
before the database.

None of this is a script that ships with planaffe, and it is not prose that has
merely been read either: `BackupTests` in the integration tests takes a dump,
restores it into an empty database, starts an instance on it and asks for what
was there before. The way back is proved at every change of the schema rather
than described.

For a readable, portable copy of one project, use the CLI instead:

```sh
pa export --project PLAN --json > planaffe-PLAN.json
```

The document contains the project, labels, complete epics and releases, and
every non-deleted issue with its comments, questions and history. The CLI reads
the existing paginated API collections; there is no separate export endpoint.
There is deliberately no importer. To move work into another system or back
into planaffe, give the document to an agent and have it create the labels,
epics and issues through that system's interface or through `pa issue create
--file`. This preserves the explicit decisions involved in mapping identities,
statuses and history instead of pretending those concepts are interchangeable.

## Development

Postgres alone, for `dotnet run`:

```sh
docker compose -f deploy/docker-compose.dev.yml up -d
dotnet run --project src/Planaffe.Api
```

`appsettings.Development.json` carries the matching connection string and a
bootstrap administrator with an email and token of no consequence. The
development Compose file also starts Mailpit for transactional-email integration
tests and local inspection; Mailpit is never part of the production Compose
file.
