import { useContext, useEffect, useState } from "react";
import { api } from "@/api/client";
import { AttentionContext, type Attention } from "./attention";

/**
 * How long one held read waits to be told before it is answered `304` and
 * asked again. The instance allows up to an hour and an operator's proxy has
 * to allow that (`docs/operations.md`), but a shorter round is what a browser
 * on a laptop that sleeps and a phone that changes network want: a connection
 * that died without saying so is noticed within a minute rather than an hour.
 */
const round = 60;

/** How long the loop waits after a failed read, doubling, up to the second. */
const firstRetry = 1_000;
const longestRetry = 30_000;

/** The least a round may take, so that nothing can turn the loop into a spin. */
const floor = 1_000;

/**
 * The frame's one read of "Needs you", for the number in the navigation, and
 * the wake pulse the screen that lists the same work watches.
 *
 * It lives once per project, above both the sidebar and the screen, because
 * the two read the same list and may not each hold a connection for it: under
 * HTTP/1.1 a browser has six per origin, and every held one is a click that
 * has to wait. `limit=1` because only `total` is wanted; the items are the
 * screen's.
 *
 * The loop is one read after another: the first plain, so that a list that is
 * empty is known to be empty, then held ones carrying the last `ETag`, each of
 * which the instance answers the moment the list differs — the wake channel of
 * `docs/api.md`, Waiting, which the CLI has used since it existed.
 */
export function useAttentionState(project: string | undefined): Attention {
  const [known, setKnown] = useState<{ of: string; needsYou: number; pulse: number } | null>(null);

  useEffect(() => {
    if (project === undefined) {
      return;
    }

    // One controller per project: a read for a project that is no longer open
    // is dropped rather than allowed to answer into the wrong navigation.
    const stop = new AbortController();

    void (async () => {
      let validator: string | undefined;
      let failures = 0;

      while (!stop.signal.aborted) {
        // A hidden tab holds nothing. Coming back reads at once, so that the
        // number is right before the human has looked at it.
        await whileVisible(stop.signal);
        if (stop.signal.aborted) {
          return;
        }

        const answer = await read(project, validator, stop.signal);

        if (answer.at === "interrupted") {
          continue;
        }

        if (answer.at === "failed") {
          // The number stays as it is. An instance that is restarting is not a
          // reason for the navigation to forget what it last knew, and not a
          // reason to keep asking it either.
          failures += 1;
          await pause(Math.min(firstRetry * 2 ** (failures - 1), longestRetry), stop.signal);
          continue;
        }

        failures = 0;

        if (answer.at === "same") {
          // The instance only answers `304` at its deadline, so this has
          // already waited a round. Something in between that answered it out
          // of a cache would not have, and a loop that trusted that would spin
          // against the instance for as long as the tab is open.
          await pause(floor, stop.signal);
          continue;
        }

        const first = validator === undefined;
        validator = answer.validator;
        setKnown((was) => {
          const mine = was !== null && was.of === project;
          return { of: project, needsYou: answer.needsYou, pulse: (mine ? was.pulse : 0) + (first ? 0 : 1) };
        });

        // No validator, no wake channel — something between here and the
        // instance dropped the header. Reading again at once would be a tight
        // loop, so this falls back to asking once a round.
        if (validator === undefined) {
          await pause(round * 1_000, stop.signal);
        }
      }
    })();

    return () => stop.abort();
  }, [project]);

  const mine = known !== null && known.of === project;

  return { needsYou: mine ? known.needsYou : null, pulse: mine ? known.pulse : 0 };
}

/** The same number and the same pulse, for the sidebar and for the screen. */
export function useAttention(): Attention {
  const attention = useContext(AttentionContext);

  if (attention === null) {
    throw new Error("useAttention is only for the frame and the screens under it.");
  }

  return attention;
}

type Answer =
  | { at: "count"; needsYou: number; validator: string | undefined }
  | { at: "same" }
  | { at: "interrupted" }
  | { at: "failed" };

/**
 * One read, held when there is a validator to hold it against. It is given up
 * when the tab goes away as well as when the project does: nobody is looking,
 * and the connection is worth more to the tab that is.
 */
async function read(project: string, validator: string | undefined, outer: AbortSignal): Promise<Answer> {
  const request = new AbortController();
  const give = () => request.abort();
  const hide = () => {
    if (document.hidden) {
      request.abort();
    }
  };

  outer.addEventListener("abort", give, { once: true });
  document.addEventListener("visibilitychange", hide);

  try {
    const { data, response } = await api.GET("/projects/{key}/needs-you", {
      params: {
        path: { key: project },
        query: { limit: 1, wait: validator === undefined ? undefined : round },
      },
      headers: validator === undefined ? undefined : { "If-None-Match": validator },
      signal: request.signal,
    });

    if (response.status === 304) {
      return { at: "same" };
    }

    return data === undefined
      ? { at: "failed" }
      : { at: "count", needsYou: data.total, validator: response.headers.get("ETag") ?? undefined };
  } catch {
    return request.signal.aborted ? { at: "interrupted" } : { at: "failed" };
  } finally {
    outer.removeEventListener("abort", give);
    document.removeEventListener("visibilitychange", hide);
  }
}

/** Resolves as soon as somebody could be looking at this tab, or never mind. */
function whileVisible(signal: AbortSignal): Promise<void> {
  if (!document.hidden || signal.aborted) {
    return Promise.resolve();
  }

  return new Promise((go) => {
    const done = () => {
      document.removeEventListener("visibilitychange", shown);
      signal.removeEventListener("abort", done);
      go();
    };
    const shown = () => {
      if (!document.hidden) {
        done();
      }
    };

    document.addEventListener("visibilitychange", shown);
    signal.addEventListener("abort", done);
  });
}

function pause(milliseconds: number, signal: AbortSignal): Promise<void> {
  if (signal.aborted) {
    return Promise.resolve();
  }

  return new Promise((go) => {
    const done = () => {
      clearTimeout(timer);
      signal.removeEventListener("abort", done);
      go();
    };
    const timer = setTimeout(done, milliseconds);

    signal.addEventListener("abort", done);
  });
}
