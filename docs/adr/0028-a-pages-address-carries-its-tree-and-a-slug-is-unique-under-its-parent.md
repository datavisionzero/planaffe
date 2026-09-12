# A Page's Address Carries Its Tree, and a Slug Is Unique Under Its Parent

A page in a space is reached by the space and then the slugs from the root
down — `/spaces/handbook/pages/company/onboarding` — at most three levels deep.
The slug stays what [ADR 0021](./0021-a-pages-address-is-its-slug-not-a-key.md)
made it, a name rather than a key, and it is now unique under its parent
instead of within a project. This adds to that decision; it reverses nothing
in it.

## Why

The knowledge base's page hangs in a space and under other pages
([ADR 0027](./0027-the-knowledge-base-hangs-on-a-space-not-on-a-project.md),
VISION 18), and an address has to name exactly one page. Once pages nest, a
single slug no longer does: `overview` is a reasonable name under `company` and
just as reasonable under `product`, and a namespace flat enough to forbid the
second one would push the tree back into the names — `company-overview`,
`product-overview` — which is the hierarchy written out by hand, with none of
its navigation.

So the address carries the path. That is also the only address a reader can
guess from the tree they are looking at, which is the property ADR 0021 bought
the slug for in the first place: a page is referred to in running text, and a
reference is worth something when it says where the page sits.

Uniqueness moves down with it. Under one parent two children cannot share a
slug, because the address would name both; under different parents they may,
because the address tells them apart. A page directly under the space is
unique among the other pages directly under the space — the space is the parent
there, and the rule is the same one.

## Why not the alternatives

**Keep the slug unique within the space and leave it out of the address.** One
segment stays in the URL, every page is reachable by `/spaces/x/pages/slug`,
and renaming a parent moves nothing. But the tree then has no address at all:
two siblings in different branches fight over `overview`, and the first author
to take a common word takes it from the whole space. It also makes the address
say less than the reader already knows.

**Give the page a key after all, now that the address is long.** This is
ADR 0021's question a second time, and its answer has not changed: the page is
the object that is named in prose rather than quoted in a commit. A key would
buy stability for exactly the reference that does not want it, and the tree
would still need an address for humans beside it.

**Redirect the old address after a rename or a move.** Refused for the reason
ADR 0021 gives, and more strongly here: with a tree, every move of a parent
would have to leave a forwarding entry for every page beneath it, so the
redirect table would grow by subtrees rather than by pages. VISION 7 names it
as the first step of the sprawl the whole section is written against.

**Address the page by an opaque id and keep the path as decoration.** Then two
addresses exist for one page, and links written by hand and links copied from
the browser would differ forever. `CONTEXT.md`'s **Number** entry exists to
stop this kind of doubling.

## Consequences

**A rename and a move both break links, and the depth limit is what keeps that
small.** Renaming a page changes the address of every page under it. Nothing
repairs the bodies that mention it, exactly as ADR 0021 accepted for a flat
wiki — but a subtree is more than one page, so the cost is real. Three levels
is the ceiling that keeps it bounded, and the history keeps both addresses.

**A slug is validated per parent, and the database says so.** The unique index
is over the space, the parent and the slug together, and it covers deleted rows
for the reason ADR 0013 gives: a slug stays spent until the purge, so a restore
never lands on a name somebody else has taken meanwhile.

**Moving a page can be refused where renaming it cannot.** A slug that is free
under the old parent may be taken under the new one, and a subtree that fits at
its current depth may not fit under a deeper parent. Those are refusals of the
move, not of the page, and they say which of the two happened.

**A project's page keeps ADR 0021 unchanged while it exists.** Its slug is
unique within the project and its address carries no tree. The two rules stand
side by side until the project's wiki is withdrawn (VISION 18), and then only
this one is left.
