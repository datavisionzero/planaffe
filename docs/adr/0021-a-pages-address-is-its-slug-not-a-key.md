# A Page's Address Is Its Slug, Not a Key

A page is reached by a name that is unique within its project —
`/PLAN/pages/architecture` in the browser, `/projects/PLAN/pages/architecture`
over HTTP. It gets no key and no number, it is the only object in the product
that does not, and renaming it is allowed: the old slug leads nowhere
afterwards and the rename stands in the history.

## Why

A key is the right address for a thing that is talked about while it is being
worked on. `PLAN-42` is short, it survives every edit of the title, it sorts,
it can be typed from memory into a commit message, and it is stable precisely
because it says nothing about the content. Everything in section 7 of
[`VISION.md`](../../VISION.md) that carries one is that kind of thing: an
issue, an epic, a release.

A page is the other kind. It is referred to in running text — "the conventions
are on `architecture`", "read `onboarding` first" — by people and by agents
writing for people, and the reference is worth something only if it carries the
subject. `PLAN-P7` would be an address nobody could read and everybody would
have to look up, which is the same complaint `CONTEXT.md` makes against using a
number where a name belongs. The page is also the one object whose title is
expected to describe its whole content, so a name derived from that title is
not a second identity to keep in sync — it is the identity.

That the address then changes when the page is renamed is a real cost, and it
is the smaller one. A wiki whose pages cannot be renamed accumulates names that
lie, and the reader pays for that on every visit.

## Why not the alternatives

**Give the page a key like everything else.** Consistent, and it would have
kept `CONTEXT.md` free of an exception. But it buys stability for a reference
that is made in prose, where an unreadable address is worse than a fragile one,
and it would make the wiki the one place in the product where you cannot guess
an address you have read once.

**A key for the address and a slug for humans.** Two identities for one object,
and every link in every page body would then be written in whichever of the two
its author preferred. The product would have to answer both forever, and the
"Number" entry in `CONTEXT.md` exists to stop exactly this kind of doubling.

**Keep the old slug alive as a redirect.** This is what a large wiki does, and
what this one must not become: a redirect table is a second, growing namespace
that nothing ever prunes, in a product whose sixth guiding principle is that
features are refused by default. VISION 7 already names it as the first step of
the sprawl the section is written against. A rename is rare, it is visible in
the history, and the full-text search finds the page under its new name
immediately.

**Never let the slug change.** Cheaper than a redirect and worse than both: the
name is then wrong forever, and whoever wants it right copies the body into a
new page and deletes the old one — a rename with the history thrown away.

## Consequences

**`CONTEXT.md` carries the exception, not a footnote.** **Slug** stands next to
**Number** in the glossary and is defined against it, so that a reader who
meets both learns why one object is addressed differently rather than
suspecting an oversight.

**A link between pages can break.** Rename `architecture` and a body that
mentions it keeps the old word. Nothing repairs it, and nothing pretends to:
the search finds the page, and a page is a document a human maintains, not a
graph the product keeps consistent.

**The slug is validated, not generated.** It is given when the page is created
and changed by renaming; the product does not derive it from the title behind
the author's back, because a title is a sentence and an address is not. Which
characters a slug may carry belongs to the HTTP contract, not here.

**Uniqueness is per project, like everything else.** Two projects may both have
`architecture`, and neither knows about the other — the project is in the
address in both directions.
