import { useCallback, useContext, useEffect, useRef, useState, type ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { useSession } from "@/session/useSession";
import { DraftGuardContext } from "./abandon";
import { draftPrefix } from "./storage";

type Saved<T> = { value: T; baseVersion: string | null; savedAt: string };

/** A form draft belongs to one signed-in user, object and form on this instance. */
export function useDraft<T>(form: string, initial: T, baseVersion: string | null): {
  value: T;
  setValue: React.Dispatch<React.SetStateAction<T>>;
  clear: () => void;
  recovery: ReactNode;
} {
  const { me } = useSession();
  const register = useContext(DraftGuardContext);
  const key = `${draftPrefix}${location.origin}:${me.id}:${form}`;
  const [pending, setPending] = useState<Saved<T> | null>(() => {
    try {
      const raw = localStorage.getItem(key);
      return raw === null ? null : JSON.parse(raw) as Saved<T>;
    } catch { return null; }
  });
  const [value, setRawValue] = useState(initial);
  const skip = useRef(false);
  // The field stays editable under the banner, and what is typed there is the
  // writer's answer to it: the waiting draft is dropped, and the new text is
  // saved and guarded from the first key on. Holding the new text back while
  // the banner waited lost it without a word the moment the screen was left.
  const setValue = useCallback<React.Dispatch<React.SetStateAction<T>>>((next) => {
    setPending(null);
    setRawValue(next);
  }, []);
  const initialText = JSON.stringify(initial);

  useEffect(() => {
    if (pending !== null) return;
    if (skip.current) {
      if (JSON.stringify(value) === initialText) skip.current = false;
      return;
    }
    try {
      if (JSON.stringify(value) === initialText) localStorage.removeItem(key);
      else localStorage.setItem(key, JSON.stringify({ value, baseVersion, savedAt: new Date().toISOString() } satisfies Saved<T>));
    } catch { /* A denied or full store must not stop editing. */ }
  }, [baseVersion, initialText, key, pending, value]);

  const clear = useCallback(() => {
    skip.current = true;
    setPending(null);
    try { localStorage.removeItem(key); } catch { /* See above. */ }
  }, [key]);

  const changed = pending === null && JSON.stringify(value) !== initialText;
  useEffect(() => register?.(key, changed, clear), [changed, clear, key, register]);

  const recovery = pending === null ? null : <div role="status" className="grid gap-2 rounded-lg border border-brand bg-accent p-3 text-sm">
    <p className="font-medium">A saved draft is available for this form.</p>
    <p className="text-muted-foreground">Saved {new Date(pending.savedAt).toLocaleString()}.
      {pending.baseVersion !== baseVersion && " The server changed since this draft was started. Review the current version before restoring."}
    </p>
    <div className="flex flex-wrap gap-2">
      <Button type="button" size="sm" onClick={() => { setRawValue(pending.value); setPending(null); }}>Restore draft</Button>
      <Button type="button" size="sm" variant="outline" onClick={() => { setPending(null); try { localStorage.removeItem(key); } catch { /* See above. */ } }}>Discard draft</Button>
    </div>
  </div>;

  return { value, setValue, clear, recovery };
}
