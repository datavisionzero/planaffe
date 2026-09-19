/* eslint-disable react-refresh/only-export-components -- The hook returns the shared confirmation dialog and the context keeps issue drafts under one guard. */
import { createContext, useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { is, overlaid } from "@/shell/shortcuts";
import { useBlocker, useBeforeUnload } from "react-router";

/**
 * Leaving a form, both ways at once.
 *
 * A form that only offers Save leaves whoever changed their mind to find the
 * way out in the navigation. So every form has a Cancel button and answers
 * Escape, and the two are the same act rather than two behaviours that drift:
 * an untouched form is left at once, a form that was written in asks before
 * throwing the writing away.
 *
 * Escape is also what a picker's list and an open dialog answer to. Those are
 * nearer to the keyboard than the form is: a list stops the event itself, and
 * anything overlaid is skipped here.
 */
export function useAbandon(changed: boolean, onCancel: () => void, onDiscard?: () => void, escape = true): { leave: () => void; permit: () => void; dialog: ReactNode } {
  const [asking, setAsking] = useState(false);
  const permitted = useRef(false);
  const blocker = useBlocker(useCallback(() => changed && !permitted.current, [changed]));
  const blocked = blocker.state === "blocked";

  useEffect(() => { if (!changed) permitted.current = false; }, [changed]);

  useBeforeUnload(useCallback((event) => {
    if (!changed) return;
    event.preventDefault();
    event.returnValue = "";
  }, [changed]));

  const leave = useCallback(() => {
    if (changed) {
      setAsking(true);
    } else {
      onCancel();
    }
  }, [changed, onCancel]);

  useEffect(() => {
    if (!escape) return;
    function onKeyDown(event: KeyboardEvent) {
      if (!is("form:cancel", event) || overlaid(event)) return;
      event.preventDefault();
      leave();
    }

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [escape, leave]);

  const keep = () => {
    setAsking(false);
    if (blocker.state === "blocked") blocker.reset();
  };
  const discard = () => {
    onDiscard?.();
    permitted.current = true;
    setAsking(false);
    if (blocker.state === "blocked") blocker.proceed();
    else onCancel();
  };

  return {
    leave,
    permit: () => { permitted.current = true; },
    dialog: (
      <Dialog open={asking || blocked} onOpenChange={(open) => { if (!open) keep(); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Discard what you wrote?</DialogTitle>
            <DialogDescription>Your changes have not been saved. You can keep writing or discard them.</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={keep}>Keep writing</Button>
            <Button variant="destructive" onClick={discard}>Discard</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    ),
  };
}

type RegisterDraft = (key: string, changed: boolean, clear: () => void) => () => void;
export const DraftGuardContext = createContext<RegisterDraft | null>(null);

/** One navigation decision covers all text fields open on an issue. */
export function DraftGuard({ children, external, onDirtyChange, onUseLatest }: { children: ReactNode; external?: boolean; onDirtyChange?: (changed: boolean) => void; onUseLatest?: () => void }) {
  const drafts = useRef(new Map<string, () => void>());
  const [changed, setChanged] = useState(false);
  const register = useCallback<RegisterDraft>((key, dirty, clear) => {
    if (dirty) drafts.current.set(key, clear);
    else drafts.current.delete(key);
    setChanged(drafts.current.size > 0);
    return () => {
      if (drafts.current.get(key) === clear) drafts.current.delete(key);
      setChanged(drafts.current.size > 0);
    };
  }, []);
  const discard = useCallback(() => {
    for (const clear of drafts.current.values()) clear();
    drafts.current.clear();
    setChanged(false);
  }, []);
  const cancel = useCallback(() => undefined, []);
  const { dialog } = useAbandon(changed, cancel, discard, false);
  const value = useMemo(() => register, [register]);
  useEffect(() => onDirtyChange?.(changed), [changed, onDirtyChange]);
  return <DraftGuardContext.Provider value={value}>
    {external && <div role="status" className="flex flex-wrap items-center justify-between gap-2 border-b border-amber-500/40 bg-amber-500/5 px-4 py-2 text-sm">
      <span>This issue changed elsewhere. Your text is kept.</span>
      <Button size="sm" variant="outline" onClick={() => { discard(); onUseLatest?.(); }}>Discard drafts and load latest</Button>
    </div>}
    {children}{dialog}
  </DraftGuardContext.Provider>;
}
