# The AGENTS.md Block

Whoever tracks tickets at their git host has to explain to every `AGENTS.md` all
over again how tickets work *here* (`VISION.md` 2.1). planaffe is one tracker
across all repositories, so that paragraph can be written once instead of once
per project — and this is it.

The block below is meant to be **copied verbatim** into the `AGENTS.md` (or
`CLAUDE.md`, or whatever the harness reads) of a repository whose tickets live in
a planaffe instance. Replace `PROJ` with the project key and change nothing else:
it names no file that only exists here, and it says only what an agent has to
know to take a ticket and give it back.

It is deliberately short. `docs/cli.md` is the complete surface; this is the two
or three commands an agent actually runs.

> Two things will point here rather than repeat it: `pa init`, which prints the
> block when it wires a repository to an instance, and the end of the
> installation guide. And when project-wide instructions for agents later live in
> the instance itself (`VISION.md` 15.3), this text is what they start from —
> written once on the server instead of once in every repository.

---

````markdown
## Tickets

Tickets for this repository live in planaffe, project `PROJ`. The checked-in
`.planaffe` file names the project, so no command has to. `pa` needs
`PLANAFFE_URL` and `PLANAFFE_TOKEN` in the environment and nothing else; it is
never interactive, writes data to stdout and errors to stderr, and `--json`
prints the object as the API answered it.

### Taking a ticket and giving it back

```sh
pa next --claim              # the highest-ranked workable ticket, claimed in one step
                             # exit 8 means nothing is workable, and says why
# … do the work …
pa issue close PROJ-42 --done --result-file -   # the result as Markdown on stdin
```

`pa next --claim` hands out and claims atomically, so two agents never get the
same ticket. Claim what you are about to work on, and only that. If you stop
without finishing, `pa issue release PROJ-42` puts it back into `todo` for
somebody else; an agent's claim also expires on its own once the agent goes
quiet.

Close with `--done` when the work the ticket asked for is delivered, with
`--canceled` when it will not be done — the result says why either way. Where the
project requires review, an agent's close lands in `review` and a human accepts
it; that is not an error.

### Asking instead of guessing

A question is a state on the ticket, a comment is not. **Whoever can go on
comments; whoever cannot go on asks.**

```sh
pa issue comment PROJ-42 "…"            # an observation, a decision, an interim state
pa issue ask PROJ-42 "…"                # what you need to know before you can go on
pa issue ask PROJ-42 "…" --wait 600     # the same, and wait up to 600 s for the answer
```

An open question makes the ticket unworkable and puts it on the human's list, so
nothing has to be flagged by hand. Asking does **not** release the claim: wait
with `--wait` and keep your context, or release the ticket if you will not wait.
Say what you need — "something is wrong here" is not a question. Answering
questions is a human's job; answer one only when told to.

### `ready`

`ready` is a statement about the ticket, not a permission: it is concrete enough
that somebody can implement it without asking first. Whoever writes a ticket says
so per ticket — set it on the ones that are clear, leave it off on the ones that
still have to ripen:

```sh
pa issue create "Title" --description-file - --priority 2 --ready
pa issue edit PROJ-42 --ready false     # it turned out to be too vague
```

Where the project has triage required switched on, `ready` decides what `next`
hands out: an unflagged ticket is never handed to anybody. The flag is a human's
word by convention rather than by a rule — set it when you are told to, and clear
it on your own when a ticket you picked up turns out to be too thin, saying in a
question or a comment what is missing.

### The commands you need

```sh
pa next --claim                          # what to work on
pa issue view PROJ-42                    # the complete ticket, epic description and all
pa issue comment PROJ-42 "…"             # a note that forces nobody to act
pa issue ask PROJ-42 "…" [--wait 600]    # a question; the ticket waits for an answer
pa issue close PROJ-42 --done --result-file -
pa issue release PROJ-42                 # give it back unfinished
```

Everything else — creating tickets in bulk, epics, labels, releases — is
`pa <object> --help`.
````
