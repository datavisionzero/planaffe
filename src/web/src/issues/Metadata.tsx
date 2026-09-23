import { useState } from "react";
import { Link } from "react-router";
import { api, describe, type Issue } from "@/api/client";
import { LabelPicker } from "@/components/ui/label-picker";
import { useAct } from "@/shared/act";
import { stale } from "@/shared/stale";
import { keyPath } from "@/shell/views";
import { date } from "./acts";
import { Field } from "./parts";
import { AssigneePicker } from "./pickers";
import { priorityLabel } from "./priorityLabel";
import { StatusDot } from "./status";

/**
 * The metadata column. Each choice is written on its own, against the version
 * the screen holds, and says which field it is saving while it does.
 */
export function Metadata({ issue, onChanged }: { issue: Issue; onChanged: (issue: Issue) => void }) {
  const { doing, error, run } = useAct();
  const [message, setMessage] = useState("");
  const busy = doing !== null;

  async function write(field: string, body: object) {
    if (busy) return;
    setMessage("");
    await run(async () => {
      const answer = await api.PATCH("/issues/{key}", { params: { path: { key: issue.key } }, headers: { "If-Match": issue.updated_at }, body: body as never });
      const current = stale<Issue>(answer);
      if (current) { onChanged(current); throw new Error(`${field} changed elsewhere. Review the latest value and choose again.`); }
      if (!answer.data) throw new Error(describe(answer.error, answer.response.status));
      onChanged(answer.data);
      setMessage(`${field} saved.`);
    }, field);
  }

  return <aside className="shrink-0 space-y-3 border-t p-4 text-sm md:w-64 md:border-t-0 md:border-l" aria-label="Issue details">
    <h2 className="font-medium">Details</h2>
    <Field name="Status"><StatusDot status={issue.status} withLabel /></Field>
    <Field name="Priority"><select name="priority" aria-label="Priority" value={issue.priority} disabled={busy} onChange={(event) => void write("Priority", { priority: Number(event.target.value) })} className="mt-1 h-8 w-full rounded-lg border bg-background px-2 text-sm">{[0, 1, 2, 3, 4].map((priority) => <option key={priority} value={priority}>{priorityLabel(priority)}</option>)}</select></Field>
    <Field name="Ready">{issue.ready ? "yes" : "no"}</Field>
    {issue.epic && <Field name="Epic"><Link to={keyPath(issue.epic.key)} className="text-brand hover:underline">{issue.epic.key}</Link> <span className="text-muted-foreground">{issue.epic.title}</span></Field>}
    {issue.claim && <Field name="Claimed by">{issue.claim.holder.name}<span className="text-muted-foreground">{issue.claim.expires_at === null ? " · does not expire" : ` · until ${date(issue.claim.expires_at)}`}</span></Field>}
    <fieldset disabled={busy}><AssigneePicker project={issue.project} value={issue.assignee?.name ?? ""} onChange={(name) => void write("Assignee", { assignee: name || null })} /></fieldset>
    <fieldset disabled={busy}><LabelPicker label="Labels" labels={issue.project_context.labels} value={issue.labels.map((label) => label.name)} onChange={(names) => void write("Labels", { labels: names })} /></fieldset>
    {doing && <p role="status" className="text-xs text-muted-foreground">Saving {doing.toLowerCase()}…</p>}
    {message && <p role="status" className="text-xs text-muted-foreground">{message}</p>}
    {error && <p role="alert" className="text-xs text-destructive">{error}</p>}
    <Field name="Author">{issue.author.name}</Field><Field name="Created">{date(issue.created_at)}</Field><Field name="Updated">{date(issue.updated_at)}</Field><Field name="Release">{issue.release === null ? <span className="text-muted-foreground">not in a release</span> : issue.release}</Field>
  </aside>;
}
