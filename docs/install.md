# Installing planaffe

**This document is written for an agent to execute.** It is a sequence, not a
reference: work through it from top to bottom. Every step has one command, the
output that means it worked, and the condition under which you stop. After each
step, decide from that condition alone whether to go on.

`docs/operations.md` is the reference beside it — every variable, the upgrade,
the backup. Read it when a step here sends you there, and not otherwise.

## What you are installing

Two containers: the instance and its Postgres. The instance applies its own
migrations, creates the first administrator on the first start and serves both
the API and the web application on one port. `pa` is the command-line client,
built from the same repository.

## Before you start

Ask the person you are working for these three things and write them down. You
cannot invent them, and two of them are secrets.

1. **Where it will run.** A host with Docker, and the address it will be
   reachable at — `http://localhost:8080` for a machine-local installation, or
   an `https://` address behind a reverse proxy.
2. **The first administrator**: a name and an email address.
3. **Whether it is reachable from outside.** If it is, the person has to make
   two decisions that are theirs: DNS and a certificate. Both are outside this
   document.

**Stop and ask** if any of the three is missing. Do not guess a hostname and do
not invent an email address.

## Step 1 — Docker is there and runs

```sh
docker compose version
```

**Expected:** a version, `v2` or newer.
**Stop if:** the command is not found, or the daemon does not answer. Docker is
the person's to install; say which of the two it was.

## Step 2 — Get the repository

```sh
git clone https://github.com/datavisionzero/planaffe
cd planaffe
```

**Expected:** the directory exists and contains `deploy/docker-compose.yml`.
**Stop if:** the clone fails. That is the network or an authentication problem,
not something to work around.

## Step 3 — Write the environment file

```sh
cp deploy/.env.example deploy/.env
```

Then set four values in `deploy/.env`. Three of them you generate; one you were
given.

| variable | what to put there |
|---|---|
| `POSTGRES_PASSWORD` | generate one: `openssl rand -base64 32` |
| `PLANAFFE_BOOTSTRAP_ADMIN` | the administrator's name |
| `PLANAFFE_BOOTSTRAP_EMAIL` | the administrator's email address |
| `PLANAFFE_BOOTSTRAP_TOKEN` | generate one: `openssl rand -hex 32`. At least 32 characters, or the instance refuses to start |

Set `PLANAFFE_PUBLIC_URL` as well if the instance is reachable at an address
other than `http://localhost:8080` — the browser needs it, and so does every
link in an email.

**Expected:** `deploy/.env` exists and the four values are non-empty.
**Stop if:** you were about to invent the administrator's email address. That is
step "Before you start", and it is the person's.

The file is ignored by git. **Do not commit it, do not print the two generated
secrets into a shared channel, and do not put them into a ticket.**

## Step 4 — Start it

```sh
docker compose -f deploy/docker-compose.yml up -d
```

**Expected:** two containers, and after up to a minute both healthy:

```sh
docker compose -f deploy/docker-compose.yml ps
```

**Stop if:** the instance container restarts in a loop. Read its log — `docker
compose -f deploy/docker-compose.yml logs planaffe` — and report what it says.
A bootstrap token under 32 characters and a database that will not come up are
the two usual causes, and the log names both.

## Step 5 — The instance answers

```sh
curl -sf http://localhost:8080/version
```

**Expected:** `{"version":"…"}`. This endpoint needs no token and answers only
once the migrations and the bootstrap have run.
**Stop if:** it does not answer within a minute of the containers being healthy.

## Step 6 — Get the client

`pa` is published with every release, one static binary per platform. Take the
one for this machine:

```sh
os=$(uname -s | tr '[:upper:]' '[:lower:]')
arch=$(uname -m); [ "$arch" = "x86_64" ] && arch=amd64; [ "$arch" = "aarch64" ] && arch=arm64
curl -fsSL -o pa "https://github.com/datavisionzero/planaffe/releases/latest/download/pa_${os}_${arch}"
chmod +x pa && sudo mv pa /usr/local/bin/pa
```

On Windows the asset is `pa_windows_amd64.exe`. Every release also carries a
`checksums.txt`; verify against it if the download did not come over a
connection you trust.

**Expected:** `pa --version` prints a version.
**Stop if:** the download 404s. That means there is no release yet for this
platform — build it from the clone you already have instead, which needs Go at
the version in `src/cli/go.mod`:

```sh
cd src/cli
go generate ./...      # the API client, generated from the checked-in contract
go install ./cmd/pa
cd ../..
```

That puts it in `~/go/bin`; if `pa` is then not found, that directory is not on
the `PATH`. Go is the person's to install, like Docker.

## Step 7 — Put the two variables in the environment

```sh
export PLANAFFE_URL=http://localhost:8080
export PLANAFFE_TOKEN=<PLANAFFE_BOOTSTRAP_TOKEN from step 3>
```

`pa` needs exactly these two and nothing else. **Expected:**

```sh
pa me
```

prints the administrator's name and `administrator`.
**Stop if:** exit 10 — that is `PLANAFFE_URL`, and the address is wrong. Exit 7
is `PLANAFFE_TOKEN`, and the token is not the one from step 3.

To keep them, put both lines in a file the shell reads at startup, with mode
`0600`, and **not** in the repository.

## Step 8 — Connect the repository whose tickets these are

Change into the repository the tickets belong to — not into the planaffe clone,
unless that is the one being tracked — and run:

```sh
pa init            # or: pa init KEY, where KEY is the project key
```

**Expected:** it prints the instance, the identity, the project it took or
created, and the `.planaffe` file it wrote.
**Stop if:** it says a project key cannot be made from the directory name. Pass
one: `pa init PROJ`, upper case, two to ten letters or digits.

`.planaffe` belongs in the repository and is committed. It carries no secret —
only the project key.

## Step 9 — The whole way, checked at once

This is the acceptance test. Run all of it; every line has to do what it says.

```sh
pa project view                                     # the project exists
pa issue create "The installation works" --description-file - <<'EOF'
Created by the installation guide as its own check. Close it or delete it.
EOF
pa issue list --status todo                         # the issue is in the list
pa next                                             # and it is workable
```

**Expected:** the project prints with its key and name, the create prints a key
like `PROJ-1`, and that key appears in both lists.
**Stop if:** any of the four exits non-zero. The exit code says what happened —
`docs/cli.md`, "Exit codes" — and the message on stderr says it in a sentence.

Open `$PLANAFFE_URL` in a browser as the last check. On the first visit the
administrator sets a password once, with the bootstrap token; from then on it
is email and password. **That step is a person's**, because it sets a password:
hand over here and say what is left to do.

## Step 10 — Tell the agents about it

Copy the block from [`agents-md.md`](./agents-md.md) into the `AGENTS.md` of the
repository you connected in step 8 and replace `PROJ` with the project key.
Without it, the next agent in that repository does not know the tickets are
here.

## What is left for a person

- The password of the first administrator, in the browser (step 9).
- DNS and a certificate, where the instance is reachable from outside.
- The reverse proxy in front of it, if there is one: it needs request timeouts
  of at least 3610 seconds, because three commands wait for up to an hour, and
  `PLANAFFE_TRUSTED_PROXY` has to name it. `docs/operations.md` has both.
- Transactional email, if invitations and password recovery are wanted. It is
  optional and everything else works without it.
- The backup. Operations are meant to be a `pg_dump` and nothing else;
  `docs/operations.md` has the command and the upgrade that starts with it.
