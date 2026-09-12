import { useEffect, useState } from "react";
import { api, describe } from "@/api/client";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Choose } from "@/shared/Choose";
import type { SpacePage, SpacePageSummary } from "./context";
import { descendantsOf, levels } from "./tree";
import { usePageTree, useSpaceList } from "./useSpaces";

/**
 * Moving a page, with everything below it — another parent, in this space or
 * in another one.
 *
 * It is an act of its own and not a field in the form that edits the text
 * (ADR 0016, `docs/api.md`): it rewrites the address of every page below this
 * one and has refusals no other change has. What can be offered is offered —
 * a parent with room for the subtree, in a space the caller can see — and what
 * only the instance can know is shown in the instance's own words when it
 * says no.
 */
export function MoveDialog({ page, onMoved }: { page: SpacePage; onMoved: (page: SpacePage) => void }) {
  const { spaces } = useSpaceList();
  const here = usePageTree();
  const [open, setOpen] = useState(false);
  const [space, setSpace] = useState(page.space);
  const [parent, setParent] = useState("");
  const [elsewhere, setElsewhere] = useState<{ of: string; pages: SpacePageSummary[] }>();
  const [busy, setBusy] = useState(false);
  const [why, setWhy] = useState<string>();

  const own = here.tree.at === "known" ? here.tree.pages : [];
  // How tall the page is with everything under it, counted in levels: a page
  // on its own is one, a page with a child is two.
  const height = own.length === 0 ? 1 : Math.max(...descendantsOf(own, page.path).map((one) => one.depth - page.depth), 0) + 1;

  // The tree of the space that is being moved into. The one the frame holds is
  // this space's, so another space is read while the dialog is open — the one
  // request this act needs, and only when somebody asks for it.
  useEffect(() => {
    if (!open || space === page.space) {
      return;
    }

    let live = true;

    void (async () => {
      const { data } = await api.GET("/spaces/{name}/pages", { params: { path: { name: space } } });

      if (live && data !== undefined) {
        setElsewhere({ of: space, pages: data });
      }
    })();

    return () => {
      live = false;
    };
  }, [open, page.space, space]);

  const target = space === page.space ? own : elsewhere?.of === space ? elsewhere.pages : [];

  const parents = target.filter((one) => {
    // Never under itself or under a page below it, and never where the subtree
    // would pass the third level.
    const below = space === page.space && (one.path === page.path || one.path.startsWith(`${page.path}/`));

    return !below && one.depth + 1 + height <= levels;
  });

  async function move() {
    setBusy(true);
    setWhy(undefined);

    try {
      const { data, error, response } = await api.POST("/spaces/{name}/pages/move", {
        params: { path: { name: page.space } },
        body: { path: page.path, space: space === page.space ? null : space, parent: parent === "" ? null : parent },
      });

      if (data === undefined) {
        setWhy(describe(error, response.status));
        return;
      }

      await here.reload();
      setOpen(false);
      onMoved(data);
    } catch {
      setWhy("The instance did not answer.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (busy) return;
        setWhy(undefined);
        if (next) {
          setSpace(page.space);
          setParent("");
        }
        setOpen(next);
      }}
    >
      <DialogTrigger render={<Button variant="outline">Move page</Button>} />
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Move {page.title}?</DialogTitle>
          <DialogDescription>
            Everything below this page moves with it, and every address underneath changes. Nothing forwards: links
            written to the old addresses stop working.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-3">
          <Choose label="Space" value={space} onChange={(next) => { setSpace(next); setParent(""); }}>
            {(spaces.at === "known" ? spaces.spaces : []).map((one) => (
              <option key={one.name} value={one.name}>{one.title}</option>
            ))}
          </Choose>
          <Choose label="Under" value={parent} onChange={setParent}>
            <option value="">Directly under the space</option>
            {parents.map((one) => (
              <option key={one.path} value={one.path}>{`${"— ".repeat(one.depth)}${one.title}`}</option>
            ))}
          </Choose>
        </div>
        {why !== undefined && <p role="alert" className="text-sm text-destructive">{why}</p>}
        <DialogFooter>
          <DialogClose render={<Button variant="outline" disabled={busy} />}>Cancel</DialogClose>
          <Button disabled={busy} onClick={() => void move()}>{busy ? "Working…" : "Move page"}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
