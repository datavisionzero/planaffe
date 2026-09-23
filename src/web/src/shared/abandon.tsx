/* eslint-disable react-refresh/only-export-components -- The hook returns the shared confirmation dialog and the context keeps issue drafts under one guard. */
import { createContext, useCallback, useContext, useEffect, useId, useMemo, useRef, useState, type ReactNode, type RefObject } from "react";
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
 *
 * Navigating away is guarded by React Router's blocker, and the router heeds
 * only the blocker registered last. A screen with two forms open at once
 * therefore puts them under one `LeaveScope`, which holds the one blocker for
 * both; a form outside a scope holds its own.
 */
export function useAbandon(changed: boolean, onCancel: () => void, onDiscard?: () => void, escape = true): { leave: () => void; permit: () => void; dialog: ReactNode } {
  const [asking, setAsking] = useState(false);
  const permitted = useRef(false);
  const scope = useContext(LeaveScopeContext);
  const id = useId();

  useEffect(() => { if (!changed) permitted.current = false; }, [changed]);

  useEffect(() => {
    if (scope === null) return;
    scope(id, { changed, permitted, discard: onDiscard });
    return () => scope(id, null);
  }, [changed, id, onDiscard, scope]);

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

  const keep = () => setAsking(false);
  const discard = () => {
    onDiscard?.();
    permitted.current = true;
    setAsking(false);
    onCancel();
  };

  return {
    leave,
    permit: () => { permitted.current = true; },
    dialog: scope === null
      ? <GuardedLeave changed={changed} permittedRef={permitted} asking={asking} onKeep={keep} onDiscard={discard} onDiscardDraft={onDiscard} />
      : <LeaveDialog open={asking} onKeep={keep} onDiscard={discard} />,
  };
}

/** A form's own blocker, where no scope holds one for it. */
function GuardedLeave({ changed, permittedRef, asking, onKeep, onDiscard, onDiscardDraft }: { changed: boolean; permittedRef: RefObject<boolean>; asking: boolean; onKeep: () => void; onDiscard: () => void; onDiscardDraft?: () => void }) {
  const blocker = useBlocker(useCallback(() => changed && !permittedRef.current, [changed, permittedRef]));
  const blocked = blocker.state === "blocked";

  useBeforeUnload(useCallback((event) => {
    if (!changed) return;
    event.preventDefault();
    event.returnValue = "";
  }, [changed]));

  const keep = () => {
    onKeep();
    if (blocker.state === "blocked") blocker.reset();
  };
  const discard = () => {
    if (blocker.state === "blocked") {
      onDiscardDraft?.();
      permittedRef.current = true;
      onKeep();
      blocker.proceed();
    } else {
      onDiscard();
    }
  };

  return <LeaveDialog open={asking || blocked} onKeep={keep} onDiscard={discard} />;
}

function LeaveDialog({ open, onKeep, onDiscard }: { open: boolean; onKeep: () => void; onDiscard: () => void }) {
  return (
    <Dialog open={open} onOpenChange={(next) => { if (!next) onKeep(); }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Discard what you wrote?</DialogTitle>
          <DialogDescription>Your changes have not been saved. You can keep writing or discard them.</DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={onKeep}>Keep writing</Button>
          <Button variant="destructive" onClick={onDiscard}>Discard</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

type Leaving = { changed: boolean; permitted: RefObject<boolean>; discard?: () => void };
type RegisterLeaving = (id: string, leaving: Leaving | null) => void;
const LeaveScopeContext = createContext<RegisterLeaving | null>(null);

/**
 * The one blocker for every form on a screen that can hold several at once —
 * the open release, whose notes and whose publication are both written on it.
 * Leaving is held while any of them was written in and not saved, and
 * discarding throws away what each of them had.
 */
export function LeaveScope({ children }: { children: ReactNode }) {
  const forms = useRef(new Map<string, Leaving>());
  const [changed, setChanged] = useState(false);
  const register = useCallback<RegisterLeaving>((id, leaving) => {
    if (leaving === null) forms.current.delete(id);
    else forms.current.set(id, leaving);
    setChanged([...forms.current.values()].some((form) => form.changed));
  }, []);
  const blocker = useBlocker(useCallback(
    () => [...forms.current.values()].some((form) => form.changed && !form.permitted.current),
    [],
  ));

  useBeforeUnload(useCallback((event) => {
    if (!changed) return;
    event.preventDefault();
    event.returnValue = "";
  }, [changed]));

  const keep = () => { if (blocker.state === "blocked") blocker.reset(); };
  const discard = () => {
    for (const form of forms.current.values()) {
      if (!form.changed) continue;
      form.discard?.();
      form.permitted.current = true;
    }
    if (blocker.state === "blocked") blocker.proceed();
  };

  return <LeaveScopeContext.Provider value={register}>
    {children}
    <LeaveDialog open={blocker.state === "blocked"} onKeep={keep} onDiscard={discard} />
  </LeaveScopeContext.Provider>;
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
