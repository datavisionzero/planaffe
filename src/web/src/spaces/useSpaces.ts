import { useCallback, useContext, useEffect, useRef, useState } from "react";
import { api, describe } from "@/api/client";
import { SpacesContext, TreeContext, type PageTree, type SpaceList, type Spaces, type Tree } from "./context";

/**
 * The spaces the signed-in identity can see, asked once per load and handed to
 * the switcher, the navigation and the list. It is part of the frame for the
 * reason the projects are: the knowledge base is a second bracket and a reader
 * moves between spaces the way they move between projects (VISION 18).
 */
export function useSpaceState(): SpaceList {
  const [spaces, setSpaces] = useState<Spaces>({ at: "asking" });
  const live = useRef(true);

  const reload = useCallback(async () => {
    try {
      const { data } = await api.GET("/spaces");

      if (live.current) {
        setSpaces(data === undefined ? { at: "failed" } : { at: "known", spaces: data });
      }
    } catch {
      if (live.current) {
        setSpaces({ at: "failed" });
      }
    }
  }, []);

  useEffect(() => {
    live.current = true;
    void (async () => {
      await reload();
    })();

    return () => {
      live.current = false;
    };
  }, [reload]);

  return { spaces, reload };
}

/** The same list, for a screen under the shell. */
export function useSpaceList(): SpaceList {
  const list = useContext(SpacesContext);

  if (list === null) {
    throw new Error("useSpaceList is only for screens under the shell.");
  }

  return list;
}

/**
 * The page tree of the space the frame is standing in — the whole tree without
 * the bodies, which is what `GET /spaces/{name}/pages` is for (ADR 0012). One
 * read for the frame and the screens together: the navigation draws it, the
 * page above it takes the titles of its ancestors out of it, and nothing asks
 * a second time.
 */
export function useTreeState(space: string | undefined): PageTree {
  const [state, setState] = useState<{ of: string; tree: Tree }>();
  const live = useRef(true);

  const reload = useCallback(async () => {
    if (space === undefined) {
      return;
    }

    try {
      const { data, error, response } = await api.GET("/spaces/{name}/pages", { params: { path: { name: space } } });

      if (live.current) {
        setState({
          of: space,
          tree: data === undefined ? { at: "failed", why: describe(error, response.status) } : { at: "known", pages: data },
        });
      }
    } catch {
      if (live.current) {
        setState({ of: space, tree: { at: "failed", why: "The instance did not answer." } });
      }
    }
  }, [space]);

  useEffect(() => {
    live.current = true;
    void (async () => {
      await reload();
    })();

    return () => {
      live.current = false;
    };
  }, [reload]);

  // The tree of the space in the address and never the one still in hand from
  // the space before it: walking from one space to another would otherwise
  // draw the old navigation under the new name for as long as the read takes.
  const tree: Tree = state !== undefined && state.of === space ? state.tree : { at: "asking" };

  return { space, tree, reload };
}

/** The open space's tree, for a screen under the shell. */
export function usePageTree(): PageTree {
  const tree = useContext(TreeContext);

  if (tree === null) {
    throw new Error("usePageTree is only for screens under the shell.");
  }

  return tree;
}
