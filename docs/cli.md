# The CLI

`pa` is planaffe from the console: the interface for agents and console-minded
humans (VISION 6.1), a client of the public API and nothing else (ADR 0003),
built as one static binary from `src/cli/`. Its shape is `pa <object> <verb>`,
like `gh` and `glab`; `pa next` is the one verb that is its own object.

## Configuration

Two environment variables and one file, and nothing to log into:

| | |
|---|---|
| `PLANAFFE_URL` | the instance, scheme and host |
| `PLANAFFE_TOKEN` | a user token or an agent token; the server tells them apart, `pa` never says which it holds (ADR 0015) |
| `.planaffe` | the project file (`CONTEXT.md`): checked in at the root of a repository, found from the working directory upwards |

The project file is `key = value` lines, `#` for comments, and knows two keys:

```
project = PLAN
repo = repo/planaffe     # where one project spans several repositories: this one's label
```

Whoever is in the repository never has to name the project; `--project KEY`
overrides the file, and `--repo NAME` or `--repo none` overrides its label. An
unknown key in the file is a mistake, not a comment: a misspelt `projekt` that
silently did nothing would send every command to the wrong project.

## Commitments to agents

- **Data to stdout, errors to stderr**, always. `--json` prints the object as
  the API answered it — the complete issue from `next --claim`, the slim page
  from `next` — and nothing else on stdout.
- **Never interactive.** No prompt, no editor, no pager; stdin is read only where
  a flag says so.
- **Every write carries an `Idempotency-Key`** `pa` generates itself, one per
  invocation, so a retry after a lost connection is safe (docs/api.md).
- **`User-Agent: pa/<version> (<os>/<arch>)`** on every request, and the
  instance's `Planaffe-Version` compared on every answer: a `pa` of another major,
  or older than the instance's minor, stops with exit 9 and says which of the two
  moves (ADR 0011). A development build of either side is not checked.

## Exit codes

The table of `docs/api.md`, derived from the status and the problem document:

| exit | meaning |
|---|---|
| 0 | success |
| 1 | unexpected: a 500, an answer `pa` cannot parse, a bug in `pa` |
| 2 | usage: bad arguments, `PLANAFFE_URL` or `PLANAFFE_TOKEN` unset, a `.planaffe` file `pa` cannot read |
| 3 | not found, deleted included |
| 4 | refused: validation, and every 422 |
| 5 | conflict: `claim-held`, `claim-lost`, `idempotency-mismatch`, `release-exists` |
| 6 | stale |
| 7 | denied: 401, 403 |
| 8 | empty: `next` found nothing, or another waiting command reached its deadline |
| 9 | version skew |
| 10 | unreachable |

## Verbs

```
pa next                      # the ready-for-agents list, in the order next hands out
pa next --claim              # take the highest-ranked workable issue and claim it
pa next --claim --wait 60    # wait up to 60 seconds for one; exit 8 at the deadline
pa next --claim --ready      # only flagged issues, even where triage is not required
pa next --claim --epic PLAN-E3 --label cut-1 --repo none
pa next --json               # the page, with reasons
pa needs-you                 # questions, review, unready under triage, then stuck blocker chains
pa needs-you --wait 3600     # wait until the list gains an entry; exit 8 at the deadline
pa needs-you --limit 20 --json

pa issue create "Title" --description-file - --priority 3 --ready --label feature --epic PLAN-E2 --blocked-by PLAN-40
pa issue create --file batch.json          # several wired-up issues in one transaction (below)
pa issue list --status todo --label bug --assignee me --sort priority
pa issue list -q '"for update" -flaky' # full text: issue fields, comments and questions
pa issue list --deleted                    # the grace period, the one read that sees deleted rows
pa issue view PLAN-42 [--json]             # the complete issue, epic description and all
pa issue edit PLAN-42 --title "…" --priority 2 --assignee none --label a --label b --if-match "<updated_at>"
pa issue edit PLAN-42 PLAN-43 --priority 2     # same change, one transaction, all or none
pa issue edit PLAN-42 --status backlog     # parking; every other status move is an act
pa issue delete PLAN-42 PLAN-43 · pa issue restore PLAN-42 · pa issue history PLAN-42
pa issue label add PLAN-42 feature · pa issue label remove PLAN-42 feature
pa issue block PLAN-42 --by PLAN-40 · pa issue unblock PLAN-42 --by PLAN-40
```

The batch file is the bulk body of `docs/api.md`: `project` may be left out
and is taken from the file or `--project`; `ref` is a handle valid inside the
request; `blocked_by` and `blocks` take refs and keys alike; the `repo` label is
added to every item unless `--repo none`.

```json
{ "issues": [
  { "ref": "schema",   "title": "The schema", "priority": 3, "ready": true, "labels": ["feature"] },
  { "ref": "contract", "title": "The contract", "blocked_by": ["schema", "PLAN-6"] }
] }
```

The acts (ADR 0016), each printing the complete issue it acted on:

```
pa issue claim PLAN-42 [--force]           # taken, extended, or claim-held (exit 5); --force takes over
pa issue release PLAN-42                   # let go: the claim cleared, the status todo
pa issue close PLAN-42 --done --result-file -      # a missing result is pointed out on stderr, never refused
pa issue close PLAN-42 --canceled --result-file why.md
pa issue review PLAN-42 [--result-file F]  # hand in explicitly
pa issue reopen PLAN-42 --comment "…"      # back to todo; from review, a missing comment is pointed out
pa issue park PLAN-42 · pa issue unpark PLAN-42
pa issue comment PLAN-42 "…" | --file -    # whoever can go on comments
pa issue ask PLAN-42 "…" | --file -        # whoever cannot go on asks; the claim stays
pa issue ask PLAN-42 "…" --wait 600        # wait for the answer, at most for the rest of that claim
pa question list [--answered | --all] [--issue PLAN-42] [-q "serializable"]
pa question answer <id> "…" | --file -
```

Projects, labels and epics:

```
pa project create PLAN "planaffe" [--triage-required] [--review-required]
pa project list · pa project view [KEY] · pa project edit PLAN --review-required true --name "…"
pa project delete PLAN --confirm PLAN      # the key typed twice, never prompted for; administrators only
pa project restore PLAN
pa label list                              # the project's schema: name, group, description
pa label create area:infra --group area --description "Compose, CI, the image."
pa label edit area:infra --group none · pa label delete area:infra · pa label restore area:infra
pa epic create "Backend" --description-file plan.md --label feature
pa epic list [--status open|closed|all] [--label L] · pa epic view PLAN-E2
pa epic edit PLAN-E2 --description-file - --if-match "<updated_at>"
pa epic close PLAN-E2 [--cancel-open | --park-open]   # lists what is still open; cancels or parks it on a flag, never interactively
pa epic reopen PLAN-E2 · pa epic delete PLAN-E2 · pa epic restore PLAN-E2

pa release list
pa release view unreleased | pa release view v1.2.0
pa release publish v1.2.0 [--description-file notes.md]
pa release notes v1.2.0                 # Markdown, with sub-issues indented under their parent
```

Identities (ADR 0015) — a secret is printed once, to stdout, and nowhere else:

```
pa me                                      # who the token says you are
pa me set --kind codex --harness cli --environment container --version 1.2.3
                                           # agents report stable metadata; `none` clears a field
pa version                                 # pa's version and the instance's; exit 9 when they do not fit
pa export --json                           # one readable document containing the current project
pa user create NAME --email ADDRESS [--administrator] # administrators only; sends an invitation
pa user list
pa agent create [--name NAME]              # users only; the agent's one token, once
pa agent list · pa agent view <id> · pa agent rename <id> --name NAME · pa agent revoke <id>
pa token create · pa token list · pa token revoke <id>
```

Descriptions, results, comments, questions and answers come from an argument, a
file or stdin (`-`), never an editor. The whole agent cycle of VISION 6.1 is
`pa next --claim`, work, `pa issue comment`, `pa issue ask`, and `pa issue close
--done --result-file -`; a human answers with `pa question answer`. The three
waiting commands accept any positive number of seconds and split waits longer
than the server's one-hour limit into rounds. `pa issue ask --wait` stops no
later than the expiry of the caller's claim; `pa needs-you --wait` first reads
the current page and then uses its ETag for the long poll. A deadline is exit 8.

## Working on it

```sh
cd src/cli
go generate ./...     # the client, from ../../docs/api/openapi.json (not committed)
go test ./...
go build ./cmd/pa
```

The generated client is `internal/api/client.gen.go`, produced by `oapi-codegen`
as a Go tool dependency of the module; CI runs the same three commands. A
release builds `cmd/pa` per platform with
`-ldflags "-X github.com/datavisionzero/planaffe/src/cli/internal/version.Version=<tag>"`.
