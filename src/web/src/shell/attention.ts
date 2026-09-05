import { createContext } from "react";

/**
 * How much of the current project is waiting for a human, held by the frame so
 * that the navigation can say it without opening the screen (VISION 10, PLAN
 * epic "Attention").
 *
 * It is a number on a link, not a notification: nothing is sent, nothing is
 * addressed at anybody, and there is no read state. The number is read from
 * the "Needs you" list itself and never from a counter of its own, so that the
 * day the list learns an addressee, the number learns it in the same move.
 *
 * It stays current by itself, on the wake channel `pa needs-you --wait` uses
 * (`docs/api.md`, Waiting) — the web application is that mechanism's second
 * user and not a second mechanism. The connection is held here, once per
 * project and tab, so that the sidebar and the screen share one.
 */
export type Attention = {
  /**
   * How many issues only a human can resolve, or `null` while that is not
   * known — before the first answer, and after one that failed. Never `0` for
   * "unknown": a navigation that cannot fail cannot show an error either.
   */
  needsYou: number | null;
  /**
   * Counts the times the instance said this project's work list changed. It
   * carries no state — like the channel it comes from, it only says "ask
   * again" — and a screen that reads the same list watches it to do so.
   */
  pulse: number;
};

export const AttentionContext = createContext<Attention | null>(null);
