# The Frontend Is React, Not Blazor

planaffe is a .NET product, so Blazor is the frontend a reader expects and its
absence needs a reason. Blazor Server is ruled out by the deployment the vision
promises: a stateful circuit over a persistent WebSocket per open tab, in a
product whose whole operational story is "two containers and `pg_dump`", buys a
reconnect and proxy-timeout problem for a UI that mostly renders text. That
leaves Blazor WebAssembly, which does satisfy the API-first shape, against React.

React wins on the two screens that decide this product. The issue list is dense,
filterable, keyboard-driven and expected to feel like Linear (VISION 6.2), and
the issue detail is a Markdown document with a shell around it — virtualized
lists, command palettes and Markdown pipelines are solved, well-maintained
components in React and a thin field in Blazor WASM. The runtime download is the
second argument: a UI whose job is to be opened quickly on a phone to triage one
ticket should not pay for a .NET runtime on every cold load.

## Consequences

The repository carries a second toolchain and the frontend cannot share C# types
with the backend — the contract has to be written down and kept honest by tests
rather than by the compiler. That cost was owed anyway: the vision fixes the web
UI as one client of the same public API the CLI uses, and
[ADR 0005](./0005-the-contract-is-checked-in-and-both-clients-are-generated-from-it.md)
is how it is paid — once, for both clients.

The frontend is built by its own toolchain and joined to the backend in exactly
one place, the `Dockerfile`, so that `dotnet build` never needs Node.
