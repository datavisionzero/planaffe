# The Product Speaks English, and Only English

The interface, the CLI output, every error message, the documentation and the
README are English. There is no localisation, no language switch, and no
machinery held ready for one — no string tables, no resource files, no keys
standing in for text. Strings sit where they are used.

`AGENTS.md` already binds the repository to English, and the status values, the
label groups and the vocabulary fixed with the vision are English. What was not
written down is what the running product says to the person in front of it, and
that is a separate question: a German-speaking maintainer building a tool
primarily for themselves has an obvious reason to answer it differently.

It is answered this way because of who the product is for beyond that maintainer:
an audience of developers working with agents, which is international and reads
English tooling all day. A second language would also have to reach the CLI, and
a CLI whose error messages change with a locale is a CLI whose exit codes and
messages an agent cannot be told about once (VISION 6.1).

Holding the machinery ready "in case" was rejected separately. That is the
configurability reflex guiding principle 3 rules out: it costs indirection in
every line of the product for an option nobody has asked for. Extracting strings
later is mechanical work, and it happens when a second language actually has an
addressee.

## Consequences

**The maintainer gets an English interface for their own tool.** That is the
concrete cost, and it is accepted.

**Working notes stay German where they help.** `scratchpad/` is exempt by
`AGENTS.md`, and drafting in the language you think in is faster. What crosses
into the repository is translated, as it already was for the vision and the
research documents.

**Nothing is walled off.** This decision is about the MVP and about not building
scaffolding in advance. If a second language ever has a real addressee, it is
reopened with the extraction work in front of it — which is roughly what it would
have cost anyway.
