# The Human Interface

This document fixes the screens, the individual actions available in the web
application, and the coarse permission boundary for cut three. It complements
the product intent in [`VISION.md`](../VISION.md); the HTTP operations remain
defined in [`api.md`](./api.md).

Bulk issue changes, export and all three waiting operations remain CLI work.
There is no board, dashboard or rich-text editor. Every Markdown editor is a
text area with a preview, and every ordinary human action on one issue is
available in the browser.

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
| `/:project/issues/:number` | Issue | sticky action bar, what needs attention, description and result, then tabs | one column; metadata as chips under the title |
| `/:project/epics` | Epics | open epics, progress and recent activity | stacked summaries |
| `/:project/epics/:number` | Epic | Markdown description, progress and issue list | one column |
| `/:project/releases` | Releases | `unreleased`, then published releases newest first | stacked summaries |
| `/:project/releases/:name` | Release | notes, exact issue membership and publish/copy actions | one column |
| `/settings` | Personal settings | profile, email, password, sessions, user tokens and agents | section navigation becomes a menu |
| `/:project/settings` | Project settings | name, switches, labels, members and project lifecycle | one column |
| `/admin` | Instance administration | users, roles, project access, deleted projects and SMTP status | tabular data becomes labelled rows |

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

The overview is a dialog rather than a screen, so that a key can be looked up
without leaving the list it is about. It is reached three ways — `?`, the
command palette, and an entry in the account menu — because a list of shortcuts
reachable only by a shortcut does not help the reader who has not found one yet.
It draws `⌘` on a Mac and `Ctrl` elsewhere, and it is generated from the one
list the handlers themselves read, so a key that changes changes there.
The Labels navigation entry required by ADR 0006 opens the labels section of
project settings; labels do not need a second management screen.

## Issue list and detail

Ready, In progress and All issues are presets over one cursor-paginated,
virtualized component. Search, status, priority, label, epic, assignee, claim,
author, blocked, `ready`, sort and order live in the URL. The server supplies
filter choices. An empty project and an empty filtered result are distinct
states. The command palette shows a few full-text matches and links to the full
filtered list.

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
its own, and a search across issues by key or title for a parent and for
blockers. Labels are chosen on the issue form, the epic form and the list
filter, where several of them become several `label` values in the address.

A refusal that names a field is shown at that field, not over the form.

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
| Issue | list, search, filter, open, inspect history | create, edit fields and relationships, set or clear `ready`, comment, answer, close, hand in for review, reopen, claim, release, delete, restore |
| Epic | list, open, inspect progress and filtered issues | create, edit Markdown and labels, close, reopen, delete, restore |
| Release | list, open, preview exact membership, copy as Markdown | edit notes, publish |
| Label | list and inspect use | create, edit, delete, restore |
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
