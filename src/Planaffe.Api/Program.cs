// The composition root, and for now the host and nothing else. Endpoints,
// authentication and the log sinks arrive with the code they belong to rather
// than as empty registrations placed here in advance.
//
// The OpenAPI document is captured from a running installation and checked in
// (ADR 0005), which is why nothing in this build generates it.

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Run();
