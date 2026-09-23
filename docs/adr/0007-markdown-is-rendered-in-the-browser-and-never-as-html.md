# Markdown Is Rendered in the Browser, and Never as HTML

Issue descriptions, results, comments, questions and release notes are Markdown
in the database, they travel over the API as Markdown, and the browser renders
them — with a pipeline that parses Markdown to a component tree and never
interprets embedded HTML.

The alternative is to render on the server and ship HTML, which is what a
Blazor-shaped product would do and what most content systems do. Two things rule
it out. The API is the product's public surface and the CLI is its other client:
an agent asking for an issue wants the Markdown it can edit and re-post, not a
rendering of it, and a server that answers HTML makes every consumer strip it
back. And the content is written by agents, quoting logs, errors and code from
places nobody vetted — the vision states plainly that ticket content is not
trustworthy (VISION 13). HTML on the wire turns that into an injection surface
that has to be sanitised correctly in one place forever; Markdown parsed to
components on the client, with raw HTML disabled and no `dangerouslySetInnerHTML`
anywhere, has no such surface to defend.

The pipeline is `react-markdown` with `remark-gfm` — tables, task lists,
strikethrough and autolinks, which is the dialect everything else in this
workflow speaks — plus syntax highlighting for fenced code, because the code
block is what agents fill tickets with.

## Consequences

**No WYSIWYG editor.** What is stored is Markdown, and what is edited is
Markdown — which is what the humans in the target group want and the only thing
an agent can write anyway (VISION 3, 6.1). That is a decision about the form of
editing and not about libraries: an editor that edits Markdown as source and
helps while doing it keeps it; one that holds HTML or a document model of its
own and derives Markdown on save does not. How a dependency for such an editor
is decided is [ADR 0023](./0023-a-dependency-is-a-decision-a-human-takes.md).

**Rendering cost sits on the client**, which is fine for one issue and would not
be for a list — so lists show titles and metadata, never rendered bodies, and a
long description is collapsed after the first paragraphs (VISION 6.2).

**Links in issue content are foreign links.** They open with `rel="noopener
noreferrer"`, and the renderer refuses schemes other than `http`, `https` and
`mailto`.

## Addendum: images are never loaded

The decision above settled links and said nothing of images, and the pipeline
filled the gap the library's way: `![alt](url)` became an `<img>` the browser
fetched the moment an issue was opened. Content here is written by agents and
not trusted (VISION 13), so one line in a ticket was enough to learn the
address, the time and the instance of everybody who read it.

**Image syntax is rendered as a foreign link and never loaded.** The link goes
to the image's URL, is labelled with its alt text — with the URL itself where
the alt text is empty — and is judged like every other link: the same three
schemes, the same `rel="noopener noreferrer"`, and text where the scheme is
refused. Nothing is requested from the server behind it, and that holds for
every URL, the instance's own origin included; the renderer drops an image
source even where one slips past the rewrite.

Loading from the own origin only was considered and is the same decision in
practice: there is no upload, so nothing on the own origin is an image anybody
put into a ticket. Loading everything with `referrerpolicy="no-referrer"` was
refused because it keeps the pixel — the request itself is what gives the
reader away. The MVP is text only (VISION 6); when uploads come after it, this
is decided again.
