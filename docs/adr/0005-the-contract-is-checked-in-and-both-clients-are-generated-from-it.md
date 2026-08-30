# The Contract Is Checked In, and Both Clients Are Generated From It

`docs/api/openapi.json` is the HTTP contract, it is a file in the repository, and
neither client writes request or response types by hand: the web client's API
layer is generated from it with `openapi-typescript`, and the Go client of `pa`
with `oapi-codegen`. Both are generated at build time and neither is committed —
the document is the artifact, its output is not.

The alternative is what most projects do: hand-written `fetch` wrappers on one
side and a hand-written HTTP client on the other, with the OpenAPI document
published for outsiders as a description of what the server happens to do. That
is exactly the arrangement that lets a renamed field reach production, and it
gets worse here than elsewhere, because this product has **two** consumers of the
same API written in two languages that share nothing with the server
([ADR 0003](./0003-the-cli-is-go-not-a-second-dotnet-binary.md),
[ADR 0004](./0004-the-frontend-is-react-not-blazor.md)). Generating both from one
document is what puts a compiler back between the server and its clients — and a
field the backend removed becomes a build failure in two places instead of a
support question.

**The document is captured from a running installation, not written by hand.**
The backend's endpoint definitions produce it, CI starts the installation against
a Postgres, fetches the document it serves, and fails if it differs from the one
checked in. Hand-maintaining the document would make it a wish; generating it
without checking it in would make a breaking change invisible in review. Checked
in and verified, the diff of a pull request shows the contract changing.

## Consequences

**A change to a response shape is a three-part commit**: the endpoint, the
regenerated document, and whatever the two clients now fail to compile against.
That is the point, and it is the cheapest moment to find out.

**The document is also the public API documentation**, which the vision promises
to anyone scripting against an installation (VISION 6.3). One artifact serves the
generator, the test and the reader.

**Generated code stays out of the repository.** Both generators run before build,
typecheck and test, so a working tree is never a state where the clients agree
with a stale contract.
