import { useCallback, useEffect, useRef, useState } from "react";
import { api, describe } from "@/api/client";
import type { ProjectStanding } from "./standing";

/** How often the overview asks again while somebody is looking at it. */
const interval = 30_000;

export type Overview =
  | { at: "asking" }
  | { at: "failed"; why: string }
  /** `read` is when this answer landed: what the ages on the tiles are measured against. */
  | { at: "known"; projects: ProjectStanding[]; agents: number; read: number };

/**
 * The overview's one read, repeated while the tab is looked at.
 *
 * It holds no connection open, and that is a decision rather than an omission
 * (`docs/human-interface.md`, Overview): the wake channel belongs to one
 * project, so ten projects would be ten held connections — for a screen that
 * is glanced at rather than sat on, and by a browser that has six per origin
 * under HTTP/1.1. So it asks with the validator it last got, is answered `304`
 * by anything that has not changed, and asks again on an interval and whenever
 * the tab comes back.
 *
 * A read that fails leaves what is on the screen where it is. An instance that
 * is restarting is not a reason to empty the overview.
 */
export function useOverview(): { overview: Overview; reload: () => void } {
  const [overview, setOverview] = useState<Overview>({ at: "asking" });
  const validator = useRef<string>(undefined);
  const live = useRef(true);

  const read = useCallback(async () => {
    try {
      const { data, error, response } = await api.GET("/standing", {
        headers: validator.current === undefined ? undefined : { "If-None-Match": validator.current },
      });

      if (!live.current || response.status === 304) {
        return;
      }

      if (data === undefined) {
        // Only while there is nothing on the screen yet: a failure after that
        // is the screen going quiet for a moment, not going blank.
        setOverview((was) => (was.at === "known" ? was : { at: "failed", why: describe(error, response.status) }));
        return;
      }

      validator.current = response.headers.get("ETag") ?? undefined;
      setOverview({ at: "known", projects: data.projects, agents: data.agents, read: Date.now() });
    } catch {
      if (live.current) {
        setOverview((was) => (was.at === "known" ? was : { at: "failed", why: "The instance did not answer." }));
      }
    }
  }, []);

  useEffect(() => {
    live.current = true;
    void read();

    const timer = setInterval(() => {
      if (!document.hidden) {
        void read();
      }
    }, interval);

    // Coming back to the tab reads at once, so that what is on it is right
    // before the reader has finished looking at it.
    const shown = () => {
      if (!document.hidden) {
        void read();
      }
    };

    document.addEventListener("visibilitychange", shown);

    return () => {
      live.current = false;
      clearInterval(timer);
      document.removeEventListener("visibilitychange", shown);
    };
  }, [read]);

  return {
    overview,
    reload: () => {
      validator.current = undefined;
      setOverview({ at: "asking" });
      void read();
    },
  };
}
