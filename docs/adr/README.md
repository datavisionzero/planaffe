# Architecture Decisions

This directory contains decisions that are difficult to reverse, would be
surprising without context, and resulted from a genuine trade-off. Product scope
and promises belong in the [product vision](../../VISION.md) instead, which also
carries the roster of the technical direction — an entry there names what was
chosen, an ADR here explains why the obvious alternative was not.

## Naming

ADRs are numbered sequentially as `NNNN-short-slug.md`. The next number follows
the highest existing number.

## Short form

```md
# Short decision title

One to three sentences describe the context, decision, and rationale.
```

Status, considered options, and consequences are included only when they add
material value to understanding the decision.

## Decisions

- [0001 – The repository is a trunk](./0001-the-repository-is-a-trunk.md)
- [0002 – The backend is four layers, not one project](./0002-the-backend-is-four-layers-not-one-project.md)
- [0003 – The CLI is Go, not a second .NET binary](./0003-the-cli-is-go-not-a-second-dotnet-binary.md)
- [0004 – The frontend is React, not Blazor](./0004-the-frontend-is-react-not-blazor.md)
- [0005 – The contract is checked in, and both clients are generated from it](./0005-the-contract-is-checked-in-and-both-clients-are-generated-from-it.md)
- [0006 – The web application is a shell before it is a screen](./0006-the-web-application-is-a-shell-before-it-is-a-screen.md)
- [0007 – Markdown is rendered in the browser, and never as HTML](./0007-markdown-is-rendered-in-the-browser-and-never-as-html.md)
- [0008 – planaffe logs into logaffe, and Serilog is the way out](./0008-planaffe-logs-into-logaffe-and-serilog-is-the-way-out.md)
