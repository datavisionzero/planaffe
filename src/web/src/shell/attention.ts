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
 */
export type Attention = {
  /**
   * How many issues only a human can resolve, or `null` while that is not
   * known — before the first answer, and after one that failed. Never `0` for
   * "unknown": a navigation that cannot fail cannot show an error either.
   */
  needsYou: number | null;
  /**
   * What the "Needs you" screen just read. That screen asks the same endpoint
   * for the same list, so it hands its total back here instead of a second
   * request asking the same question.
   */
  note: (project: string, needsYou: number) => void;
};

export const AttentionContext = createContext<Attention | null>(null);
