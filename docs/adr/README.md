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
- [0009 – The MVP is built in three cuts, and the first one ends at the switch-over](./0009-the-mvp-is-built-in-three-cuts.md)
- [0010 – The product speaks English, and only English](./0010-the-product-speaks-english-and-only-english.md)
- [0011 – The API carries no version, and migrations only run forward](./0011-the-api-carries-no-version-and-migrations-only-run-forward.md)
- [0012 – A list returns a slim issue, and only a single issue is complete](./0012-a-list-returns-a-slim-issue-and-only-a-single-issue-is-complete.md)
- [0013 – Deleting is a soft delete with a floor, and identities are never deleted](./0013-deleting-is-a-soft-delete-with-a-floor-and-identities-are-never-deleted.md)
- [0014 – Review is a status, and whether a close lands there is a project switch](./0014-review-is-a-status-and-a-project-switch.md)
- [0015 – A token is an agent or a user's key, and an agent is never an administrator](./0015-a-token-is-an-agent-or-a-users-key-and-an-agent-is-never-an-administrator.md)
- [0016 – Status transitions are acts, not a field you write](./0016-status-transitions-are-acts-not-a-field-you-write.md)
- [0017 – The web application is drawn by Tailwind and Base UI, in components the repository owns](./0017-the-web-application-is-drawn-by-tailwind-and-base-ui-components-the-repository-owns.md)
- [0018 – Transactional email is an optional instance capability](./0018-transactional-email-is-an-optional-instance-capability.md)
- [0019 – Triage required selects, it does not permit](./0019-triage-required-selects-it-does-not-permit.md)
- [0020 – A newline is a line break, and stored text is not hard-wrapped](./0020-a-newline-is-a-line-break-and-stored-text-is-not-hard-wrapped.md)
- [0021 – A page's address is its slug, not a key](./0021-a-pages-address-is-its-slug-not-a-key.md)
- [0022 – A comment can be corrected and withdrawn by its author](./0022-a-comment-can-be-corrected-and-withdrawn-by-its-author.md)
