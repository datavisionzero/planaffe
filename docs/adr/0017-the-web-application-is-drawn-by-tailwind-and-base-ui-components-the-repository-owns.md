# The Web Application Is Drawn by Tailwind and Base UI, in Components the Repository Owns

`src/web` is built on Tailwind CSS v4 as the token layer, Base UI as the
primitive layer, and components generated once by the shadcn CLI into the
repository and owned there. The issue list is virtualized with TanStack Virtual,
and Markdown is rendered by `react-markdown` with `remark-gfm` onto those same
components. The comparison behind this is
[`docs/research/frontend-design-foundation.md`](../research/frontend-design-foundation.md);
the shell it draws is the one [ADR 0006](./0006-the-web-application-is-a-shell-before-it-is-a-screen.md)
describes.

The obvious alternative is a component library with a design system attached —
Mantine, in particular, is the fastest route to a working shell: an `AppShell`,
a `Spotlight`, a hundred and forty-five components tested with axe and
VoiceOver, one theme object. It loses on two things this application cares
about. Its identity comes with it, on every screen, and the vision wants an
application that looks like planaffe and feels like Linear rather than like a
library's showcase. And its cost is paid on every cold load on a phone
([ADR 0004](./0004-the-frontend-is-react-not-blazor.md)): the measured probe
puts the headless options at a third to a half of a library's JavaScript, and
Chakra's runtime CSS-in-JS alone costs more than Base UI, TanStack Virtual and
`react-markdown` together.

The other alternative, Tailwind alone with everything written here, loses on
the one need that is hardest to get right and easiest to get wrong: keyboard
behaviour. Focus trapping, roving focus, dismissal and ARIA wiring are exactly
the work a headless kit takes over and tests, and a project of two people
should own its look, not its focus management.

Two of the five needs decided nothing, because every option meets them the same
way: no library ships virtualization, so the list is TanStack Virtual in each
case, and Markdown is a component mapping whatever draws the components. The
choice hinged on the other three — keyboard behaviour, a mobile shell that is
the same application, and no drift as screens are added — and on cost.

**Base UI rather than Radix**, within the same architecture, because it is what
the shadcn CLI generates for by default, so the components track upstream
without translation; because it ships an Autocomplete that documents the
command-palette use and the TanStack Virtual hook-up, which avoids `cmdk` —
unreleased since March 2025 and carrying a Radix Dialog underneath, which would
mean two primitive libraries in one application; and because it releases monthly
under MUI with the people who built Radix. Radix with `cmdk` remains the named
fallback: the same architecture, 22 KB gzip smaller, an older palette.

## Consequences

**Drift is held by tokens, and the tokens are lintable.** The palette, the
type ramp, the spacing and the radii are CSS variables in one file; Tailwind is
restricted to them, dark mode is one class on `<html>`, and "no raw colour, no
arbitrary value" is a rule a linter can hold. A new screen speaks the same
vocabulary or does not build.

**The components are the repository's.** Once generated, a Sidebar or a Dialog
is a file in `src/web`, bent to the seven views of ADR 0006 without fighting a
library's layout model — and fixed here when it breaks, because there is no
`npm update` for a file that has been edited. That is the price of ownership,
paid deliberately.

**The look has to be decided, not inherited.** shadcn's defaults are widely
used and recognisable; planaffe's identity lives in its own token set, or the
application looks like every other one built this way. The proposal of the
shell — neutral palette, IBM Plex, one accent — is the starting point, not the
decision; the token set is the first ticket after this one.

**Swapping the primitive is a per-component migration, not a rewrite.** The
primitive sits behind a repository-owned component, which is the move shadcn
itself made from Radix to Base UI; going the other way is the same work.

**Three things are decided later, on purpose**: the exact token set, the icon
library (both candidates are permissively licensed), and how code in Markdown
is highlighted — `rehype-highlight` weighs more than the whole primitive layer
and is lazy-loaded or replaced, not shipped whole.

**`react-markdown` needs a `urlTransform` of ours.** Its default admits `irc`,
`ircs` and `xmpp` beside `http`, `https` and `mailto`;
[ADR 0007](./0007-markdown-is-rendered-in-the-browser-and-never-as-html.md)
restricts links further than the library does by default, and the restriction
is written down where the pipeline is configured.
