import { useCallback, useContext, useEffect, useState } from "react";
import { api } from "@/api/client";
import { AttentionContext, type Attention } from "./attention";

/**
 * The frame's one read of "Needs you", for the number in the navigation. It
 * lives once per project, above both the sidebar and the screen, because the
 * two read the same list — and because this is the place a held connection
 * will hang on when the number learns to stay current by itself.
 *
 * `limit=1` because only `total` is wanted; the items are the screen's. The
 * read is repeated when the project changes and when the frame moves to
 * another view, which is as current as a number without a wake channel gets.
 */
export function useAttentionState(project: string | undefined, view: string): Attention {
  const [known, setKnown] = useState<{ of: string; needsYou: number } | null>(null);

  const note = useCallback((of: string, needsYou: number) => {
    setKnown((was) => (was !== null && was.of === of && was.needsYou === needsYou ? was : { of, needsYou }));
  }, []);

  // Not while "Needs you" is open: that screen reads the same list, with the
  // items, and reports its total through `note`. Two requests for one question
  // would be the second counting path this number is not allowed to have.
  const asking = project !== undefined && view !== "needs-you";

  useEffect(() => {
    if (!asking || project === undefined) {
      return;
    }

    let current = true;

    void (async () => {
      try {
        const { data } = await api.GET("/projects/{key}/needs-you", {
          params: { path: { key: project }, query: { limit: 1 } },
        });

        if (current) {
          setKnown(data === undefined ? null : { of: project, needsYou: data.total });
        }
      } catch {
        // No number rather than a wrong one. The navigation is the frame and
        // may not carry an error banner.
        if (current) {
          setKnown(null);
        }
      }
    })();

    return () => {
      current = false;
    };
  }, [asking, project, view]);

  return { needsYou: known !== null && known.of === project ? known.needsYou : null, note };
}

/** The same number, for the sidebar and for the screen that feeds it. */
export function useAttention(): Attention {
  const attention = useContext(AttentionContext);

  if (attention === null) {
    throw new Error("useAttention is only for the frame and the screens under it.");
  }

  return attention;
}
