# Frontend Design Foundation: Component Library, Headless Kit, or Tailwind Alone

**Date:** 2026-09-02
**Context:** ticket PLAN-4, the "visual system" decision that [ADR 0006](../adr/0006-the-web-application-is-a-shell-before-it-is-a-screen.md) deliberately left open. Constrained by [ADR 0004](../adr/0004-the-frontend-is-react-not-blazor.md) (React + Vite + TypeScript, cold load on a phone matters), [ADR 0007](../adr/0007-markdown-is-rendered-in-the-browser-and-never-as-html.md) (`react-markdown` + `remark-gfm`, never raw HTML) and [`VISION.md`](../../VISION.md) section 6.2 (dense, fast, keyboard-operable list; Markdown-centred detail; works on a phone; Linear as the interaction model).

## Questions

1. Which visual/component foundation should `src/web` be built on? Three options: **(1)** a component library with a design system attached — Mantine or Chakra UI; **(2)** a headless kit plus a token set and Tailwind — Radix Primitives / the shadcn/ui approach, or Base UI; **(3)** Tailwind CSS alone with a small set of components written in the repository.
2. Judged against five needs: **(a)** a dense virtualized issue list; **(b)** keyboard navigation and a command palette; **(c)** a Markdown document view rendered from a component tree; **(d)** a mobile layout that is the same application, navigation collapsing to a drawer; **(e)** no visual drift as screens are added — tokens, theming discipline, dark mode.
3. For each option: what it gives and what has to be added, bundle cost, theming/CSS/dark mode, accessibility of the primitives, maintenance status and licence as of today, and how hard it is to swap later.
4. Briefly: how Linear, GitHub Projects and Height lay out the issue list and the issue detail.

## Method

Primary sources only: the projects' documentation sites (`mantine.dev`, `chakra-ui.com`, `radix-ui.com`, `ui.shadcn.com`, `base-ui.com`, `tailwindcss.com`, `tanstack.com`), their GitHub repositories (README, `LICENSE`, `package.json`, releases, source trees read through the GitHub API), the npm registry (`npm view`), and the products' own pages (`linear.app`, `docs.github.com`, `height.app`). No blog posts other than the vendors' own announcements, no comparison articles, no third-party benchmarks. Web search was used only to locate pages.

**Bundle cost** is not documented by any of the libraries except TanStack Virtual, so it was **measured**: each candidate was bundled with esbuild 0.28.2 (minified ESM, `NODE_ENV=production`, React 19.2.8) in a throwaway probe that renders a provider plus a dialog, a menu, a popover and a button — the same three primitives the shell needs — and the gzip size was compared with a bare React baseline. That is a probe, not planaffe; it ranks the options, it does not predict the application's size.

Where a claim could not be backed by a primary source, that is stated. No version numbers, dates or sizes were guessed. **Snapshot: 2026-09-02.**

---

# Part 1: The three building blocks every option has to add

None of the four libraries under comparison ships a virtualized list, and only one ships a command palette. Three of the five needs are therefore met by the same additional packages in every option, and the option decides only how they are styled.

## 1.1 Virtualization: TanStack Virtual

Mantine's component tree has `Table`, `ScrollArea`, `Tree` and `OverflowList` but no virtualized list ([`packages/@mantine/core/src/components`](https://github.com/mantinedev/mantine/tree/master/packages/%40mantine/core/src/components)); Chakra's has `table`, `scroll-area`, `listbox` and `tree-view`, no virtualized list ([`packages/react/src/components`](https://github.com/chakra-ui/chakra-ui/tree/main/packages/react/src/components)); Radix has none ([`packages/react`](https://github.com/radix-ui/primitives/tree/main/packages/react)); Base UI has none and points at a library instead: its Combobox and Autocomplete docs both say to "efficiently handle large datasets using a virtualization library like `@tanstack/react-virtual`" ([Combobox](https://base-ui.com/react/components/combobox), [Autocomplete](https://base-ui.com/react/components/autocomplete)).

**TanStack Virtual** is "a headless UI utility for virtualizing long lists of elements in JS/TS, React, Vue, Svelte, Solid, Lit, and Angular" that "does not ship with or render any markup or styles for you"; the virtualizer "can be oriented on either the vertical (default) or horizontal axes" ([Introduction](https://tanstack.com/virtual/latest/docs/introduction)). The README claims "Lightweight (10–15kb)", "dynamic & measured sizing", "sticky items and window-scrolling utilities", MIT ([README](https://github.com/TanStack/virtual)). `@tanstack/react-virtual` 3.14.10, published 2026-08-18, MIT (npm; [releases](https://github.com/TanStack/virtual/releases)). Measured: **+7.1 KB gzip** over the React baseline (Part 4).

Alternatives checked only for existence: `react-virtuoso` 4.18.12 (2026-08-17, MIT per npm) and `react-window` 2.3.0 (2026-07-20, MIT per npm). Both render their own scroller markup; TanStack is the only one of the three that is headless, which is what a repository-owned list row needs.

## 1.2 Command palette: cmdk, Base UI Autocomplete, Mantine Spotlight, kbar

- **cmdk** — "⌘K is a command menu React component that can also be used as an accessible combobox"; unstyled; `Command.Dialog` "composes Radix UI's Dialog component"; "labeling, aria attributes, and DOM ordering tested with Voice Over and Chrome DevTools"; MIT ([README](https://github.com/pacocoursey/cmdk)). Its `package.json` depends on `@radix-ui/react-dialog`, `-compose-refs`, `-id`, `-primitive` ([cmdk/package.json](https://github.com/pacocoursey/cmdk/blob/main/cmdk/package.json)). **Maintenance:** last release v1.1.1 on 2025-03-14, last push to the repository 2025-10-29, 74 open issues ([releases](https://github.com/pacocoursey/cmdk/releases)). That is eighteen months without a release. Measured: +17.2 KB gzip including the Radix Dialog it carries.
- **Base UI Autocomplete** — "An input that suggests options as you type", free-form text allowed, `filter` prop, arrow-key navigation with optional looping; the docs show a command-palette usage and the virtualization hook-up above ([Autocomplete](https://base-ui.com/react/components/autocomplete)). Base UI's Combobox "does not allow free-form text input" and points at Autocomplete "for search widgets" ([Combobox](https://base-ui.com/react/components/combobox)). No extra package if Base UI is the primitive layer.
- **Mantine Spotlight** — `@mantine/spotlight`, "Command center for your application", `Ctrl + K` / `Cmd + K` by default via `use-hotkeys`, changeable with the `shortcut` prop ([Spotlight](https://mantine.dev/x/spotlight/)). Mantine only.
- **kbar** — v1.0.0 released 2026-08-10 after a beta series that ran from 2023 to 2025-07-31 ([releases](https://github.com/timc1/kbar/releases)); MIT (npm). Not examined further; noted as the one palette with a fresh release.

**Finding:** shadcn/ui's `Command` component imports `cmdk` in **both** its Radix and its Base UI variant (`registry/bases/{radix,base}/ui/command.tsx`, [shadcn-ui/ui](https://github.com/shadcn-ui/ui/tree/main/apps/v4/registry/bases)). A Base UI project that adds shadcn's Command therefore carries a Radix Dialog as well.

## 1.3 Markdown: react-markdown + remark-gfm, mapped onto components

`react-markdown` is "secure by default" because it does not use `dangerouslySetInnerHTML` and "typically escapes HTML (or ignores it, with `skipHtml`)". The `components` option maps every element the Markdown produces onto a React component of the repository's choosing; `allowedElements` / `disallowedElements` restrict the set; remark and rehype plugins extend the pipeline; MIT ([README](https://github.com/remarkjs/react-markdown)). Version 10.1.0, 2025-03-07; `remark-gfm` 4.0.1, 2025-02-10 (npm; [releases](https://github.com/remarkjs/react-markdown/releases), [remark-gfm releases](https://github.com/remarkjs/remark-gfm/releases)).

Two consequences for ADR 0007:

1. **The component mapping is how HTML is avoided** — the pipeline yields a tree of `p`, `a`, `code`, `table`, `input[type=checkbox]` nodes and the repository supplies the React component for each. Which foundation is chosen only decides what classes or components those mapped elements use. This need is therefore **foundation-neutral**.
2. **The default URL policy is wider than ADR 0007 permits.** The default `urlTransform` "allows the protocols `http`, `https`, `irc`, `ircs`, `mailto`, and `xmpp`, and URLs relative to the current protocol" ([README](https://github.com/remarkjs/react-markdown)). The ADR names `http`, `https` and `mailto` only, so a custom `urlTransform` is required — the default does not implement the decision.

Measured: react-markdown + remark-gfm **+47.1 KB gzip**; adding `rehype-highlight` 7.0.2 costs a further **+54 KB gzip** — more than the whole Radix primitive set. Syntax highlighting should be lazy-loaded or registered with a short language list. (`rehype-sanitize` is not needed when raw HTML is never enabled; it is listed here only because it is often reached for.)

---

# Part 2: The three options

## 2.1 Option 1 — component library with a design system

### Mantine

- **What it is:** "145 components (all `@mantine/*` packages)" and "82 hooks"; "built and maintained by Vitaly Rtishchev and more than 500 other contributors" ([About](https://mantine.dev/about/)). Required packages `@mantine/core` and `@mantine/hooks`, the stylesheet `@mantine/core/styles.css`, and `postcss-preset-mantine` ([Getting started](https://mantine.dev/getting-started/)).
- **(a) list:** no virtualization; `Table`, `ScrollArea`, `Kbd`, and `Spoiler` (collapse long text — useful for the collapsed description of VISION 6.2) exist. Add TanStack Virtual.
- **(b) keyboard / palette:** `@mantine/spotlight` is a built-in palette (1.2). Menus, Combobox and Modal carry their own keyboard handling; `FocusTrap` "is used in all Mantine components that require focus trap like Modal, DatePicker, and Popover" ([FocusTrap](https://mantine.dev/core/focus-trap/)).
- **(c) Markdown:** neutral — map `react-markdown` elements onto `Text`, `Anchor`, `Code`, `Table`.
- **(d) mobile:** **built in.** `AppShell` is "a layout component that can be used to create a common Header / Navbar / Footer / Aside layout pattern"; the navbar collapses below a `breakpoint`, `collapsed` takes `{ mobile, desktop }`, and below the breakpoint the navbar takes 100 % width as an overlay ([AppShell](https://mantine.dev/core/app-shell/)). This is exactly ADR 0006's shell.
- **(e) theming / dark mode:** a theme object of "colors, fonts, spacing, border-radius, and other design tokens", `createTheme` overrides "deeply merged with the default theme" ([Theme object](https://mantine.dev/theming/theme-object/)). "CSS modules is the recommended way of applying most of the styles" and "the most performant and flexible way of styling components"; theme values are exposed as CSS variables; no runtime CSS-in-JS ([Styles overview](https://mantine.dev/styles/styles-overview/)). Colour scheme: `defaultColorScheme` is `light`, `dark` or `auto`; MantineProvider "sets a `data-mantine-color-scheme` attribute on the `<html />` element"; `useMantineColorScheme`, `useComputedColorScheme` ([Color schemes](https://mantine.dev/theming/color-schemes/)).
- **Accessibility:** "Mantine components follow WAI-ARIA accessibility guidelines. All components have proper roles, aria-* attributes and semantics"; "all components that have interactive elements are tested with axe (jest-axe)"; "manually tested with screen readers (VoiceOver)" ([Are Mantine components accessible?](https://help.mantine.dev/q/are-mantine-components-accessible)).
- **Bundle (measured):** +60.8 KB gzip JS plus the full `styles.css` at 277 KB raw / **39.0 KB gzip** — about **+100 KB gzip** for the probe. Whether a per-component subset of the stylesheet can be imported was not checked.
- **Maintenance / licence:** 9.6.0 on 2026-08-31; twelve releases between 2026-05-14 and 2026-08-31, i.e. a patch or minor roughly every ten days; 51 open issues; MIT ([releases](https://github.com/mantinedev/mantine/releases), repository licence field).
- **Swap cost: highest.** Every screen imports Mantine components and props (`Button variant=…`, `Stack gap=…`), the theme object is Mantine's schema, and the PostCSS preset is in the build. Leaving means rewriting every component call site.

### Chakra UI

- **What it is:** v3 is "a composition of two projects in the Chakra ecosystem, Ark UI and Zag.js", "combining the headless library, Ark UI with the styling APIs in Panda CSS"; and the team "decided to keep emotion (and runtime css-in-js) to preserve the dynamic styling benefits" ([Announcing v3](https://chakra-ui.com/blog/announcing-v3), 2024-10-22). `package.json` depends on `@ark-ui/react` 5.39.0 and peer-depends on `@emotion/react` ([packages/react/package.json](https://github.com/chakra-ui/chakra-ui/blob/main/packages/react/package.json)). Install: `npm i @chakra-ui/react @emotion/react`; the Provider "composes … ChakraProvider from @chakra-ui/react for the styling system" and "ThemeProvider from next-themes for color mode" ([Vite](https://chakra-ui.com/docs/get-started/frameworks/vite)).
- **(a) list:** no virtualization. Add TanStack Virtual.
- **(b) palette:** none built in; `combobox` and `menu` exist (component tree). Add cmdk or write one on Ark's combobox.
- **(c) Markdown:** neutral.
- **(d) mobile:** `Drawer` "is used to render a content that slides in from the side of the screen" ([Drawer](https://chakra-ui.com/docs/components/drawer)); **no application-shell or sidebar component** was found in the component tree or the docs pages checked. The shell is written in the repository.
- **(e) theming / dark mode:** theming "built around the API of Panda CSS": `defineConfig` → `createSystem` → `ChakraProvider`; raw tokens and semantic tokens, semantic tokens "always return a css variable"; `cva`/`sva` recipes ([Theming overview](https://chakra-ui.com/docs/theming/overview)). "Chakra relies on the `next-themes` library to provide dark mode support"; `_dark` condition; `.dark` / `.light` class forcing ([Dark mode](https://chakra-ui.com/docs/styling/dark-mode)).
- **Accessibility:** delegated — "if the issue is a logic or accessibility bug, then it's most likely a bug in Zag.js" ([Contributing](https://chakra-ui.com/docs/get-started/contributing)).
- **Bundle (measured):** **+105.2 KB gzip JS**, the largest of the probe; no static CSS because styling is resolved at runtime by Emotion.
- **Maintenance / licence:** 3.37.0 on 2026-08-28; minors on 2026-01-10, 02-03, 02-11, 03-03, 04-22, 06-10, 07-19, 08-28 — roughly monthly; 14 open issues; MIT ([releases](https://github.com/chakra-ui/chakra-ui/releases)).
- **Swap cost: highest**, same reasons as Mantine, plus the Emotion runtime and `next-themes` in the tree.

## 2.2 Option 2 — headless primitives + tokens + Tailwind (the shadcn/ui approach)

The approach: primitives supply behaviour and ARIA, Tailwind supplies the token vocabulary, and the components themselves are **files in the repository**, generated once by a CLI. shadcn/ui: "This is not a component library. It is how you build your component library" ([Introduction](https://ui.shadcn.com/docs)). Since July 2026 "new projects now use Base UI by default. Radix is still fully supported … every update and new component will ship for both libraries" ([July 2026 changelog](https://ui.shadcn.com/docs/changelog/2026-07-base-ui-default)); the choice is made at `npx shadcn create` / `init` (`-b radix`), and "the components look and behave the same way. Only the underlying implementation changes" ([January 2026 changelog](https://ui.shadcn.com/docs/changelog/2026-01-base-ui)). The registry tree holds three bases: `aria`, `base`, `radix` ([apps/v4/registry/bases](https://github.com/shadcn-ui/ui/tree/main/apps/v4/registry/bases)). CLI `shadcn` 4.20.1 on 2026-09-02; MIT ([releases](https://github.com/shadcn-ui/ui/releases)). Generated components also pull `class-variance-authority` (0.7.1, **Apache-2.0**), `tailwind-merge` (3.6.0, MIT) and `lucide-react` (1.39.0, **ISC**) — all permissive, but not all MIT (npm).

### Radix Primitives

- **What it is:** "a low-level UI component library with a focus on accessibility, customization and developer experience", "Maintained by @workos", "Licensed under the MIT License, Copyright © 2022-present WorkOS" ([README](https://github.com/radix-ui/primitives)). Components "adhere to the WAI-ARIA design patterns where possible"; the team handles "`aria` and `role` attributes, focus management, and keyboard navigation" ([Introduction](https://www.radix-ui.com/primitives/docs/overview/introduction), [Accessibility](https://www.radix-ui.com/primitives/docs/overview/accessibility)). Dialog "adheres to the Dialog WAI-ARIA design pattern", `Esc` "closes the dialog and moves focus to `Dialog.Trigger`", styling hooks via `[data-state]` ([Dialog](https://www.radix-ui.com/primitives/docs/components/dialog)). Unified package `radix-ui`, subpath imports "can help some bundlers tree-shake more effectively" ([Getting started](https://www.radix-ui.com/primitives/docs/overview/getting-started)).
- **Inventory:** dialog, alert-dialog, dropdown-menu, context-menu, menubar, navigation-menu, popover, hover-card, tooltip, select, tabs, toast, scroll-area, toggle-group, toolbar, roving-focus, focus-scope … — **no combobox, no autocomplete** ([packages/react](https://github.com/radix-ui/primitives/tree/main/packages/react)).
- **Bundle (measured):** Dialog + DropdownMenu + Popover **+33.6 KB gzip** — the smallest primitive set in the probe.
- **Maintenance:** release notes dated 2026-07-20, 2026-07-06 and 2026-06-30 ([Releases](https://www.radix-ui.com/primitives/docs/overview/releases)); `radix-ui` 1.6.7 published 2026-07-31 (npm); 348 open issues; no GitHub Releases entries (the changelog lives on the site).

### Base UI

- **What it is:** "From the creators of Radix, Material UI, and Floating UI" — Colm Tuite, Marija Najdova, Flavien Delangle, James Nelson, Jenna Smith, Michał Dudak, Aarón García; "Our focus is on accessibility, performance, and developer experience" ([About](https://base-ui.com/react/overview/about)); repository `mui/base-ui`, MIT. v1.0.0 "Stable 🎉" on 2025-12-11 with "35 unstyled UI components" and the "New `@base-ui/react` npm package" (the earlier name was `@base-ui-components/react`) ([Releases](https://base-ui.com/react/overview/releases)).
- **Accessibility:** "Base UI components adhere to the WAI-ARIA Authoring Practices to provide basic keyboard accessibility out of the box"; "manage focus automatically following a user interaction"; but "it's the developer's responsibility to visually indicate focus" ([Accessibility](https://base-ui.com/react/overview/accessibility)).
- **Inventory:** autocomplete, combobox, dialog, alert-dialog, drawer, menu, context-menu, menubar, navigation-menu, popover, select, tabs, toast, tooltip, scroll-area, field/form, number-field, otp-field … ([packages/react/src](https://github.com/mui/base-ui/tree/master/packages/react/src)). The palette need (1.2) is covered in-library by Autocomplete.
- **Setup quirks:** the layout root needs `isolation: isolate` so popups stack above content, and `body { position: relative }` is recommended for iOS 26 Safari backdrops ([Quick start](https://base-ui.com/react/overview/quick-start)).
- **Bundle (measured):** Dialog + Menu + Popover **+55.9 KB gzip** — 22 KB more than Radix for the same three.
- **Maintenance:** 1.7.0 on 2026-08-04, 1.6.0 2026-06-18, 1.5.0 2026-05-19, 1.4.1 2026-04-20, 1.4.0 2026-04-13 — a minor roughly every month ([Releases](https://base-ui.com/react/overview/releases), [GitHub releases](https://github.com/mui/base-ui/releases)); 426 open issues; last push 2026-09-02.

### Tailwind CSS (the token layer of Option 2, and all of Option 3)

- "Theme variables are special CSS variables defined using the `@theme` directive that influence which utility classes exist in your project"; Tailwind "also generates regular CSS variables for your theme variables"; a namespace can be replaced (`--color-*: initial;`) or the whole default theme dropped (`--*: initial;`), after which "you'll only be able to use utility classes matching your custom theme variables" ([Theme variables](https://tailwindcss.com/docs/theme)). That last mode is the mechanism for need (e): a design with ten colours has ten colour utilities, not the palette's 242.
- Dark mode: the `dark` variant "uses the `prefers-color-scheme` CSS media feature" by default; `@custom-variant dark (&:where(.dark, .dark *))` or a `[data-theme=dark]` selector switches it to a manual toggle ([Dark mode](https://tailwindcss.com/docs/dark-mode)).
- shadcn on top of it: "We use and recommend CSS variables for theming"; semantic pairs — "the base token controls the surface color and the `-foreground` token controls the text and icon color that sits on that surface"; "Dark mode works by overriding the same tokens inside a `.dark` selector"; oklch values; Tailwind v4 `@theme inline` maps the tokens to `bg-background`, `text-foreground` ([Theming](https://ui.shadcn.com/docs/theming)).
- 4.3.3 on 2026-07-16; MIT, "Copyright (c) Tailwind Labs, Inc." ([releases](https://github.com/tailwindlabs/tailwindcss/releases), [LICENSE](https://github.com/tailwindlabs/tailwindcss/blob/main/LICENSE)). `tailwindcss` + `@tailwindcss/vite` are build-time; nothing of Tailwind itself ships as JS.

### Option 2 against the five needs

- **(a)** TanStack Virtual, rows are repository components with Tailwind classes. Nothing in the way.
- **(b)** Menus, dialogs, popovers, tabs and roving focus from the primitive layer; the palette from cmdk (Radix) or Autocomplete (Base UI); global shortcuts are a hook in the repository either way.
- **(c)** neutral; mapped elements get Tailwind classes directly, so `@tailwindcss/typography` is unnecessary.
- **(d)** shadcn's `Sidebar` block: `collapsible="offcanvas"` "slides in from the left or right", becomes a `Sheet` on mobile via `useIsMobile`, toggled with `cmd+b` / `ctrl+b` ([Sidebar](https://ui.shadcn.com/docs/components/base/sidebar); source imports `use-mobile`, `sheet` — [sidebar.tsx](https://github.com/shadcn-ui/ui/blob/main/apps/v4/registry/bases/base/ui/sidebar.tsx)). Generated into the repository, then owned.
- **(e)** tokens are CSS variables in one file; Tailwind can be restricted to them; dark mode is a class on `<html>`. The discipline is enforceable by lint (no arbitrary values, no raw colours) rather than by a library.
- **Swap cost: medium, and already demonstrated.** The primitive layer sits behind repository-owned components, and shadcn itself moved its default from Radix to Base UI while "keeping the same abstraction" ([July 2026 changelog](https://ui.shadcn.com/docs/changelog/2026-07-base-ui-default)) — a per-component migration, not a rewrite. Swapping Tailwind would be a rewrite of class strings, but the tokens survive as CSS variables.

## 2.3 Option 3 — Tailwind alone, components written in the repository

Everything from the Tailwind paragraph above applies. What is missing is the behaviour layer: dialog focus trapping and focus return, menu roving focus and typeahead, popover positioning and dismissal, `aria-*` wiring. That is precisely the list both headless libraries name as the work they take off the developer — Radix: "`aria` and `role` attributes, focus management, and keyboard navigation" ([Introduction](https://www.radix-ui.com/primitives/docs/overview/introduction)); Base UI: "manage focus automatically following a user interaction" ([Accessibility](https://base-ui.com/react/overview/accessibility)). Native `<dialog>` covers the modal case; there is no native equivalent for a keyboard-navigable menu or a positioned popover, and the primary sources consulted here do not cover browser APIs, so no claim is made about them.

- **(a)** TanStack Virtual, as everywhere. **(b)** the hardest part of the option: menus, palette and drawer are hand-written, and the a11y test surface of Mantine (axe, VoiceOver) or Base UI's team is not inherited. **(c)** neutral. **(d)** a drawer is a dialog; written once. **(e)** identical to Option 2 — this is where the option is strong.
- **Bundle:** the smallest possible; only React plus what the repository writes.
- **Swap cost:** lowest into Option 2 (add a primitive under an existing component), highest out of it for anything already written by hand.

---

# Part 3: Comparison

## 3.1 Needs against options

**Legend:** ● included · ◐ partly, or one add-on · ○ written or added in the repository

| Need | Mantine | Chakra | Radix + shadcn | Base UI + shadcn | Tailwind alone |
|---|---|---|---|---|---|
| (a) virtualized list | ○ TanStack Virtual | ○ TanStack Virtual | ○ TanStack Virtual | ○ TanStack Virtual (documented hook-up) | ○ TanStack Virtual |
| (b) menus, dialogs, focus | ● | ● (Ark/Zag) | ● | ● | ○ hand-written |
| (b) command palette | ● Spotlight | ○ cmdk | ◐ cmdk (stale) | ● Autocomplete | ○ hand-written |
| (c) Markdown → components | ● neutral | ● neutral | ● neutral | ● neutral | ● neutral |
| (d) shell with drawer | ● AppShell | ○ Drawer only | ● Sidebar block (owned) | ● Sidebar block (owned) | ○ hand-written |
| (e) tokens, dark mode | ● theme object, CSS vars | ● Panda tokens, next-themes | ● CSS vars + `@theme` | ● CSS vars + `@theme` | ● `@theme`, `--*: initial` |
| Runtime CSS-in-JS | no | **yes (Emotion)** | no | no | no |
| Components live in | node_modules | node_modules | repository | repository | repository |
| Swap cost | high | high | medium | medium | low in / high out |

## 3.2 Bundle probe (gzip, esbuild 0.28.2, React 19.2.8, 2026-09-02)

| Probe | JS gzip | Δ vs baseline | Static CSS gzip |
|---|---|---|---|
| React + ReactDOM, one `<div>` | 60.2 KB | — | — |
| Radix `Dialog` + `DropdownMenu` + `Popover` | 93.8 KB | **+33.6 KB** | — |
| Base UI `Dialog` + `Menu` + `Popover` | 116.1 KB | **+55.9 KB** | — |
| Mantine `MantineProvider` + `AppShell` + `Button` + `Modal` + `Menu` + `TextInput` | 121.0 KB | **+60.8 KB** | **+39.0 KB** (`styles.css`) |
| Chakra `ChakraProvider` + `Button` + `Dialog` + `Menu` + `Input` | 165.4 KB | **+105.2 KB** | — (runtime) |
| cmdk `Command.Dialog` (carries Radix Dialog) | 77.4 KB | +17.2 KB | — |
| `@tanstack/react-virtual`, one list | 67.2 KB | +7.1 KB | — |
| `react-markdown` + `remark-gfm` | 107.2 KB | +47.1 KB | — |
| … plus `rehype-highlight` | 161.3 KB | +101.1 KB | — |

Reading: the two headless options cost a third to a half of the two libraries for the same three behaviours, and Chakra's runtime styling is the most expensive item measured. Tailwind's own cost is build-time and depends on the classes used; it was not measured. Deltas overlap (cmdk's Radix Dialog is shared with the Radix option) and a real application will add more than the probe — the numbers order the options, nothing more.

## 3.3 Maintenance and licence (snapshot 2026-09-02)

| Package | Version | Released | Cadence | Maintainer | Licence |
|---|---|---|---|---|---|
| `@mantine/core` | 9.6.0 | 2026-08-31 | ~every 10 days | Vitaly Rtishchev + contributors | MIT |
| `@chakra-ui/react` | 3.37.0 | 2026-08-28 | ~monthly | Chakra UI team | MIT |
| `radix-ui` | 1.6.7 | 2026-07-31 (notes 2026-07-20) | irregular, 3 notes Jun–Jul 2026 | WorkOS | MIT |
| `@base-ui/react` | 1.7.0 | 2026-08-04 | ~monthly minors | MUI (ex-Radix/Floating UI) | MIT |
| `tailwindcss` | 4.3.3 | 2026-07-16 | ~monthly | Tailwind Labs, Inc. | MIT |
| `shadcn` (CLI) | 4.20.1 | 2026-09-02 | several per week | shadcn | MIT |
| `@tanstack/react-virtual` | 3.14.10 | 2026-08-18 | frequent patches | TanStack | MIT |
| `cmdk` | 1.1.1 | **2025-03-14** | none since | pacocoursey | MIT |
| `kbar` | 1.0.0 | 2026-08-10 | first stable | timc1 | MIT |
| `react-markdown` | 10.1.0 | 2025-03-07 | stable, 5 open issues | remarkjs | MIT |
| `remark-gfm` | 4.0.1 | 2025-02-10 | stable | remarkjs | MIT |
| `class-variance-authority` | 0.7.1 | 2024-11-26 | — | joe-bell | **Apache-2.0** |
| `lucide-react` | 1.39.0 | 2026-09-01 | weekly | lucide | **ISC** |

Sources: npm registry (`npm view … version time license`) and the GitHub releases pages linked in Part 2.

---

# Part 4: How Linear, GitHub Projects and Height lay it out

Only from the products' own pages; density is a number none of them documents.

**Linear.** List navigation: "↑ / ↓ or J / K to navigate the page to the issue", `X` selects, `Shift` + arrows extends, `Cmd/Ctrl A` selects all after filtering, `Cmd/Ctrl K` opens the command bar for actions on the selection ([Select issues](https://linear.app/docs/select-issues)). Detail without leaving the list: "Tap `Space` to toggle peek on or off", "use ↑ and ↓ to move through adjacent issues or projects while updating the preview", `Esc` closes; the peek shows "the description, assignee, status, priority, cycle, labels, estimate, creation date, and updated date" ([Peek](https://linear.app/docs/peek)). Split view exists for Triage: "view your list of issues side by side with the focused issue" ([changelog, 2022-01-20](https://linear.app/changelog/2022-01-20-linear-preview-new-sidebar-and-team-icons)); triage actions are `1` accept, `2` duplicate, `3` decline, `H` snooze ([Triage](https://linear.app/docs/triage)). List and board layouts, grouping by "status, assignee, project, priority, cycle, label, parent issue, team …", ordering, a toggle to show sub-issues, and per-view choice of displayed properties ([Display options](https://linear.app/docs/display-options)). Mobile: "Linear Mobile is built with native Swift and Kotlin code", iOS and Android, "the portable companion" ([Mobile](https://linear.app/mobile)); whether the web application is meant for phones is **not stated**. A density setting was **not found** in the docs pages checked.

**GitHub Projects.** Three layouts — table, "a powerful and adaptable spreadsheet comprised of your issues, pull requests, and draft issues"; board; roadmap ([Changing the layout](https://docs.github.com/en/issues/planning-and-tracking-with-projects/customizing-views-in-your-project/changing-the-layout-of-a-view)); the overview calls it "a high-density table layout" ([About Projects](https://docs.github.com/en/issues/planning-and-tracking-with-projects/learning-about-projects/about-projects)). Table: show/hide fields, drag column headers to reorder, group (not by title, labels, reviewers or linked PRs), sort with a secondary key, field sums per group ([Customizing the table layout](https://docs.github.com/en/issues/planning-and-tracking-with-projects/customizing-views-in-your-project/customizing-the-table-layout)). Keyboard: arrows move focus between cells, `Enter` "Toggle edit mode for the focused cell", `Shift + Space` "Select item", `Space` opens the selected item, `Cmd/Ctrl + F` "Focus filter field", `Cmd/Ctrl + Shift + \` "Open row actions menu"; issue lists: `c` create, `o` or `Enter` open ([Keyboard shortcuts](https://docs.github.com/en/get-started/accessibility/keyboard-shortcuts)). `Cmd/Ctrl + K` inside a project opens "a project-specific command palette"; the site-wide command palette "is currently in public preview and is subject to change" and is "deactivated by default" ([GitHub Command Palette](https://docs.github.com/en/get-started/accessibility/github-command-palette)). The phrase "side panel" appears in the docs only for a project's own status updates; a documented name for the item-detail panel was **not found** in the five Projects pages checked. Mobile: GitHub Mobile lets you "Read, review, and collaborate on issues and pull requests"; projects are not mentioned ([GitHub Mobile](https://docs.github.com/en/get-started/using-github/github-mobile)). No density or row-height option is documented.

**Height.** `height.app` and `help.height.app` refused the TLS handshake from the research machine on every attempt (curl exit 35, four fetch resets), so **no page could be read**. The search engine's excerpts of Height's own pages say: `Cmd/Ctrl + K` opens "Command" for bulk actions, `Cmd + P` searches, a list can be shown "as either a Kanban board, a calendar, or a spreadsheet", `Enter` twice creates a task and `Tab` makes a subtask ([Height overview](https://help.height.app/en/articles/3606831-height-overview), [Spreadsheets](https://height.app/product/spreadsheet)). **These are unverified** against the page text and should be re-checked before being relied on.

---

# Open Points and Uncertainties

1. **Height** could not be read at all (above). Its inclusion here is a placeholder.
2. **Bundle numbers** are a probe of three primitives, not planaffe. Real sizes depend on which components are actually used and on code splitting; the ordering is the finding, not the digits.
3. **Mantine's stylesheet** was measured as the full `styles.css`; whether per-component CSS imports reduce it was not checked.
4. **cmdk's future** is unknown — eighteen months without a release is a fact, abandonment is an inference. shadcn still ships it in all three bases as of 2026-09-02.
5. **Base UI's** stable line is nine months old (1.0.0 on 2025-12-11). Its API has had seven minor releases since; the changelog was not audited for breaking changes.
6. **Linear's web app on phones** and any **density setting** in Linear or GitHub are not documented in the pages checked; absence in the docs is not absence in the product.
7. **Whether GitHub's item detail is called a "side panel"** is unconfirmed; the layout was not observed, only the docs read.
8. **Tailwind's own runtime CSS size** for planaffe was not measured; it is a function of the classes used.

---

## Recommendation

**Option 2: Tailwind CSS v4 as the token layer, Base UI as the primitive layer, and the components generated once by the shadcn CLI into the repository and owned there — with TanStack Virtual for the list and `react-markdown` + `remark-gfm` mapped onto those components.**

The reasoning, in the order of the needs:

- **The two hardest needs are foundation-neutral, so the foundation should be judged on the other three.** Virtualization (a) is added from the same package in every option, because none of the four libraries ships one; Markdown (c) is a component mapping whatever is chosen. What remains is keyboard behaviour, the mobile shell, and drift.
- **Keyboard behaviour (b) is the argument against Option 3.** Focus trapping, roving focus, dismissal and ARIA wiring are exactly what Radix and Base UI list as the work they take over, and Mantine tests with axe and VoiceOver. A two-person project should not write and test a menu; it should own its look, not its focus management. Base UI's Autocomplete covers the palette without cmdk, which has not shipped since 2025-03-14 — and avoiding cmdk also avoids carrying a second primitive library (its Radix Dialog) under a Base UI application.
- **The shell (d) exists in this option as a block that is owned, not imported.** shadcn's Sidebar collapses to a Sheet on mobile and is toggled with `Cmd + B`; after generation it is a file in `src/web` that can be bent to ADR 0006's seven views without fighting a library's layout model. Mantine's AppShell is the better ready-made shell, but it comes with Mantine's 145-component identity attached.
- **Drift (e) is where Option 2 is strongest.** Tokens are CSS variables in one file, Tailwind can be restricted to them with `--*: initial`, dark mode is one class on `<html>`, and the rule "no raw colour, no arbitrary value" is lintable. A component library gives the same discipline but in its own vocabulary, and every screen speaks it.
- **Cost and cold load (ADR 0004).** The probe puts the headless options at a third to a half of the libraries' JS, with no runtime CSS-in-JS; Chakra's Emotion runtime alone costs more than Base UI plus TanStack Virtual plus react-markdown.
- **Reversibility.** Because the primitive sits behind a repository-owned component, swapping Base UI for Radix (or back) is the per-component migration shadcn itself performed in 2026, not a rewrite. The Radix + cmdk pairing remains the fallback if Base UI's cadence turns into churn: same architecture, 22 KB smaller, older palette.

**Why Base UI rather than Radix within Option 2:** it is shadcn's default since July 2026, so generated components track upstream without translation; it ships Autocomplete (the palette) and documents the TanStack Virtual hook-up; it releases monthly under MUI with the people who built Radix; and Radix's own changelog shows three entries in mid-2026 against 348 open issues. The price is 22 KB gzip in the probe and a stable line that is nine months old.

**What it gives up:**

- **Mantine's completeness.** AppShell, Spotlight, Spoiler, Tree, 145 components and 82 hooks, tested with axe and VoiceOver, one theme object, one stylesheet — the fastest route to a working shell by some distance. Option 2 will re-derive a dozen of these in the repository over time, each one a small piece of work and a small piece of maintenance.
- **A visual identity out of the box.** shadcn's defaults are widely used and recognisable; planaffe's look has to be decided and held by its own tokens, or it will look like every other shadcn application.
- **Ownership.** Generated components are the repository's to fix; there is no upstream `npm update` for a bug in the Sidebar once it has been edited.
- **Youth.** Base UI 1.x is nine months old; Mantine and Radix have longer stable histories.
- **The 22 KB** the Radix pairing would have saved, and the option of using cmdk, the most-copied palette, without carrying two primitive libraries.

Not decided here, and deliberately: the exact token set, the icon library (`lucide-react` is ISC, `@tabler/icons-react` MIT — both permissive), and whether syntax highlighting is `rehype-highlight` lazy-loaded or a smaller alternative. Those are the first tickets after the ADR, not part of it.
