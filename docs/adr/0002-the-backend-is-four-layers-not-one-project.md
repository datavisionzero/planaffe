# The Backend Is Four Layers, Not One Project

The backend is `Planaffe.Domain`, `Planaffe.Application`, `Planaffe.Infrastructure`
and `Planaffe.Api` on .NET 10, with dependencies pointing inward and the compiler
holding them there. The obvious alternative is one project with folders named
after features, and for a product whose entire case is being small it is a
serious one: four projects for an issue tracker two people run is a great deal of
structure, and every feature touches three or four of them.

What decided it is a promise the vision already made. Guiding principle 1 says every
function a human has in the UI is reachable from the CLI, and the CLI is a client
of the same public API — so there is one set of use cases with more than one
adapter over it from the first day, and an MCP server is already named as the
next one. Layering turns "they cannot drift" from discipline into a compile unit:
claiming the next ready issue is one type in Application, and no adapter can
reach the database on its own to grow a capability the others lack.

The second reason is that the hardest rules in this product are rules, not
plumbing. When is an issue *ready* (VISION 10), when has a claim expired (11),
what happens to a released issue that is reopened (7) — every one of those is a
sentence in the vision that has to hold no matter which adapter asks. They belong
in a project that references nothing.

## Consequences

**The risk taken on is an anemic domain** — four projects in which
`Planaffe.Domain` holds nothing but data classes and every rule lives a layer up.
The guard is a rule that can be checked by reading: **anything the vision already
states as a rule belongs in Domain.** The seven conditions of readiness, the
four-hour claim expiry, the priority scale and its ordering, the status set and
what closes an issue, the label group that admits one value — all of that is of
that kind. If those end up in Application, the innermost project is ballast and
this decision was not worth its cost.

**Atomicity is Infrastructure's problem, and it is why the port is coarse.**
"Fetch the next ready issue and claim it" is one use case and one round trip, a
conditional update in one transaction (VISION 10, 11). Application asks for that
act, not for a query followed by a write — a port that let a caller assemble the
two would be the race the product exists to prevent.

**A new field touches every layer.** That is the recurring price, and it is
paid on exactly the kind of change the vision says should be rare, because the
field set is closed by guiding principle 3.
