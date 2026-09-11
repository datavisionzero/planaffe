# The Human Interface

This document fixes the screens, the individual actions available in the web
application, and the coarse permission boundary for cut three. It complements
the product intent in [`VISION.md`](../VISION.md); the HTTP operations remain
defined in [`api.md`](./api.md).

Bulk issue changes, export and all three waiting operations remain CLI work.
There is no board and no rich-text editor. There is one screen above the
projects — the overview, which grades what is waiting for a human and never how
long a backlog is
([ADR 0024](./adr/0024-the-overview-grades-attention-not-backlog-size.md)); it
is the only place the application answers across projects, and it is not a
dashboard of numbers about the work. Every Markdown editor is the
same field over Markdown source, and every ordinary human action on one issue is
available in the browser. Enter in that field is a line break where it is
read, so nothing has to be known about Markdown to write one
([ADR 0020](./adr/0020-a-newline-is-a-line-break-and-stored-text-is-not-hard-wrapped.md)).

That field is one component, so what is said of it holds in all eight places it
is used — an issue's description, a comment, a question, an answer, a result, an
epic's description, a page's body, a release's notes. It shows the structure of
the text while it is typed, continues a list on Enter and ends it on an empty
item, and carries a toolbar and the keys for the marks nobody wants to spell
out; ⌘/Ctrl+Enter saves from inside it, so a comment is written and sent without
the hands leaving the text. A description or a page body opens as a surface
rather than as six lines and grows with what is written; a comment opens small
and grows the same way. The preview stands beside the text where the window is
wide enough and takes its place where it is not, and a button pulls the same
field over the whole window with the preview beside it — Escape closes that and
stops there, because the form behind it answers Escape too. What is edited is
Markdown and never a document model of its own
([ADR 0007](./adr/0007-markdown-is-rendered-in-the-browser-and-never-as-html.md)).

## Screen matrix

| route | screen | primary content | narrow-screen behaviour |
|---|---|---|---|
| `/login` | Sign in | email, password, recovery link | centred single column |
| `/activate` | Accept invitation or bootstrap | name/email context and password setup | centred single column |
| `/recover` | Recover password | request form or new-password form | centred single column |
| `/device` | Sign in a console | the code a terminal printed, what confirming grants, and the two decisions | centred single column |
| `/projects` | Overview | one tile per project: its standing, the reason for it and its three counts, worst first | one column of tiles |
| `/projects/new` | Create project | immutable key, name and two project switches | centred single column |
| `/:project/ready` | Ready | shared issue list with workable defaults | two-line rows, no horizontal scroll |
| `/:project/in-progress` | In progress | shared issue list filtered to active claims | two-line rows, no horizontal scroll |
| `/:project/needs-you` | Needs you | question, review, unready and stuck reasons, and a line where no agent could pick work up | reason and next action stay visible |
| `/:project/issues` | All issues | shared issue list, all URL filters, and the four sorts — by epic it groups | filters open as a dismissible sheet |
| `/:project/issues/new` | Create issue | title, Markdown description, priority, status, `ready`, and the five choices — labels, epic, parent, assignee, blockers | two columns from `lg`: title and description in the wide one, the eight other controls in a narrow column beside them; one column below that, in the same order, with the pairs stacked, chips wrapped and each suggestion list below its field |
| `/:project/issues/:number` | Issue | sticky action bar, what needs attention, description and result, then tabs; editing it is guarded and a conflict is shown, not lost; a comment carries its author's edit and delete | one column; metadata as chips under the title |
| `/:project/epics` | Epics | open epics, progress and recent activity | stacked summaries |
| `/:project/epics/new` | Create epic | title, Markdown description and the label choice | one column; the same form the epic screen edits in place |
| `/:project/epics/:number` | Epic | Markdown description, progress and issue list; editing it is guarded and a conflict is shown, not lost | one column |
| `/:project/pages` | Pages | the project's flat wiki, by slug, with who touched what last | the slug and the title stay, the rest folds away |
| `/:project/pages/new` | Create page | the slug, the title, the Markdown body and the label choice | one column; the same form the page screen edits in place |
| `/:project/pages/:slug` | Page | the rendered Markdown, then renaming and deleting; editing it is guarded and a conflict is shown, not lost | one column |
| `/:project/releases` | Releases | `unreleased`, then published releases newest first | stacked summaries |
| `/:project/releases/:name` | Release | notes, exact issue membership and publish/copy actions; on the open release each row can be taken out, on the newest publication rename and take back | one column |
| `/:project/labels` | Labels | the project's set, grouped where its labels exclude one another, with the create line above it | the row stacks: the name and what it means above the two acts |
| `/settings/profile` | Personal settings · Profile | name and email address | area list folds above the area |
| `/settings/security` | Personal settings · Security | password and browser sessions | area list folds above the area |
| `/settings/tokens` | Personal settings · User tokens | the tokens that still work, revoked ones on request, and the secret of a new one, once | area list folds above the area |
| `/settings/agents` | Personal settings · Agents | the agents and their tokens, the revoked ones on request; each row's acts — rename, rotate the token, revoke — in its menu | area list folds above the area |
| `/:project/settings/general` | Project settings · General | name, the two switches and project deletion | area list folds above the area |
| `/:project/settings/instructions` | Project settings · Instructions | which page of the wiki every agent is handed with every ticket, and a link into it | area list folds above the area |
| `/:project/settings/members` | Project settings · Members | who has access to the project | area list folds above the area |
| `/admin/users` | Administration · Users | invite, role and lifecycle, searchable and filtered by state; each row's acts in its menu | area list folds above the area |
| `/admin/projects` | Administration · Projects | the instance's live projects, searchable and sortable; deleted ones on request, restored or deleted from the row | area list folds above the area |
| `/admin/projects/:key` | Administration · One project | who has access to it, and granting or removing it | area list folds above the area |
| `/admin/email` | Administration · Transactional email | the SMTP status and a test message | area list folds above the area |

`:number` is the part of a key after its project prefix ([`CONTEXT.md`](../CONTEXT.md)):
`PLAN-42` is at `/PLAN/issues/42`, `PLAN-E3` at `/PLAN/epics/E3`. A link to an
issue or an epic takes its project from the key it names and not from the
address it sits on, so a blocker in another project leads to that project.
`:slug` is the exception the product has exactly one of
([ADR 0021](./adr/0021-a-pages-address-is-its-slug-not-a-key.md)): a page is
addressed by its name, so `/PLAN/pages/architecture` is the whole address and
there is no number to look up.

The application shell persists around every project route. Its project switcher
contains only projects the caller can access. The shell binds five shortcuts:
`⌘K` opens the command palette, `⌘B` folds the sidebar, `p` opens the project
switcher, `c` creates an issue in the project the frame is standing in, and `?`
opens the overview of every key the application binds. `p`, `c` and `?` are bare
keys, because `⌘P` belongs to the browser's print dialog and an issue tracker is
worth printing. Creating belongs to the project rather than to a list of it, so
`c` answers on every screen of a project and not only on the three that are
issue lists. Non-administrators do not see the admin entry,
but hiding navigation is never the authorization check.

"Needs you" and "In progress" in that navigation carry counts — what is waiting
for a human, and what is being worked on — so that a reader standing on another
view knows where to look. They are numbers on links and not notifications:
nothing is sent, nothing is addressed at anybody, and there is no read state.
At zero there is no badge, because a counter showing zero is not a signal; past
ninety-nine it says `99+`; and where the instance did not answer there is no
badge either — the navigation is the frame and carries no error. Each count is
read from the list it stands on rather than from a counter of its own, and it
belongs to the name of the link, so a screen reader says "Needs you, 3" instead
of reading two fragments in a row.

The other two views carry none. "All issues" would show a number that is always
there and barely moves, which is decoration and takes the attention away from
the two that mean something, and the count of what is ready belongs to the
agent, which reads it through `pa next` and not through a navigation. The two
badges also look alike: "Needs you" is the request and "In progress" the
observation, and which of them is the more pressing is what the screen says,
not a second colour in the frame.

They stay current without anybody reloading the page. The frame holds one read
per list against the wake channel `pa needs-you --wait` uses (`docs/api.md`,
Waiting) — one connection per list and tab, which the "Needs you" screen shares
rather than opening a second, and which is given up while the tab is in the
background and taken up again the moment it is looked at. Where the instance
stops answering, the number stays as it last was and the loop tries again with
a growing pause: a navigation that flickered at every hiccup would be worse
than one that is a few seconds behind.

The overview is a dialog rather than a screen, so that a key can be looked up
without leaving the list it is about. It is reached three ways — `?`, the
command palette, and an entry in the account menu — because a list of shortcuts
reachable only by a shortcut does not help the reader who has not found one yet.
It draws `⌘` on a Mac and `Ctrl` elsewhere, and it is generated from the one
list the handlers themselves read, so a key that changes changes there.
The Labels navigation entry required by ADR 0006 opens the project's labels
screen; there is no second place labels are managed from, and no section of
project settings pretending to be one.

The three administration screens — personal settings, project settings and
instance administration — are one shell: a list of areas beside the area being
looked at, each area with an address of its own, so that one can be linked to
and a reload comes back to it. `/settings`, `/:project/settings` and `/admin`
still work and lead to the first area of each. On a narrow screen the list
folds above the area rather than beside it. What the instance answered stands
at the act that asked rather than at the foot of the page, and a row's acts
live in the row's own menu.

The epic's description is a living document a human and an agent both edit, so
the form sends `If-Match` with the version it opened with. A refusal is not a
dead end: the typed text stays in the field, the version the instance handed
back is adopted for the next attempt, and the other version's description is
shown so it can be merged by hand. Saving again is then a decision to overwrite
it rather than a request that can only fail. The page editor is the same
mechanism on the same reasoning, and for the same reason: a wiki is the text two
people are most likely to be in at once.

**Everywhere `If-Match` is sent, what comes back is taken.** That is the whole
of what the refusal is for (`docs/api.md`, "Concurrency on text fields"), and a
screen that drops it turns the guard into a trap. The issue mask does what the
epic's does, with one difference that belongs to the issue: beside the title and
the description it writes seven fields chosen from lists, and saving writes all
of them. So the other version's title and description are shown to be merged
from, and the fields that also differ are named — priority, status, ready,
labels, epic, parent, assignee — because whoever is about to overwrite somebody
else's label has to know they are. Where nothing is typed there is nothing to
merge and nothing to show: the ready switch on the issue and the triage button
on "Needs you" take the version, say that it changed, and let the same press
work the second time.

## Overview

The one screen that answers across projects, and the one `/` lands on. A tile
per project, and the tile says how that project stands: its **standing**
([`CONTEXT.md`](../CONTEXT.md)), the reason that produced it, and the three
counts underneath — in progress, ready, open. Which is to say what "Needs you"
already says for one project, said for all of them at once, for the reader who
has four and can otherwise only look at one at a time.

The five steps are `clear`, `running`, `idle`, `waiting` and `neglected`, worst
applicable wins, and what grades them is what waits for a human and whether an
agent could still take work — never how many issues are open. That is ADR 0024,
and it is why the open count is a number on the tile and colours nothing.

**The tile never rests on colour**, the way an issue row already marks its
status and its priority twice over: the step carries a symbol, its word and the
reason in a line of its own — "2 questions, oldest 6 days", "1 in review",
"nothing ready, no agent". `idle` names which of its three cases it is, because
no agent at all, work behind blockers and work that is simply not workable are
repaired in three different places. At `clear` the line says nothing is open rather than
printing three zeros.

**Tiles sort by standing, worst first**, then by the older entry, then by
project key: what needs a human is the first thing under the reader's eye. The
whole tile is the link, and it leads to the reason rather than to the project —
`neglected` and `waiting` to that project's "Needs you", `idle` to its "Ready",
the two good steps to where the project opens anyway.

**`/` lands here, with one exception**: a reader with a single project is taken
straight into it, because an overview of one tile is decoration. `/projects/new`
keeps its own meaning and is not swallowed by the route above it. The way back
is the project switcher, as a row above the projects, and the command palette;
the sidebar belongs to one project and gets no entry that leads out of it. It
is called Overview in both of them and in its own heading — the name this
section carries — so that nothing has to be recognised twice.

**It holds no connection open.** One read carrying `If-None-Match`, repeated
when the tab is looked at again and on an interval while it is, and not at all
while it is in the background. The wake channel is one project's
([`api.md`](./api.md), Waiting), so ten projects would be ten held connections
for a screen that is glanced at rather than sat on. Where the instance stops
answering the tiles stay as they were and the next attempt comes with a growing
pause — what the navigation counts already do.

**Reaching `clear` is celebrated**, once, and only when the reader was there to
see it change: confetti over the screen for about a second, a smaller one at
the tile for the step out of `waiting` or `neglected` into `running`, and
nothing at all on first load, which would spend the gesture on arriving rather
than on finishing. A reader who asked for reduced motion gets the sentence and
no motion. The celebration carries nothing that has to be read, so it is
`aria-hidden` and takes no pointer.

## Pages

The wiki is flat and stays flat: a list ordered by slug, no tree and no table of
contents, because the full-text search is what a hierarchy would have been for.
The screen is therefore a list and a document, and the body is written in the
same Markdown field as everything else here — no WYSIWYG, and a page gets no
editor of its own.

The search stands above the list rather than behind a filter sheet, and it and
the label filter both live in the URL, so a pasted link says what it shows. An
empty wiki and a filter that matched nothing are different states: the first
says what a page is for, the second says nothing matched.

Two acts are not fields in that form. **Renaming** moves the address, and
nothing forwards: it is a dialog that says so, because a link written to the old
slug stops working and whoever renames should be told once. **Deleting** says
that the slug stays taken while the page can still come back, which is what
keeps a restore from landing on a name somebody else took. Both stand under the
document rather than in the header, where the thing one usually wants is
"Edit".

## Issue list and detail

Ready, In progress and All issues are presets over one cursor-paginated,
virtualized component. Search, status, priority, label, epic, assignee, claim,
author, blocked, `ready`, sort and order live in the URL. The server supplies
filter choices. An empty project and an empty filtered result are distinct
states. The command palette shows a few full-text matches and links to the full
filtered list. It searches pages as well as issues, under headings that say
which is which — a hit that does not say what kind of thing it is is a poor
hit.

Sorting by epic groups the list. It groups by sorting rather than by cutting up
the page it happens to hold — the epic is the first sort key on the server
(`docs/api.md`), so a group is one unbroken run and opens exactly once, whatever
page it began on. A head names the epic and its title, the run for the issues
under no epic comes last and says "No epic" rather than trailing off the end,
and within a group the order is what is up next: priority first. The heads are
rows of the same virtual window and are `presentation` inside the listbox, which
takes only options; every row still says which issue it is.

A row marks the two things that are scales rather than words twice over, so
that neither rests on colour alone. The status is a dot whose fill says the
stage — nothing decided is an empty ring, work in flight is half full, what has
come to rest is solid — and whose hue says which state it is. The priority is
four bars of rising height with as many lit as the step is high, pale at `none`
and coloured at `urgent` and nowhere else. Beside each stands its word, read out
where it is not shown. The choices that set them carry the same words, because a
native option holds text and nothing else.

`j` and `k` move the active row, `Enter` opens it, `/` focuses
search and `Escape` closes the topmost filter or preview — the same list `?`
shows. Focus and the active
row remain visible. Returning from a detail screen restores filters and scroll
position.

Anything that has to exist already is chosen, never typed. One control does it
everywhere: chips for what is chosen, a field that filters, a list of what
there is, and the same keys — arrows and Enter choose, Escape closes the list
before it closes anything around it, Backspace on an empty field takes the last
chip back. On a narrow screen the chips wrap and the list opens below the field
rather than over it.

Its fillings are the label set of the project (grouped, each label with the
one-line description it carries; choosing the sibling of a group already
carried replaces it and says so, rather than letting the instance refuse the
save; a name the project does not have offers itself as one to create, with its
group and description left to the labels screen), the epics of the project
(with the title beside the key, and a closed one saying so on its row, since
attaching to it reopens it), the members of the project plus nobody as a row of
its own, the agents of the instance for an author, and a search across issues by
key or title for a parent and for blockers. Labels are chosen on the issue form,
the epic form and the list filter, where several of them become several `label`
values in the address.

The list filter chooses the same things, in the flavour a filter needs: nothing
chosen is *any* rather than *nobody*, *me* is a row of its own, and so is *no
epic* or *nobody* where the filter admits one. The author is the one choice the
filter has that no form does; it offers the project's members and the instance's
agents, revoked ones included, because a token that no longer works wrote issues
that are still there to be found.

A refusal that names a field is shown at that field, not over the form.

Every form offers the way out it asked for: a Cancel button and `Escape`, one
behaviour reached two ways. An untouched form is left at once; a form that was
written in asks whether to discard first, with Discard and Keep writing.
Leaving goes back where the form was opened from — the list a create was
started on, the epic whose key came along in the address — and falls back to
the epic or the issue list where nothing stands behind it. `Escape` belongs to
whatever is nearest the keyboard: an open suggestion list or dialog closes
before the form does, and a Markdown field pulled over the window is one of
those. `⌘/Ctrl+Enter` saves from inside a text field, so what was typed does not
have to be left first.

Creating is never the key alone. The header of every list that can be added to
carries the act as a button — New issue on the issue lists and on Needs you,
New epic on the epics — the epic screen offers one that arrives at the form with
its own key already filled in, and the command palette carries all three:
issue, epic and project.

The detail screen first presents what the issue needs now: an answer field for
an open question; the result, `canceled` and reopen for review; open blockers
when blocked; the holder and age when claimed.

Acting on an issue never means scrolling past it. The header carries the one
action the status calls for — claim, hand in for review, accept, reopen — with
`Edit` beside it and every other verb, deletion included, behind an overflow
menu; it stays in view while the page scrolls. The description is never folded
away: a long one is capped at a readable height behind a fade until somebody
asks for the rest. Below that, conversation, relationships and history share one
tabbed area, each tab carrying its count, with the conversation open by default;
a comment or a question opens its field on a button inside that tab rather than
standing open beside the actions. A comment carries the two acts ADR 0022 gave
it in the same overflow menu the header uses, and only where they are allowed:
its author edits it — in the field it was written in, opened on the text it is
correcting, with "edited" in the byline afterwards — and its author, or any
user on anybody's, deletes it behind a confirmation that says there is no
grace period. Adding and removing a blocker lives in the relationships tab.

Narrow screens get status, priority, `ready` and the epic as a chip line
directly under the title; the metadata column itself starts at the medium
breakpoint and carries the rest.

## Action matrix

| area | read actions | write actions |
|---|---|---|
| Issue | list, search, filter, open, inspect history | create, edit fields and relationships, set or clear `ready`, comment, edit or delete a comment, answer, close, hand in for review, reopen, claim, release the claim, put into or take out of the open release, delete, restore |
| Epic | list, open, inspect progress and filtered issues | create, edit Markdown and labels, close, reopen, delete, restore |
| Release | list, open, preview exact membership, copy as Markdown | edit notes, publish, put an issue into the open release or take it out, rename or take back the newest publication |
| Label | list and inspect use | create, edit name, group and description, rename or dissolve a group, delete, restore |
| Project | switch, see how every project one has access to stands, and inspect settings/members/instructions | create; edit name, switches and the instructions page; delete or restore when administrator |
| Identity | inspect own profile, sessions, tokens and agents; read a device login by its code | change own name, verified email and password; revoke sessions/tokens; create or revoke own tokens and agents; rename an agent and rotate its token; confirm or refuse a device login |
| Administration | inspect all users, project assignments, deleted projects and SMTP status | invite/resend, hand over an invitation or password link, deactivate/reactivate, change administrator role, assign projects, send test email |

Closing an epic with open issues warns but succeeds. Adding an issue to a closed
epic warns that the epic reopens. Publishing a release always shows its name,
notes and exact issue set first. Published notes remain editable; publication
time, publisher and membership remain fixed.

## Permission matrix

Project access belongs to a user and is inherited by all of that user's agents.
User tokens act as their user. A caller without access receives `404` for a
project or its content, including direct keys and search; an authenticated caller
who lacks authority for a visible administrative action receives `403`.

| capability | assigned user | their agent | administrator without assignment |
|---|---:|---:|---:|
| Read and change project content | yes | yes | no |
| Create a project | yes; gains access | no | yes; gains access |
| Change name, triage/review switches and labels | yes | yes, labels only | no |
| View the project's member summary | yes | no | yes |
| Assign or remove project access | no | no | yes |
| Delete or restore a project | no | no | yes |
| Manage own password, sessions and user tokens | yes | no | yes, for self |
| Confirm or refuse a device login | yes | no | yes, for self |
| Manage own agents | yes | no | yes, for self |
| Invite or deactivate users; change administrator role | no | no | yes |
| Hand over an invitation or password link | no | no | yes |
| Inspect SMTP status and send a test email | no | no | yes |

An administrator role grants instance administration, not implicit access to
project content. There must always be at least one active administrator; the
last one can neither be deactivated nor demoted.

Deactivating and demoting ask before they act, and the dialog writes the
consequences out rather than asserting them; reactivating and granting the role
do not. A question in front of every act is no longer a warning, only a second
click. **Neither is offered on the reader's own row.** The server's guard
catches only the last active administrator, so with two of them either can lock
themselves out; that is the second way into the same dead end, and the
interface does not offer it.

### The two lists

Both are the same list twice — users and projects — and they carry the same
head: a search, the one filter that decides what is shown without being asked,
a sort, and the number left when something narrows them. That count is a
`status`, because it changes while somebody types.

**What a list shows unasked is the live half.** The user list leaves the
deactivated out, and the project list does not even fetch the deleted:
`deleted` is a parameter of `GET /admin/projects`, so what is not shown is not
asked for either, rather than fetched and hidden. Both are one choice away —
the state filter, and "Deleted: shown".

Searching is done in the client, because neither list is paginated: projects by
key and name, users by name and email. Projects sort by key, by name or newest
first; users by name or by state. An empty instance and a search that matched
nothing are different sentences, the same distinction the wiki and the issue
list make.

A project's row carries its own lifecycle: **restore** where it is deleted,
**delete** where it is not. Restoring asks nothing — it takes nothing away and
it is the answer to a mistake. Deleting asks, and the question says that
everything in the project goes with it, that the grace period is how long it
can come back, and that the key stays taken until then. Restoring used to be a
floor lower, on the project's own access screen, and deleting was only in the
project's settings.

A deleted row says both dates: **`Created <day> · Deleted <date> · restorable
until at least <day>`**, and the project's own access screen says the second
half above its restore button. Whoever reads a deletion is asking whether it
can still be undone, and a date the reader has to work out from an instance
variable they have never seen is not an answer. The browser does the addition
itself: `GET /me` carries `deletion_grace_days`, so one number answers this row
and answers epics, pages and labels the same way when their screens come to
ask. **"At least"** is not hedging — the purge is opportunistic, so the grace
period is a floor and a project nobody writes to keeps its deleted rows longer
([ADR 0013](adr/0013-deleting-is-a-soft-delete-with-a-floor-and-identities-are-never-deleted.md)).

The user administration also hands over the two links that lead into an account:
an invited user's activation link and an active user's password link. They are
shown once and carried over by the administrator, who therefore never learns
anybody's password — the person sets it themselves, which is the difference the
history could not make visible afterwards. Both are offered whether or not SMTP
is configured, because a control that appears and disappears with an operating
setting explains itself to nobody; without SMTP they are the only way in at all
([ADR 0018](./adr/0018-transactional-email-is-an-optional-instance-capability.md)).

`/device` is the confirmation half of `pa login`
([ADR 0025](./adr/0025-the-console-signs-in-through-a-browser-and-keeps-a-user-token.md)),
and it stands outside the shell like the other three above: it is one decision
about one console, not a screen of the product. Somebody who is not signed in
meets the sign-in screen and is **left here afterwards** rather than sent to the
overview — the one place sign-in remembers where it came from.

The page says what the confirmation grants before the button that grants it: a
key to this instance as the person confirming, with everything they can see and
do, and one that does not expire on its own. Refusing is no harder to reach than
approving, because a refusal somebody has to hunt for is a refusal nobody makes.
An expired, decided or unknown code says which of those it was — except that a
code that never existed and one that ran out are the same sentence, since
telling them apart would tell a guesser which of their guesses was half right.

Opening an address that is not the caller's is a permission state and not a
redirect: `/admin` without the role says that the instance administration
belongs to administrators and offers the way back. The navigation still does not
show the area, and the server still refuses the call with `403`; the screen is
what a link out of a bookmark or a colleague's message runs into.

## Accessibility and performance floor

Every action is reachable by keyboard, focus is visible and restored after a
dialog, controls have accessible names, status and errors are not conveyed by
colour alone, and asynchronous changes are announced. Dialogs trap focus and
return it to their trigger, or, where the action they confirmed removed that
trigger, to what the screen offers next. The phone layout performs the same actions as the
desktop layout.

The shell renders before project data, navigation does not remount it, list rows
are virtualized, and three things arrive after the frame rather than in it: the
Markdown pipeline with the first screen that renders Markdown, the editor
with the first screen that writes it, and the confetti when something is
actually celebrated — which for most readers on most days is never. The editor weighs about three and a half
times what the pipeline does, which is why it is fetched when a field is first
put on a screen and why the field is a quiet placeholder for that moment rather
than a plainer text area that would be swapped out from under somebody who had
started typing. Fenced code is not highlighted; it
carries the language its fence named ([ADR 0017](./adr/0017-the-web-application-is-drawn-by-tailwind-and-base-ui-components-the-repository-owns.md)).
Loading, empty, error and permission states are designed states rather than
blank screens.

Three of those weights are a number and not a description, because a budget
that names none is never exceeded — it is only not kept, and nobody notices:

| budget | limit | what is weighed |
|---|---:|---|
| `first-load` | 780 kB | the entry module, every chunk preloaded beside it and the stylesheet — everything the built `index.html` asks for before the shell renders |
| `markdown` | 200 kB | the Markdown pipeline's own chunk, fetched with the first screen that renders Markdown |
| `editor` | 620 kB | the editor's own chunk, fetched when a field is first put on a screen |
| `confetti` | 20 kB | the celebration's own chunk, fetched the first time a project reaches `clear` |

The last three are also checked for where they are and not only for what they
weigh: all of them are promised to arrive after the frame, so a build that puts
one of them into the first load fails whatever its size. That is the half a number
alone would have missed — the pipeline sat in the first load for a while
because one screen was imported statically instead of lazily, and the paragraph
above went on promising otherwise.

They are measured out of the build, in the bytes it writes, by `npm run budget`
in `src/web`; CI runs it on every push and an excess is red rather than a note,
which holds the trunk ([ADR 0001](./adr/0001-the-repository-is-a-trunk.md)).
That is the only point at which a budget does anything: a note is overlooked by
the third time. The limits are therefore set wide enough to catch a jump rather
than to fence in growth, so that the red means something when it comes.

Fonts are not weighed. They are `@font-face` sources the browser fetches for
the subsets it actually needs, and they hold nothing up. Nor is there a number
for the time to the first frame: without a browser driver it is not
reproducibly measurable, and a number that depends on the machine it was taken
on is worse than no number at all.
