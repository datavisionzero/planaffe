import { useEffect, useId, useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router";
import { api, describe, type Issue, type Schemas } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { LabelPicker } from "@/components/ui/label-picker";
import { useLabels } from "@/projects/useLabels";
import { Markdown } from "@/shared/Markdown";
import { PageHeader } from "@/shared/PageHeader";
import { keyPath } from "@/shell/views";
import { priorityLabel } from "./priority";
import { statusLabel } from "./statusLabel";

type NewIssue = Schemas["NewIssue"];

export function NewIssueView() {
  const { project } = useParams();
  const navigate = useNavigate();
  // The epic screen leads here with its own key in the address, so the bracket
  // an issue is started under is not typed a second time.
  const [search] = useSearchParams();
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string>();

  async function save(draft: IssueDraft) {
    setSaving(true); setError(undefined);
    try {
      const item: NewIssue = { ref: null, title: draft.title, description: draft.description, priority: draft.priority, ready: draft.ready, labels: draft.labels, epic: blank(draft.epic), parent: blank(draft.parent), assignee: blank(draft.assignee), blocked_by: words(draft.blockedBy), blocks: [], status: draft.status };
      const { data, error: problem, response } = await api.POST("/issues", { body: { project: project!, issues: [item] } });
      if (!data) { setError(describe(problem, response.status)); return; }
      void navigate(keyPath(data.items[0].key), { replace: true });
    } catch { setError("The instance did not answer."); } finally { setSaving(false); }
  }

  return <><PageHeader title="Create issue" /><IssueForm epic={search.get("epic") ?? undefined} submit="Create issue" saving={saving} error={error} onSubmit={save} /></>;
}

export function EditIssueForm({ issue, onSaved, onCancel }: { issue: Issue; onSaved: (issue: Issue) => void; onCancel: () => void }) {
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string>();
  async function save(draft: IssueDraft) {
    setSaving(true); setError(undefined);
    const body = { title: draft.title, description: draft.description, priority: draft.priority, ready: draft.ready, labels: draft.labels, epic: blank(draft.epic), parent: blank(draft.parent), assignee: blank(draft.assignee), ...(parkable(issue) && draft.status !== issue.status ? { status: draft.status } : {}) };
    try {
      const { data, error: problem, response } = await api.PATCH("/issues/{key}", { params: { path: { key: issue.key } }, headers: { "If-Match": issue.updated_at }, body: body as never });
      if (!data) { setError(describe(problem, response.status)); return; }
      onSaved(data);
    } catch { setError("The instance did not answer."); } finally { setSaving(false); }
  }
  return <IssueForm initial={issue} submit="Save changes" saving={saving} error={error} onSubmit={save} onCancel={onCancel} />;
}

/**
 * Whether the editor may move the status at all. Parking is the one status
 * move that is a field write, `todo` to `backlog` and back on an open,
 * unclaimed issue (ADR 0016); every other move is an act of its own. Offering
 * the choice anywhere else sent a change the instance refuses, and the refusal
 * took the title and the description edited beside it with it.
 */
function parkable(issue: Issue | undefined): boolean {
  return issue === undefined || ((issue.status === "todo" || issue.status === "backlog") && issue.claim === null);
}

type IssueDraft = { title: string; description: string; priority: number; ready: boolean; labels: string[]; epic: string; parent: string; assignee: string; blockedBy: string; status: "backlog" | "todo" };

function IssueForm({ initial, epic, submit, saving, error, onSubmit, onCancel }: { initial?: Issue; epic?: string; submit: string; saving: boolean; error?: string; onSubmit: (draft: IssueDraft) => void; onCancel?: () => void }) {
  const [draft, setDraft] = useState<IssueDraft>({ title: initial?.title ?? "", description: initial?.description ?? "", priority: initial?.priority ?? 2, ready: initial?.ready ?? false, labels: initial?.labels.map((x) => x.name) ?? [], epic: initial?.epic?.key ?? epic ?? "", parent: initial?.parent?.key ?? "", assignee: initial?.assignee?.name ?? "", blockedBy: initial?.blocked_by.flatMap((x) => x.key ?? []).join(", ") ?? "", status: initial?.status === "backlog" ? "backlog" : "todo" });
  const set = <K extends keyof IssueDraft>(key: K, value: IssueDraft[K]) => setDraft((old) => ({ ...old, [key]: value }));
  const [reopens, setReopens] = useState<string>();
  const { project } = useParams();
  const { labels, create } = useLabels(project);

  // Attaching an issue to a closed epic reopens the epic, silently as far as
  // the HTTP call goes. The human interface asks for the warning, so the epic
  // that was typed is read when the field is left and says what saving will do.
  async function askAboutEpic(value: string) {
    const key = value.trim();
    setReopens(undefined);
    if (key === "" || key.toUpperCase() === (initial?.epic?.key ?? "").toUpperCase()) return;
    setReopens(await warnAboutEpic(key));
  }

  // An epic that arrived in the address was never typed, so nothing ever
  // leaves the field and the warning would first appear as a reopened epic.
  useEffect(() => {
    if (epic === undefined) return;
    let current = true;
    void warnAboutEpic(epic).then((warning) => { if (current) setReopens(warning); });
    return () => { current = false; };
  }, [epic]);

  return <form className="mx-auto grid w-full max-w-3xl gap-4 p-4 md:p-6" onSubmit={(event) => { event.preventDefault(); void onSubmit(draft); }}>
    <label className="grid gap-1 text-sm font-medium">Title<Input required autoFocus value={draft.title} onChange={(e) => set("title", e.target.value)} /></label>
    <MarkdownField label="Description" value={draft.description} onChange={(value) => set("description", value)} />
    <div className="grid gap-3 sm:grid-cols-3"><Select label="Priority" value={draft.priority} onChange={(value) => set("priority", Number(value))}>{[0,1,2,3,4].map((x) => <option key={x} value={x}>{priorityLabel(x)}</option>)}</Select>{parkable(initial) ? <Select label="Status" value={draft.status} onChange={(value) => set("status", value as "backlog" | "todo")}><option value="todo">Todo</option><option value="backlog">Backlog</option></Select> : <Select label="Status" hint="Changed through the issue's own actions" value={initial!.status} disabled onChange={() => undefined}><option value={initial!.status}>{statusLabel(initial!.status)}</option></Select>}<label className="flex items-end gap-2 pb-2 text-sm"><input type="checkbox" checked={draft.ready} onChange={(e) => set("ready", e.target.checked)} /> Ready</label></div>
    <LabelPicker label="Labels" labels={labels} value={draft.labels} onChange={(names) => set("labels", names)} onCreate={create} />
    <div className="grid gap-3 sm:grid-cols-2"><Text label="Epic" value={draft.epic} change={(v) => set("epic", v)} leave={(v) => void askAboutEpic(v)} /><Text label="Parent issue" value={draft.parent} change={(v) => set("parent", v)} /><Text label="Assignee" value={draft.assignee} change={(v) => set("assignee", v)} />{!initial && <Text label="Blocked by" hint="Comma separated issue keys" value={draft.blockedBy} change={(v) => set("blockedBy", v)} />}</div>
    {reopens && <p role="status" className="text-sm text-brand">{reopens}</p>}
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    <div className="flex justify-end gap-2">{onCancel && <Button type="button" variant="outline" onClick={onCancel}>Cancel</Button>}<Button type="submit" disabled={saving}>{saving ? "Saving…" : submit}</Button></div>
  </form>;
}

export function MarkdownField({ label, value, onChange, required }: { label: string; value: string; onChange: (value: string) => void; required?: boolean }) {
  const [preview, setPreview] = useState(false);
  const id = useId();
  return <div className="grid gap-1 text-sm font-medium"><span className="flex items-center justify-between"><label htmlFor={id}>{label}</label><button type="button" className="text-xs font-normal text-brand hover:underline" onClick={() => setPreview((x) => !x)}>{preview ? "Edit" : "Preview"}</button></span>{preview ? <div className="min-h-32 rounded-lg border p-3"><Markdown>{value || "_Nothing to preview._"}</Markdown></div> : <textarea id={id} required={required} value={value} onChange={(e) => onChange(e.target.value)} className="min-h-32 rounded-lg border bg-background px-3 py-2 font-mono text-sm outline-none focus-visible:ring-3 focus-visible:ring-ring/50" />}</div>;
}

function Text({ label, hint, value, change, leave }: { label: string; hint?: string; value: string; change: (value: string) => void; leave?: (value: string) => void }) { return <label className="grid gap-1 text-sm font-medium">{label}{hint && <span className="text-xs font-normal text-muted-foreground">{hint}</span>}<Input value={value} onChange={(e) => change(e.target.value)} onBlur={leave && ((e) => leave(e.target.value))} /></label>; }
function Select({ label, hint, value, disabled, onChange, children }: { label: string; hint?: string; value: string | number; disabled?: boolean; onChange: (value: string) => void; children: React.ReactNode }) { return <label className="grid gap-1 text-sm font-medium">{label}{hint && <span className="text-xs font-normal text-muted-foreground">{hint}</span>}<select className="h-8 rounded-lg border bg-background px-2 disabled:text-muted-foreground" value={value} disabled={disabled} onChange={(e) => onChange(e.target.value)}>{children}</select></label>; }
/** What saving an issue into that epic would also do, if anything. */
async function warnAboutEpic(key: string): Promise<string | undefined> {
  const { data } = await api.GET("/epics/{key}", { params: { path: { key } } });
  return data?.status === "closed" ? `${data.key} is closed. Saving attaches this issue and reopens the epic.` : undefined;
}

function blank(value: string) { return value.trim() || null; }
function words(value: string) { return value.split(",").map((x) => x.trim()).filter(Boolean); }
