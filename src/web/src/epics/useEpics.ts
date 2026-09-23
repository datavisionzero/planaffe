import { useEffect, useState } from "react";
import { api, type Schemas } from "@/api/client";

type EpicSummary = Schemas["EpicSummary"];

const pageSize = 100;

/**
 * The epics of the project, asked once — a project has few enough of them to
 * hold, and the issue form offers them as a choice rather than as a key to
 * type. Few enough is not at most a hundred, so every page is read.
 *
 * What is held belongs to the project it was asked for. A screen that stays
 * mounted while the address moves to another project offers none rather than
 * the last project's epics while the new ones are on their way.
 */
export function useEpics(project: string | undefined): EpicSummary[] {
  const [held, setHeld] = useState<{ of: string; epics: EpicSummary[] }>();

  useEffect(() => {
    if (project === undefined) return;

    let current = true;

    void (async () => {
      const epics: EpicSummary[] = [];

      try {
        let cursor: string | undefined;
        do {
          const { data } = await api.GET("/epics", { params: { query: { project, cursor, limit: pageSize } } });
          if (data === undefined) break;
          epics.push(...data.items);
          cursor = data.next_cursor ?? undefined;
        } while (cursor !== undefined && current);
      } catch {
        // What arrived before the failure is still the project's.
      }

      if (current) setHeld({ of: project, epics });
    })();

    return () => {
      current = false;
    };
  }, [project]);

  return held !== undefined && held.of === project ? held.epics : none;
}

/** One empty list, so that a consumer's memo does not see a new one on every render. */
const none: EpicSummary[] = [];
