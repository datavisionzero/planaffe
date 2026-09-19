# Logging Uses Framework Providers

planaffe logs through `Microsoft.Extensions.Logging`. Its JSON console provider
is always on, and `Logaffe.Extensions.Logging` delivers the same entries to
logaffe when both an endpoint and ingest token are configured. This supersedes
[ADR 0008](./0008-planaffe-logs-into-logaffe-and-serilog-is-the-way-out.md):
Serilog and the rolling file are removed.

The direct provider follows the approach used by payaffe. It builds structured
entries from `ILogger` message templates, includes scopes and request trace IDs,
and holds a bounded queue when logaffe cannot be reached. Console output stays
available through the container log even when delivery fails. A single request
line records method, path, status and duration without query strings or bodies.

The three existing environment variables remain: endpoint, token and minimum
level. Both delivery settings must be present or absent. The level now uses
framework names; `Verbose` and `Fatal` remain accepted as aliases for `Trace`
and `Critical` in existing instance configurations. The dependency was approved
under [ADR 0023](./0023-a-dependency-is-a-decision-a-human-takes.md):
`Logaffe.Extensions.Logging` 0.5.0 and its `Logaffe.Client` dependency are MIT,
and the logging abstractions are already part of the .NET host. None reaches
the browser.
