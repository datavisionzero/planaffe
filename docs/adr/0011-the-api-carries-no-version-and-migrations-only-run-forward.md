# The API Carries No Version, and Migrations Only Run Forward

There is no `/api/v1`. Endpoints sit at their plain paths, the installation names
its version in a response header and under `GET /version`, and the CLI sends its
own version with every request. Migrations apply themselves on startup and only
ever forward; there is no downgrade path.

The alternative is the versioned prefix every public API carries. It solves a
problem this product does not have: a provider who cannot reach the clients and
therefore has to keep the old shape alive beside the new one. Here the
installation and both its clients belong to the same operator, the CLI is
distributed as a binary cut from the same tag as the server (`docs/codebase.md`),
and the contract is checked in and verified against the running installation on
every build ([ADR 0005](./0005-the-contract-is-checked-in-and-both-clients-are-generated-from-it.md)).
A version in the path would be a promise to maintain two shapes, made by a
project that has stated it will not.

What the operator actually runs into is **version skew**: the CLI is updated by a
package manager while the installation is three months old, or the other way
round. A prefix does not help with that either — it turns skew into a 404 on an
endpoint the client believes exists. Naming the versions on both sides does help,
because the mismatch can then be reported as what it is.

**Migrations run forward only** because the alternative is a downgrade path
nobody exercises. A reverse migration that has never been run against real data
is not a safety net, it is a second thing that can be wrong at the worst moment.
The honest answer is the one VISION 16 already gives: operations consist of
Postgres backups. `pg_dump` before an upgrade is the way back, and the
documentation says so where somebody will read it.

## Consequences

**Skew produces a message, not a mystery.** When the CLI is too old or too new for
the installation, it says which versions are involved and what to do, under an
exit code of its own — distinguishable from a network failure, as VISION 6.1
requires of every error an agent has to react to.

**The compatibility promise is narrow and stated.** A CLI talks to installations
of its own minor version and older within the same major. Widening that later is
possible; claiming it now would be claiming test coverage that does not exist.

**Breaking changes are allowed until 1.0** and are named in the changelog. After
1.0 they need a major version, and that is the only version number this product
has.

**An upgrade is one-way, so the documentation leads with the backup.** Not as a
footnote — as the sentence before the upgrade command.
