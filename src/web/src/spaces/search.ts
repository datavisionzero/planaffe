import { useEffect, useRef, useState } from "react";
import { api, describe, type Schemas } from "@/api/client";

export type SpacePageHit = Schemas["SpacePageHit"];

export type Hits =
  | { at: "asking" }
  | { at: "failed"; why: string }
  | { at: "known"; hits: SpacePageHit[] };

/** Enough of a word to ask the instance about, as in the palette. */
export const shortest = 2;

/**
 * The one search across the knowledge base: `GET /pages`, over every space the
 * caller may see, or one of them by name. The question is in the address, so
 * this hook takes it rather than keeping one of its own.
 */
export function useKnowledgeSearch(query: string, space: string | undefined): Hits {
  const [state, setState] = useState<{ of: string; where: string; hits: Hits }>();
  const live = useRef(true);
  const asked = query.trim();
  const where = space ?? "";

  useEffect(() => {
    live.current = true;
    return () => {
      live.current = false;
    };
  }, []);

  useEffect(() => {
    if (asked.length < shortest) {
      return;
    }

    const controller = new AbortController();

    void (async () => {
      try {
        const { data, error, response } = await api.GET("/pages", {
          params: { query: space === undefined ? { q: asked } : { q: asked, space } },
          signal: controller.signal,
        });

        if (live.current && !controller.signal.aborted) {
          setState({
            of: asked,
            where,
            hits: data === undefined ? { at: "failed", why: describe(error, response.status) } : { at: "known", hits: data },
          });
        }
      } catch {
        if (live.current && !controller.signal.aborted) {
          setState({ of: asked, where, hits: { at: "failed", why: "The instance did not answer." } });
        }
      }
    })();

    return () => controller.abort();
  }, [asked, space, where]);

  // The answer to the question that is being asked, never the one before it:
  // a list that lags behind the field says something that is not true any more.
  return state !== undefined && state.of === asked && state.where === where ? state.hits : { at: "asking" };
}
