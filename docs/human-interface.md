# The Human Interface

This document fixes the screens, the individual actions available in the web
application, and the coarse permission boundary for cut three. It complements
the product intent in [`VISION.md`](../VISION.md); the HTTP operations remain
defined in [`api.md`](./api.md).

Bulk issue changes, export and all three waiting operations remain CLI work.
There is no board, dashboard or rich-text editor. Every Markdown editor is a
text area with a preview, and every ordinary human action on one issue is
available in the browser. Enter in that text area is a line break where it is
read, so nothing has to be known about Markdown to write one
([ADR 0020](./adr/0020-a-newline-is-a-line-break-and-stored-text-is-not-hard-wrapped.md)).

## Screen matrix

| route | screen | primary content | narrow-screen behaviour |
|---|---|---|---|
| `/login` | Sign in | email, password, recovery link | centred single column |
| `/activate` | Accept invitation or bootstrap | name/email context and password setup | centred single column |
| `/recover` | Recover password | request form or new-password form | centred single column |
| `/projects/new` | Create project | immutable key, name and two project switches | centred single column |
| `/:project/ready` | Ready | shared issue list with workable defaults | two-line rows, no horizontal scroll |
| `/:project/in-progress` | In progress | shared issue list filtered to active claims | two-line rows, no horizontal scroll |
| `/:project/needs-you` | Needs you | question, review, unready and stuck reasons | reason and next action stay visible |
| `/:project/issues` | All issues | shared issue list and all URL filters | filters open as a dismissible sheet |
| `/:project/issues/new` | Create issue | title, Markdown description, priority, status, `ready`, and the five choices — labels, epic, parent, assignee, blockers | one column throughout; the pairs of fields stack, chips wrap, each suggestion list opens below its field |
| `/:project/issues/:number` | Issue | sticky action bar, what needs attention, description and result, then tabs | one column; metadata as chips under the title |
| `/:project/epics` | Epics | open epics, progress and recent activity | stacked summaries |
| `/:project/epics/new` | Create epic | title, Markdown description and the label choice | one column; the same form the epic screen edits in place |
| `/:project/epics/:number` | Epic | Markdown description, progress and issue list | one column |
| `/:project/releases` | Releases | `unreleased`, then published releases newest first | stacked summaries |
| `/:project/releases/:name` | Release | notes, exact issue membership and publish/copy actions; on the open release each row can be taken out, on the newest publication rename and take back | one column |
| `/:project/labels` | Labels | the project's set, grouped where its labels exclude one another, with the create line above it | the row stacks: the name and what it means above the two acts |
| `/settings/profile` | Personal settings · Profile | name and email address | area list folds above the area |
| `/settings/security` | Personal settings · Security | password and browser sessions | area list folds above the area |
| `/settings/tokens` | Personal settings · User tokens | the tokens, and the secret of a new one, once | area list folds above the area |
| `/settings/agents` | Personal settings · Agents | the agents and their tokens | area list folds above the area |
| `/:project/settings/general` | Project settings · General | name, the two switches and project deletion | area list folds above the area |
| `/:project/settings/members` | Project settings · Members | who has access to the project | area list folds above the area |
| `/admin/users` | Administration · Users | invite, role and lifecycle, each row's acts in its menu | area list folds above the area |
| `/admin/projects` | Administration · Projects | every project of the instance, deleted ones included | area list folds above the area |
| `/admin/projects/:key` | Administration · One project | who has access to it, and granting or removing it | area list folds above the area |
| `/admin/email` | Administration · Transactional email | the SMTP status and a test message | area list folds above the area |

`:number` is the part of a key after its project prefix ([`CONTEXT.md`](../CONTEXT.md)):
`PLAN-42` is at `/PLAN/issues/42`, `PLAN-E3` at `/PLAN/epics/E3`. A link to an
issue or an epic takes its project from the key it names and not from the
address it sits on, so a blocker in another project leads to that project.

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

"Needs you" in that navigation carries a count of what is waiting for a human,
so that a reader standing on another view knows where to look. It is a number on
a link and not a notification: nothing is sent, nothing is addressed at anybody,
and there is no read state. At zero there is no badge, because a counter showing
zero is not a signal; past ninety-nine it says `99+`; and where the instance did
not answer there is no badge either — the navigation is the frame and carries no
error. The count is read from the "Needs you" list itself rather than from a
counter of its own, and it belongs to the name of the link, so a screen reader
says "Needs you, 3" instead of reading two fragments in a row.

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

## Issue list and detail

Ready, In progress and All issues are presets over one cursor-paginated,
virtualized component. Search, status, priority, label, epic, assignee, claim,
author, blocked, `ready`, sort and order live in the URL. The server supplies
filter choices. An empty project and an empty filtered result are distinct
states. The command palette shows a few full-text matches and links to the full
filtered list.

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
before the form does.

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
standing open beside the actions. Adding and removing a blocker lives in the
relationships tab.

Narrow screens get status, priority, `ready` and the epic as a chip line
directly under the title; the metadata column itself starts at the medium
breakpoint and carries the rest.

## Action matrix

| area | read actions | write actions |
|---|---|---|
| Issue | list, search, filter, open, inspect history | create, edit fields and relationships, set or clear `ready`, comment, answer, close, hand in for review, reopen, claim, release the claim, put into or take out of the open release, delete, restore |
| Epic | list, open, inspect progress and filtered issues | create, edit Markdown and labels, close, reopen, delete, restore |
| Release | list, open, preview exact membership, copy as Markdown | edit notes, publish, put an issue into the open release or take it out, rename or take back the newest publication |
| Label | list and inspect use | create, edit name, group and description, rename or dissolve a group, delete, restore |
| Project | switch and inspect settings/members | create; edit name and switches; delete or restore when administrator |
| Identity | inspect own profile, sessions, tokens and agents | change own name, verified email and password; revoke sessions/tokens; create or revoke own tokens and agents |
| Administration | inspect all users, project assignments, deleted projects and SMTP status | invite/resend, deactivate/reactivate, change administrator role, assign projects, send test email |

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
| Manage own agents | yes | no | yes, for self |
| Invite or deactivate users; change administrator role | no | no | yes |
| Inspect SMTP status and send a test email | no | no | yes |

An administrator role grants instance administration, not implicit access to
project content. There must always be at least one active administrator; the
last one can neither be deactivated nor demoted.

## Accessibility and performance floor

Every action is reachable by keyboard, focus is visible and restored after a
dialog, controls have accessible names, status and errors are not conveyed by
colour alone, and asynchronous changes are announced. Dialogs trap focus and
return it to their trigger, or, where the action they confirmed removed that
trigger, to what the screen offers next. The phone layout performs the same actions as the
desktop layout.

The shell renders before project data, navigation does not remount it, list rows
are virtualized, and the Markdown pipeline arrives with the first screen that
renders Markdown rather than with the frame. Fenced code is not highlighted; it
carries the language its fence named ([ADR 0017](./adr/0017-the-web-application-is-drawn-by-tailwind-and-base-ui-components-the-repository-owns.md)).
Loading, empty, error and permission states are designed states rather than
blank screens.
