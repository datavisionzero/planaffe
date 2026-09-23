import { useState, type ReactNode } from "react";
import { Link } from "react-router";
import { api, describe, type Issue } from "@/api/client";
import { Button } from "@/components/ui/button";
import { failure } from "@/shared/act";
import { stale } from "@/shared/stale";
import { keyPath } from "@/shell/views";

/** The positive answer comes from the same selection query as next. */
export function IssueWorkability({ issue, onChanged }: { issue: Issue; onChanged: (issue: Issue) => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const reasons: ReactNode[] = [];
  if (issue.status !== "todo") {
    reasons.push(issue.status === "backlog" ? "Parked in backlog." : issue.status === "review" ? "In review; decide the result first." : issue.status === "in_progress" ? "Already in progress." : "Closed.");
  }
  if (issue.claim) reasons.push(`Claim held by ${issue.claim.holder.name}.`);
  if (issue.open_questions > 0) {
    const first = issue.questions.find((question) => question.answer === null);
    reasons.push(first ? <a className="text-brand hover:underline" href={`#question-${first.id}`}>{issue.open_questions} open {issue.open_questions === 1 ? "question needs" : "questions need"} an answer.</a> : `${issue.open_questions} open questions need an answer.`);
  }
  if (issue.open_blockers > 0) reasons.push(<a className="text-brand hover:underline" href="#open-blockers">{issue.open_blockers} open {issue.open_blockers === 1 ? "blocker" : "blockers"} must close.</a>);
  if (issue.open_sub_issues > 0) reasons.push(`${issue.open_sub_issues} open ${issue.open_sub_issues === 1 ? "sub-issue" : "sub-issues"} must close.`);
  if (issue.project_context.triage_required && !issue.ready) reasons.push("This project requires Ready before next can take it.");
  if (issue.workability.parent_gated) reasons.push(issue.parent ? <>Parent <Link className="text-brand hover:underline" to={keyPath(issue.parent.key)}>{issue.parent.key}</Link> is parked, closed, or blocked.</> : "Its parent is unavailable.");

  async function setReady() {
    setBusy(true); setError("");
    try {
      const answer = await api.PATCH("/issues/{key}", { params: { path: { key: issue.key } }, headers: { "If-Match": issue.updated_at }, body: { ready: true } as never });
      const current = stale<Issue>(answer);
      if (current) { onChanged(current); setError("This issue changed. Check its latest state and set Ready again if needed."); return; }
      if (!answer.data) throw new Error(describe(answer.error, answer.response.status));
      onChanged(answer.data);
    } catch (reason) {
      setError(failure(reason));
    } finally { setBusy(false); }
  }

  const workable = issue.workability.workable && reasons.length === 0;
  return <section className="mb-6 rounded-lg border bg-muted/20 p-4 text-sm" aria-label="Workability">
    <h2 className="font-medium">Workable: {workable ? "yes" : "no"}</h2>
    {workable
      ? <p className="mt-1">{issue.assignee ? `Only ${issue.assignee.name} can take this issue when next selects it.` : "An agent can take this issue when next selects it."}</p>
      : <ul className="mt-2 list-disc space-y-1 pl-5">{reasons.length > 0 ? reasons.map((reason, index) => <li key={index}>{reason}</li>) : <li>The instance cannot hand out this issue yet. Its details may have changed.</li>}</ul>}
    <p className="mt-2 text-xs text-muted-foreground">Ready: {issue.ready ? "set" : "not set"}. {issue.project_context.triage_required ? "Required by this project." : "Not required by this project."}</p>
    {issue.project_context.triage_required && !issue.ready && issue.status === "todo" && <Button className="mt-2" size="sm" variant="outline" disabled={busy} onClick={() => void setReady()}>{busy ? "Working…" : "Set ready"}</Button>}
    {error && <p role="alert" className="mt-2 text-xs text-destructive">{error}</p>}
  </section>;
}
