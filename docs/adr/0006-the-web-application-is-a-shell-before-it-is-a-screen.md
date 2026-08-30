# The Web Application Is a Shell Before It Is a Screen

The first thing built in `src/web` is the application shell — a persistent
navigation frame with routes behind it — not the issue list with navigation added
later. Concretely, and from the first commit that renders anything:

- A **left navigation** listing the views of the current project: Ready for
  agents, In progress, Needs you, All issues, Epics, Releases, Labels.
- A **project switcher** in the header, because multiple projects are the normal
  case (VISION 6.2) and never a settings page.
- An **account menu at the top right** — the signed-in identity, settings, agent
  tokens, sign out — which is where every reader of a professional web
  application has looked for it for fifteen years.
- **One route per view**, deep-linkable, with the URL carrying the filter. A
  ticket link that survives being pasted into a chat is worth more here than
  anywhere, because pasting it into a chat is how the human hands work to an
  agent.
- **A layout that collapses to one column on a phone**: the navigation becomes a
  drawer, the list stays readable, and triaging a ticket on a phone needs no
  zooming (VISION 5, 6.2).

The alternative is not a considered design; it is what happens by default —
one screen that grows filters and panels until navigation has to be retrofitted
around a component tree that assumed it was alone. The cost of retrofitting is
paid in exactly the places that hurt: routing, state ownership, and every
existing link. The vision already names seven distinct views for humans plus
epics, releases, labels, projects and tokens, so this application is a
multi-screen application on the day it starts. Building the frame first is
cheaper than proving that again.

## Consequences

**Every screen is a route with a name**, and "where does this live?" has an
answer before the screen is written.

**The visual system is a separate decision** and is deliberately not made here.
What the shell is has to be settled early; which foundation draws it — a
component library, a headless kit plus a token set, or plain CSS — is a choice
with a proposal and a comparison behind it, and it is tracked as its own piece
of work. This ADR constrains that choice only by what it demands: dense list
rendering, keyboard navigation, a command palette, and a mobile layout that is
the same application rather than a reduced one.

**Speed is a constraint of the shell, not a later optimisation.** The frame
renders before data arrives, navigation between views does not remount it, and
the list is virtualized. A UI that is opened to answer one question loses its
argument if it spends a second deciding to show anything (VISION 4, principle 7).
