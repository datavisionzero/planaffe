# planaffe

Instructions for coding agents working in this repository. See
[VISION.md](VISION.md) for what planaffe is and what it deliberately is not.

## Language

Everything in the repository is written in **English**, regardless of the
language a contributor speaks: source code, identifiers, comments, docs, ADRs,
commit messages, and PR titles and bodies. GitHub issues too — they are where
strangers arrive.

What never reaches the remote is exempt, because it is the working level rather
than the published product: notes and drafts under `scratchpad/` (see below),
and the tickets in the tracker, which are written in whatever language the
people working on them speak.

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

## The tracker

**planaffe tracks planaffe.** The backlog lives in a planaffe instance, in the
project the checked-in [`.planaffe`](.planaffe) file names — `PLAN`, so the
issues of this repository read `PLAN-42` and its epics `PLAN-E3`. The file is
found from the working directory upwards, which is why no command here has to
name the project.

Reach it with `pa`, the CLI of [`docs/cli.md`](docs/cli.md), built from
`src/cli/`. It needs `PLANAFFE_URL` and `PLANAFFE_TOKEN` in the environment and
nothing else; which instance those name is the contributor's own business and
is never written down here. `pa next` says what to work on, `pa needs-you` what
only a human can resolve, and `pa issue create` opens a ticket. A ticket is
written in the language of whoever works on it, not in the English of the
repository — see Language above.

Until 5 September 2026 the backlog was the local `scratchpad/` instead, because
there was no instance worth trusting with it. That is over: a tracker its own
authors do not use is a tracker nobody should be asked to use.

GitHub issues remain the place where strangers report things. Nothing about
this changes that.

## The scratchpad

`scratchpad/` is the local working area — notes, drafts, German originals,
throwaway experiments. It is in `.gitignore` and never reaches the remote. Its
rules are in `scratchpad/README.md` on the machine you are working on. If the
directory does not exist, proceed silently; it is not part of the published
repository.

What belongs there is the thinking, not the work items: a note that outlives a
session, a draft of something that will be published in English, the output of
a one-off command. A ticket belongs in planaffe, where it can be found by
somebody who is not sitting at this machine.
