import { useId, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { api, describe, type Issue, type Schemas } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Markdown } from "@/shared/Markdown";
import { PageHeader } from "@/shared/PageHeader";
import { priorityLabel } from "./priority";

type NewIssue = Schemas["NewIssue"];

export function NewIssueView() {
  const { project } = useParams();
  const navigate = useNavigate();
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string>();

  async function save(draft: IssueDraft) {
    setSaving(true); setError(undefined);
    try {
      const item: NewIssue = { ref: null, title: draft.title, description: draft.description, priority: draft.priority, ready: draft.ready, labels: words(draft.labels), epic: blank(draft.epic), parent: blank(draft.parent), assignee: blank(draft.assignee), blocked_by: words(draft.blockedBy), blocks: [], status: draft.status };
      const { data, error: problem, response } = await api.POST("/issues", { body: { project: project!, issues: [item] } });
      if (!data) { setError(describe(problem, response.status)); return; }
      void navigate(`/${project}/issues/${data.items[0].key}`, { replace: true });
    } catch { setError("The instance did not answer."); } finally { setSaving(false); }
  }

  return <><PageHeader title="Create issue" /><IssueForm submit="Create issue" saving={saving} error={error} onSubmit={save} /></>;
}

export function EditIssueForm({ issue, onSaved, onCancel }: { issue: Issue; onSaved: (issue: Issue) => void; onCancel: () => void }) {
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string>();
  async function save(draft: IssueDraft) {
    setSaving(true); setError(undefined);
    const currentParking = issue.status === "backlog" ? "backlog" : "todo";
    const body = { title: draft.title, description: draft.description, priority: draft.priority, ready: draft.ready, labels: words(draft.labels), epic: blank(draft.epic), parent: blank(draft.parent), assignee: blank(draft.assignee), ...(draft.status === currentParking ? {} : { status: draft.status }) };
    try {
      const { data, error: problem, response } = await api.PATCH("/issues/{key}", { params: { path: { key: issue.key } }, headers: { "If-Match": issue.updated_at }, body: body as never });
      if (!data) { setError(describe(problem, response.status)); return; }
      onSaved(data);
    } catch { setError("The instance did not answer."); } finally { setSaving(false); }
  }
  return <IssueForm initial={issue} submit="Save changes" saving={saving} error={error} onSubmit={save} onCancel={onCancel} />;
}

type IssueDraft = { title: string; description: string; priority: number; ready: boolean; labels: string; epic: string; parent: string; assignee: string; blockedBy: string; status: "backlog" | "todo" };

function IssueForm({ initial, submit, saving, error, onSubmit, onCancel }: { initial?: Issue; submit: string; saving: boolean; error?: string; onSubmit: (draft: IssueDraft) => void; onCancel?: () => void }) {
  const [draft, setDraft] = useState<IssueDraft>({ title: initial?.title ?? "", description: initial?.description ?? "", priority: initial?.priority ?? 2, ready: initial?.ready ?? false, labels: initial?.labels.map((x) => x.name).join(", ") ?? "", epic: initial?.epic?.key ?? "", parent: initial?.parent?.key ?? "", assignee: initial?.assignee?.name ?? "", blockedBy: initial?.blocked_by.flatMap((x) => x.key ?? []).join(", ") ?? "", status: initial?.status === "backlog" ? "backlog" : "todo" });
  const set = <K extends keyof IssueDraft>(key: K, value: IssueDraft[K]) => setDraft((old) => ({ ...old, [key]: value }));
  return <form className="mx-auto grid w-full max-w-3xl gap-4 p-4 md:p-6" onSubmit={(event) => { event.preventDefault(); void onSubmit(draft); }}>
    <label className="grid gap-1 text-sm font-medium">Title<Input required autoFocus value={draft.title} onChange={(e) => set("title", e.target.value)} /></label>
    <MarkdownField label="Description" value={draft.description} onChange={(value) => set("description", value)} />
    <div className="grid gap-3 sm:grid-cols-3"><Select label="Priority" value={draft.priority} onChange={(value) => set("priority", Number(value))}>{[0,1,2,3,4].map((x) => <option key={x} value={x}>{priorityLabel(x)}</option>)}</Select><Select label="Status" value={draft.status} onChange={(value) => set("status", value as "backlog" | "todo")}><option value="todo">Todo</option><option value="backlog">Backlog</option></Select><label className="flex items-end gap-2 pb-2 text-sm"><input type="checkbox" checked={draft.ready} onChange={(e) => set("ready", e.target.checked)} /> Ready</label></div>
    <div className="grid gap-3 sm:grid-cols-2"><Text label="Labels" hint="Comma separated" value={draft.labels} change={(v) => set("labels", v)} /><Text label="Epic" value={draft.epic} change={(v) => set("epic", v)} /><Text label="Parent issue" value={draft.parent} change={(v) => set("parent", v)} /><Text label="Assignee" value={draft.assignee} change={(v) => set("assignee", v)} />{!initial && <Text label="Blocked by" hint="Comma separated issue keys" value={draft.blockedBy} change={(v) => set("blockedBy", v)} />}</div>
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    <div className="flex justify-end gap-2">{onCancel && <Button type="button" variant="outline" onClick={onCancel}>Cancel</Button>}<Button type="submit" disabled={saving}>{saving ? "Saving…" : submit}</Button></div>
  </form>;
}

export function MarkdownField({ label, value, onChange, required }: { label: string; value: string; onChange: (value: string) => void; required?: boolean }) {
  const [preview, setPreview] = useState(false);
  const id = useId();
  return <div className="grid gap-1 text-sm font-medium"><span className="flex items-center justify-between"><label htmlFor={id}>{label}</label><button type="button" className="text-xs font-normal text-brand hover:underline" onClick={() => setPreview((x) => !x)}>{preview ? "Edit" : "Preview"}</button></span>{preview ? <div className="min-h-32 rounded-lg border p-3"><Markdown>{value || "_Nothing to preview._"}</Markdown></div> : <textarea id={id} required={required} value={value} onChange={(e) => onChange(e.target.value)} className="min-h-32 rounded-lg border bg-background px-3 py-2 font-mono text-sm outline-none focus-visible:ring-3 focus-visible:ring-ring/50" />}</div>;
}

function Text({ label, hint, value, change }: { label: string; hint?: string; value: string; change: (value: string) => void }) { return <label className="grid gap-1 text-sm font-medium">{label}{hint && <span className="text-xs font-normal text-muted-foreground">{hint}</span>}<Input value={value} onChange={(e) => change(e.target.value)} /></label>; }
function Select({ label, value, onChange, children }: { label: string; value: string | number; onChange: (value: string) => void; children: React.ReactNode }) { return <label className="grid gap-1 text-sm font-medium">{label}<select className="h-8 rounded-lg border bg-background px-2" value={value} onChange={(e) => onChange(e.target.value)}>{children}</select></label>; }
function blank(value: string) { return value.trim() || null; }
function words(value: string) { return value.split(",").map((x) => x.trim()).filter(Boolean); }
