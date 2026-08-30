# planaffe Logs Into logaffe, and Serilog Is the Way Out

The application logs through `Microsoft.Extensions.ILogger` and ships with two
sinks wired from the first commit: **logaffe is the intended target**, enabled as
soon as an endpoint and a token are configured, and **Serilog to console and a
rolling file** is what an installation gets otherwise. No call site knows which
one is active.

logaffe is the sibling product, self-hosted the same way, built for exactly this
audience — and running planaffe on it from day one is the fastest way to find out
what its client packages get wrong. But planaffe is MIT software other people
install, and a logging tool that has to be installed alongside it would be a
second required container in a product whose case is that it needs two. So the
dependency is a configuration value, not an assumption: unconfigured, planaffe
logs to stdout like every other container, and Serilog's file sink covers the
operator who wants to keep something without running anything.

Serilog rather than the framework's built-in console alone, because structured
logging is what makes the logaffe path and the local path the same log — the
same message template, the same properties, one of them shipped and one of them
written down.

## Consequences

**The logging configuration is three environment variables**, in the shape the
rest of the settings already have: an endpoint, a token, and a minimum level.
Nothing else is configurable, and there is no provider model to extend.

**A log target that is unreachable never becomes an outage.** The logaffe client
queues in memory, drops the oldest under pressure, and neither throws into the
caller nor blocks it. A ticket system does not stop accepting tickets because a
log server is down.

**Nothing about a request body is logged.** Issue content is written by agents
and quoted from elsewhere (VISION 13); it belongs in the database and in the
response, not in a log line that leaves the installation.
