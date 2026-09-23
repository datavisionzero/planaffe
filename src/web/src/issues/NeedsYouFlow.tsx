import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router";
import { api, describe } from "@/api/client";
import { Button } from "@/components/ui/button";
import { failure } from "@/shared/act";
import { needsYouIssuePath, viewOf, viewPath } from "@/shell/views";

type Place = { at: "current" } | { at: "after"; candidates: string[] };
type Step = { at: "checking" } | { at: "current" } | { at: "done" } | { at: "next"; candidates: string[] } | { at: "failed"; why: string };

/**
 * Where the issue stands in Needs you, against the current server order and
 * not a saved row number: still on the list, or gone from it with these
 * waiting behind it. The candidates are only listed here; which of them can
 * still be opened is asked when somebody wants to go on, not on every wake.
 */
async function place(issueKey: string, signal: AbortSignal): Promise<Place> {
  const project = issueKey.slice(0, issueKey.indexOf("-"));
  const candidates: string[] = [];
  let cursor: string | undefined;
  do {
    const { data, error, response } = await api.GET("/projects/{key}/needs-you", {
      params: { path: { key: project }, query: { cursor, limit: 50 } }, signal,
    });
    if (!data) throw new Error(describe(error, response.status));
    for (const item of data.items) {
      if (item.issue.key === issueKey) return { at: "current" };
      candidates.push(item.issue.key);
    }
    cursor = data.next_cursor ?? undefined;
  } while (cursor !== undefined && !signal.aborted);
  return { at: "after", candidates };
}

/** The first candidate that still opens: one read each, in order, until one does. */
async function firstOpen(candidates: string[]): Promise<string | undefined> {
  for (const key of candidates) {
    const { data, error, response } = await api.GET("/issues/{key}", { params: { path: { key } } });
    if (data) return key;
    if (response.status !== 403 && response.status !== 404) throw new Error(describe(error, response.status));
  }
  return undefined;
}

/**
 * The source list stays one click away, and a completed act offers the next
 * live item. It follows the Needs you list's own wake pulse, not every change
 * to an issue in the project: only a change to that list can move this issue
 * on or off it.
 */
export function NeedsYouFlow({ issueKey, revision, pulse }: { issueKey: string; revision: number; pulse: number }) {
  const navigate = useNavigate();
  const project = issueKey.slice(0, issueKey.indexOf("-"));
  const back = viewPath(project, viewOf("needs-you"));
  const [step, setStep] = useState<Step>({ at: "checking" });
  const [retry, setRetry] = useState(0);

  useEffect(() => {
    const stop = new AbortController();
    void place(issueKey, stop.signal).then((value) => {
      if (stop.signal.aborted) return;
      setStep(value.at === "current" ? value : value.candidates.length === 0 ? { at: "done" } : { at: "next", candidates: value.candidates });
    }, (reason) => {
      if (!stop.signal.aborted) setStep({ at: "failed", why: failure(reason) });
    });
    return () => stop.abort();
  }, [issueKey, revision, pulse, retry]);

  async function advance(candidates: string[]) {
    setStep({ at: "checking" });
    try {
      const key = await firstOpen(candidates);
      if (key !== undefined) void navigate(needsYouIssuePath(key));
      else setStep({ at: "done" });
    } catch (reason) {
      setStep({ at: "failed", why: failure(reason) });
    }
  }

  return <div className="flex flex-wrap items-center gap-3 border-b bg-muted/20 px-4 py-2 text-sm" aria-label="Needs you workflow">
    <Link className="text-brand hover:underline" to={back}>← Back to Needs you</Link>
    {step.at === "next" && <Button size="sm" variant="outline" onClick={() => void advance(step.candidates)}>Next waiting issue</Button>}
    {step.at === "done" && <span role="status">All caught up. Nothing else needs you.</span>}
    {step.at === "current" && revision > 0 && <span role="status">This issue still needs your attention.</span>}
    {step.at === "failed" && <span role="alert">Could not find the next issue: {step.why} <button className="underline" onClick={() => setRetry((value) => value + 1)}>Try again</button></span>}
  </div>;
}
