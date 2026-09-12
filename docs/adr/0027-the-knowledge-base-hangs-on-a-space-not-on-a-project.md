# The Knowledge Base Hangs on a Space, Not on a Project

Knowledge that belongs to no project lives in a **space**: a second bracket
beside the project, and the only thing in the knowledge base that carries
access. A human creates a space and names the users who see it — an agent never
does either — and the space carries one switch of its own: it is open to agents
or closed to them. Pages hang inside it, at most three levels deep, and the
project's flat wiki is withdrawn once they do
([`VISION.md`](../../VISION.md) 18.).

This reverses two sentences of VISION 7, and they are reversed knowingly rather
than quietly: "whoever may see the tickets may see the knowledge" was the
argument that kept a second permission model out, and "flat, and it stays flat"
was written about a project's wiki. Both are right about a project's wiki. This
is not one. [ADR 0021](./0021-a-pages-address-is-its-slug-not-a-key.md) stands
in everything it says about the slug; the one sentence it ends on moves, because
a name is now unique under its parent rather than in a project.

## Why the bracket is a new thing and not a project

The knowledge this is built for has no project: a company's handbook, how data
protection is handled, what was agreed with a customer. Hanging it on a project
would mean creating projects that are not projects — and a project here is not
an empty container. It carries a key, and every key prefixes issues; it carries
epics, releases, the workflow, `next`, `needs-you`, triage and review switches.
A handbook would own all of that and use none of it, visibly, in every list and
every switcher.

The second half is that access has to hang somewhere, and whatever it hangs on
*is* the bracket. Granting per page is the fine-grained model VISION 12 refuses;
granting per instance is not granting at all. So the bracket and the access are
one decision, not two, and the space is what that decision is called.

The price is real and is paid once per person: knowledge that used to ride along
on project access now needs a grant of its own. VISION 18 states it rather than
hiding it.

## Why the switch names the agent and not the interface

The obvious wording is "this space cannot be read over the CLI". It would be
wrong the day the MCP server arrives (VISION 15.1), and wrong again for a script
against the HTTP API — and wrong *silently*: a space somebody marked closed
would stay reachable through a route that did not exist when the box was ticked.
So the switch names the identity that reads. Whatever an agent's token is used
through, a space closed to agents is not there for it.

**Not there, not forbidden.** An error that distinguishes "does not exist" from
"you may not" hands over the existence of the space, and the existence is half of
what is being protected. A closed space is absent from every list, every search
result and every direct address, and the answer under its address is the one a
space that never existed would get. This is the same shape as the hidden
cross-project blocker in VISION 12, with one difference that explains both: the
blocker has to keep blocking, so it is shown as an obstacle without a name;
nothing here depends on a page the reader may not have, so nothing is shown.

## Why an agent does not create a space

The same line [ADR 0015](./0015-a-token-is-an-agent-or-a-users-key-and-an-agent-is-never-an-administrator.md)
draws for identities. An agent that can open brackets decides what it may read
next and can open one that is closed to nobody; the switch above would then be a
suggestion. And it keeps the number of spaces small: a bracket that is free to
create breeds, granting turns into a chore, the chore turns into a formality, and
the formality ends at everyone seeing everything.

Inside a space that is open to it, the agent does everything a human does with
pages — read, create, edit, move, delete, search. The border is the bracket, not
the work.

## Why not the alternatives

**Per-page access, the way large wikis do it.** Every page becomes a possible
exception, and the question "who can see this?" stops having a readable answer;
it becomes a computation over a tree with inheritance and overrides. VISION 12's
coarse model exists precisely so that the answer stays a list of names.

**A flag per page instead of per space.** More flexible, and wrong in the way
VISION 7 already describes for the instructions: a guarantee is worth what it is
worth at a glance. Whoever writes something sensitive should not have to
remember a checkbox on each page — they write it in a closed space, and the
space is closed, including for every page added to it later by somebody who
never saw the switch.

**Keep the project's wiki alongside the knowledge base.** Two addresses, two
searches, two places to look for one sentence, and a rule about which knowledge
goes where that nobody remembers a month later. The one text that genuinely
cannot move is the instructions, and it does not stay as a page: it returns to
the project as a field of its own, because it must follow project access and
nothing else (VISION 15.3, 18.).

**A second application.** A second identity model, a second search, a second
container and a second thing to back up — for a product whose second guiding
principle is that hosting it stays two containers. The separation the idea wants
is one of interface, not of deployment, and that is where it is made.

## Consequences

- `CONTEXT.md` gains **Space** and loses `space` from the words to avoid for
  **Project**. The word is given away here, so the glossary has to say what it
  now means and what it still must not mean.
- ADR 0021 needs a successor for the address: the tree is in it, and uniqueness
  is per parent.
- **Page** is redefined: it no longer belongs to a project, it may have
  children, and "no per-page access" now means "access is the space's".
- One central access check knows two brackets. For an agent it subtracts every
  closed space, on every route — list, search, direct read, and whatever
  interface comes later.
- The instructions become a field on the project and leave the wiki behind.
- Nothing in the tracker reaches into a space: no issue, no epic, no release, no
  key, no label.
- None of this touches 1.0. It is built after the three cuts of
  [ADR 0009](./0009-the-mvp-is-built-in-three-cuts.md), as the page already was.
