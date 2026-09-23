/**
 * The browser's storage, where it can be had.
 *
 * `localStorage` and `sessionStorage` are conveniences here — a remembered
 * scroll offset, a theme, an unsent draft — and never something a screen may
 * depend on. A private window, blocked site data or a sandboxed frame makes
 * the accessor itself throw, and a throw in a render takes the screen with it.
 * So every read answers `null` where storage is not there, and every write and
 * removal is silently skipped.
 */
export type Area = "local" | "session";

function storage(area: Area): Storage {
  return area === "local" ? window.localStorage : window.sessionStorage;
}

export function recall(area: Area, key: string): string | null {
  try {
    return storage(area).getItem(key);
  } catch {
    return null;
  }
}

export function remember(area: Area, key: string, value: string) {
  try {
    storage(area).setItem(key, value);
  } catch {
    // Nothing to remember it in; the screen works without it.
  }
}

export function forget(area: Area, key: string) {
  try {
    storage(area).removeItem(key);
  } catch {
    // Nothing was kept, so nothing has to go.
  }
}

/**
 * Where a form's unsent text is kept (`useDraft`): one key per signed-in user,
 * object and form, all under this prefix.
 */
export const draftPrefix = "planaffe.draft:";

/**
 * Throw away every unsent draft in this browser. Signing out is leaving the
 * machine to whoever comes next, and ticket text — which quotes whatever an
 * agent pasted into it — is not left behind in storage for them.
 */
export function forgetDrafts() {
  try {
    const held = window.localStorage;
    const drafts = Array.from({ length: held.length }, (_, index) => held.key(index)).filter((key) => key?.startsWith(draftPrefix));
    for (const key of drafts) held.removeItem(key!);
  } catch {
    // Nothing could be kept where storage is refused, so nothing is left.
  }
}
