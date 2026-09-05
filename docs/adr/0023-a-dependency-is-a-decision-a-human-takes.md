# A Dependency Is a Decision a Human Takes

Nothing in this repository forbids a dependency, and nothing ever did. What is
decided here is how one arrives: a human approves it, its licence is checked
before the move, and what it costs the browser is measured rather than
estimated. The rule holds for the backend, the CLI and the web application
alike, which is why it is not written into an ADR about any one of them.

It is written down because its absence had begun to act like a rule of its own.
[ADR 0007](./0007-markdown-is-rendered-in-the-browser-and-never-as-html.md) and
[ADR 0017](./0017-the-web-application-is-drawn-by-tailwind-and-base-ui-components-the-repository-owns.md)
are quoted as a general ban on bringing anything in — which is neither what they
say nor what they weighed. ADR 0007 decides that Markdown is rendered in the
browser and edited as Markdown: a decision about the form of editing, not about
libraries. ADR 0017 turned down one component library and one syntax
highlighter, each on its own measured cost, and named a headless kit it did take
because focus management is not what a project of this size should own. Together
they read as "no new dependencies", and a rule nobody wrote is a rule nobody can
argue with. It had a price: the one Markdown editor the web application has
stayed a six-line text area longer than it should have.

## What is decided

**A human approves it.** A new dependency is not added by an agent on the way
past. What it does, what it weighs, and what the alternative would be belong in
the question that is asked before it, not in the commit message that carries it.
The approval is per dependency, not per area: there is no list of packages an
agent may reach for without asking.

**What is decided is the tree, not the package.** A dependency arrives with
everything it pulls, and the transitive part is where the surprises live — the
highlighter that came along for a Markdown editor, the second primitive library
under a command palette. Licence and weight are read off the tree that actually
lands in the lock file. A package that is small and permissive and drags in
something that is neither has not passed.

**The licence is checked before the move, not after.** Open source only, and
only licences that cannot change under us for the same code: no Business Source
Licence, no SSPL, no terms that depend on how much a user uses or how large they
are, and no open core whose paid tier holds back what we would actually need.
This is not caution for its own sake — VISION 4.8 promises an MIT product with
no open-core restrictions in the core, and VISION 2.1 promises that what is
self-hosted and MIT-licensed stays free. A tracker with a licence switch in its
belly cannot keep either.

**What ships is permissive; copyleft that reaches the product is not taken.**
GPL and AGPL are open source and are not the question here: the question is
whether an operator can take this instance, change it, and run it under the
terms VISION 2.1 names. A library whose licence travels into the product would
answer that for us, and we do not let a dependency decide the product's licence.
For tooling that only builds or tests, a copyleft licence is nobody's problem
and is fine.

**The weight is measured, not guessed.** What goes into the browser is weighed
in gzip against what is already there, and the number goes in the ticket. There
is no budget in kilobytes, because a number like that ages faster than the
decisions it would govern; what there is instead is the architecture ADR 0006
already asks for — the shell ships first, and what only one screen needs loads
with that screen, behind a dynamic import. Size decides where a dependency
loads. It rarely decides whether.

**The three checks are lighter for tooling than for the product.** A formatter,
a test runner or a build plugin ships to nobody: it is still a human's decision
and still gets the licence read, but it is not weighed, because there is nothing
to weigh it against.

## What this does not decide

**Nothing about the dependencies that are already here.** Tailwind, Base UI,
TanStack Virtual, `react-markdown`, Serilog, Cobra and the rest were decided,
each in its place, and this rule is not a reason to revisit them. It applies to
the next one.

**Nothing about what the answer should be.** ADR 0017's refusals stay refusals
and remain correct on their own reasoning. A rule about how a question is
decided says nothing about how it comes out.

## Why not the alternatives

**Leave it implicit.** That is the position we were in, and it turned out not to
be neutral: with nothing written down, the strictest reading of the nearest ADR
wins every argument, and the strictest reading of ADR 0007 and ADR 0017 is a ban
they never decided.

**A dependency budget** — a count of packages, or a ceiling in kilobytes. It
looks checkable and is not: the number is picked once and then defended, or
quietly moved, and neither has anything to do with whether a particular library
is worth its size. The measurement belongs to the ticket that proposes the
dependency, where it can be argued about against an alternative.

**An allowlist of approved packages.** It has to be maintained by the same
person who would otherwise just be asked, and it goes stale in exactly the
direction that hurts: the thing that is not on it is the thing that is new.

**Automated licence scanning in CI instead of a rule.** A scanner is a good
thing to have and would catch the transitive surprise this rule is worried
about. It decides nothing, though — it reports. It can be added later under this
rule; it cannot replace it.

## Consequences

**ADR 0007 says what it means about editing.** Its consequence "No WYSIWYG
editor" is clarified in place: what is stored is Markdown and what is edited is
Markdown, which is a statement about the form of editing. An editor that edits
Markdown as source and helps while doing it is compatible with it; an editor
that keeps HTML or a document model of its own and derives Markdown when saving
is not.

**ADR 0017 is untouched.** Its decision was right and is consistent with this
one; this ADR is a reference from it, not a correction of it.

**`AGENTS.md` carries the short form.** An instruction to agents that lives only
in an ADR is an instruction that has to be found first. The repository
instructions say plainly that a dependency is approved by a human and name this
ADR for the rest.

**A proposal has a shape.** What it does, what the alternative is, what the tree
looks like, what the licence is, and — where it goes to the browser — what it
weighs in gzip against what is already there. Five answers, in the ticket.
