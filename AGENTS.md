# planaffe

Instructions for coding agents working in this repository. See
[VISION.md](VISION.md) for what planaffe is and what it deliberately is not.

## Language

Everything in the repository is written in **English**, regardless of the
language a contributor speaks: source code, identifiers, comments, docs, ADRs,
commit messages, PR titles and bodies, and issues.

Working notes, tickets and drafts under `scratchpad/` are exempt — they are
local and never pushed (see below).

## Repository and contributions

- **Host**: GitHub — `datavisionzero/planaffe`. Public, MIT.
- **Layout**: where the code lives, which project holds what, and which way the
  dependencies point is [`docs/codebase.md`](docs/codebase.md). Read it before
  adding a file.
- **Language of the domain**: [`CONTEXT.md`](CONTEXT.md) is the glossary. Code,
  identifiers, the HTTP contract and the CLI use its canonical names without
  exception. Read it before naming anything, and add the term there when you
  settle a new one.
- **Decisions**: [`docs/adr/`](docs/adr/) holds the architecture decisions. Read
  the ones that touch the area you are about to work in, and say so explicitly
  when your work contradicts one instead of silently overriding it.
- Contributions arrive as pull requests from forks. Maintainers may push to
  `main` directly.
- Commit and push only when asked to.

## Branching

The repository is a **trunk**: `main` is the only long-lived branch, and it is
always in a state that could be released
([ADR 0001](docs/adr/0001-the-repository-is-a-trunk.md)).

- Committing straight to `main` is the normal path for maintainers.
- A short-lived branch is optional — take one when the work is large, risky, or
  wants review, and merge it back within days, not weeks.
- Whatever the path, CI has to be green on `main`. A red trunk is fixed or
  reverted before anything else is pushed on top of it.

## Before pushing

Two checks, every time, because a public repository does not forget:

1. **No personal information.** No real names, private e-mail addresses, home
   or IP addresses, hostnames of private machines, absolute paths carrying a
   user name, or anything else that identifies a person. Commit authorship and
   `datavisionzero` are the exception — that is the account this is published
   under. Check the diff, not just the files you meant to change:
   `git diff --staged` and `git log -p @{u}..` before the push.
2. **No secrets.** No tokens, connection strings, private keys or `.env`
   contents, not even expired or example ones that look real.

When something has to be written down that fails either check, it belongs in
`scratchpad/`, which is ignored by git.

## The scratchpad

`scratchpad/` is the local working area — tickets, notes, drafts, throwaway
experiments. It is in `.gitignore` and never reaches the remote. Its rules are
in `scratchpad/README.md` on the machine you are working on. If the directory
does not exist, proceed silently; it is not part of the published repository.

**Issues live in planaffe itself**, not on GitHub: the maintainer's own
instance tracks the work (`.planaffe` at the root names the project, and
`docs/cli.md` says how `pa` is configured), because this repository is public
and the working level of a product that is still being cut into shape is not a
shop window. GitHub issues stay the place where strangers report things.
