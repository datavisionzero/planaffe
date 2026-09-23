import { useCallback, useState } from "react";

/**
 * The one sentence a failed act leaves behind: what the instance said where it
 * said something, and that it did not answer where it said nothing. Every
 * write that throws its refusal as an `Error` is read back through this.
 */
export function failure(reason: unknown): string {
  return reason instanceof Error ? reason.message : "The instance did not answer.";
}

/**
 * One act at a time and what became of it, the shape every control that
 * writes has: busy while it runs, the refusal afterwards, nothing on success.
 *
 * `run` hands back what the act answered, or `undefined` where it failed, so
 * the caller decides what a success does. `doing` says which act is under way
 * where one control carries several — the metadata column saves one field at
 * a time and says which.
 */
export function useAct() {
  const [doing, setDoing] = useState<string | null>(null);
  const [error, setError] = useState<string>();

  const run = useCallback(async <T>(act: () => Promise<T>, what = ""): Promise<T | undefined> => {
    setDoing(what);
    setError(undefined);
    try {
      return await act();
    } catch (reason) {
      setError(failure(reason));
      return undefined;
    } finally {
      setDoing(null);
    }
  }, []);

  return { busy: doing !== null, doing, error, setError, run };
}
