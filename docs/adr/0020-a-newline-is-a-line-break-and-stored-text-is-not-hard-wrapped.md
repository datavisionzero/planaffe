# A Newline Is a Line Break, and Stored Text Is Not Hard-Wrapped

The Markdown pipeline of [ADR 0007](./0007-markdown-is-rendered-in-the-browser-and-never-as-html.md)
gains `remark-breaks`: a single newline renders as a line break rather than as a
space. In exchange, no description, result, comment, question, answer or release
note is stored hard-wrapped — a paragraph is one line, however long, and a blank
line separates paragraphs.

This is a departure from CommonMark, which the pipeline otherwise follows, and
it was not free to arrive at. Both halves are one decision: the renderer may
only treat every newline as meant once every newline actually is.

## Why

The text has two authors and two readers, and they had settled on opposite
conventions without either being written down.

Agents wrote for the terminal. `pa issue view` prints a description verbatim, so
agents hard-wrapped at about 78 columns, and CommonMark's soft break — a newline
is a space — reflowed that correctly in the browser. It worked, and it was
invisible.

People wrote into a text area. They pressed Enter, saved, and the break was
gone: not stripped anywhere, but rendered as the space CommonMark says it is.
Every Markdown field in the product has this shape — the issue description, the
epic description, release notes, comments, answers, results — so it was not one
field misbehaving, it was the pipeline being right about the wrong reader.

Only one of the two can be served by a renderer, because at render time a
newline inside a paragraph carries no evidence of which one put it there. So the
ambiguity is removed where it is created rather than guessed at where it is
displayed.

## Why not the alternatives

**Leave the renderer, tell people to use a blank line.** Defensible — the field
is Markdown and says so, and it already has a preview. But the product's own
target group writes tickets in a hurry (VISION 3), and a rule that has to be
learned to type a line break is a rule that will be broken every day. The
preview shows the loss without explaining it.

**Add `remark-breaks` and leave the corpus wrapped.** The cheap half of this
decision, and the wrong one: every 78-column wrap in every existing ticket would
become a visible break, turning prose into a staircase. The renderer would then
be right about newlines that were never meant.

**Teach the CLI to wrap for the terminal, and keep hard wraps out of the store
that way.** This is what unwrapping needs to remain readable, and it turned out
to need no code: a terminal soft-wraps a long line on its own. A wrapper in
`render` that knew the window width would be a second place deciding what a
paragraph looks like, and the first one — the browser — already does it better.

## Consequences

**The shape of stored text is now load-bearing.** A hard-wrapped paragraph
written by an older agent, or pasted from a terminal, renders as a staircase and
looks broken, because it is. That is the cost of the trade, and it is mostly
paid once: the descriptions and results in the tracker were unwrapped when this
was decided.

**A comment cannot be unwrapped, because a comment cannot be edited.** There is
no `PATCH` under `/issues/{key}/comments` and there should not be one — a
comment is a thing somebody said, and the record of what was said does not get
rewritten because the renderer learned something. The handful written before
this decision stay as they are and read as a staircase. That is the honest
outcome and it is cheaper than an edit endpoint nobody wanted.

**Whoever writes tickets is told.** This is a convention for the text agents
produce, not a rule about the repository, so it lives where the instructions for
writing tickets live rather than in `AGENTS.md`.

**Pasted output still needs a fence.** A log or a stack trace was always meant
to go in a fenced block; now the difference between fencing it and not is
visible immediately rather than only when someone reads it.
