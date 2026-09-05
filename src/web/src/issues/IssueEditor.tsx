import { useId, useState } from "react";
import { useLocation, useNavigate, useParams, useSearchParams } from "react-router";
import { api, codeOf, describe, type Issue, type Problem, type Schemas } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { LabelPicker } from "@/components/ui/label-picker";
import { useEpics } from "@/epics/useEpics";
import { useLabels } from "@/projects/useLabels";
import { Markdown } from "@/shared/Markdown";
import { useAbandon } from "@/shared/abandon";
import { PageHeader } from "@/shared/PageHeader";
import { keyPath } from "@/shell/views";
import { AssigneePicker, EpicPicker, IssuePicker } from "./pickers";
import { priorityLabel } from "./priority";
import { statusLabel } from "./statusLabel";

type NewIssue = Schemas["NewIssue"];

export function NewIssueView() {
  const { project } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  // The epic screen leads here with its own key in the address, so the bracket
  // an issue is started under is not typed a second time.
  const [search] = useSearchParams();
  const [saving, setSaving] = useState(false);
  const [refused, setRefused] = useState<Refusal>();

  async function save(draft: IssueDraft) {
    setSaving(true); setRefused(undefined);
    try {
      const item: NewIssue = { ref: null, title: draft.title, description: draft.description, priority: draft.priority, ready: draft.ready, labels: draft.labels, epic: blank(draft.epic), parent: blank(draft.parent), assignee: blank(draft.assignee), blocked_by: draft.blockedBy, blocks: [], status: draft.status };
      const { data, error: problem, response } = await api.POST("/issues", { body: { project: project!, issues: [item] } });
      if (!data) { setRefused(refusal(problem, response.status)); return; }
      void navigate(keyPath(data.items[0].key), { replace: true });
    } catch { setRefused({ fields: {} , why: "The instance did not answer." }); } finally { setSaving(false); }
  }

  // Back where the form was opened from — the list `c` was pressed on, or the
  // epic whose key came along in the address — rather than at the issue list
  // whatever the way in was. A form opened by its own link has nothing behind
  // it, and falls back to the epic it names, or to the list.
  function cancel() {
    if (location.key !== "default") { void navigate(-1); return; }
    const epic = search.get("epic");
    void navigate(epic === null ? `/${project}/issues` : keyPath(epic));
  }

  return <><PageHeader title="Create issue" /><IssueForm epic={search.get("epic") ?? undefined} submit="Create issue" saving={saving} refused={refused} onSubmit={save} onCancel={cancel} /></>;
}

export function EditIssueForm({ issue, onSaved, onCancel }: { issue: Issue; onSaved: (issue: Issue) => void; onCancel: () => void }) {
  const [saving, setSaving] = useState(false);
  const [refused, setRefused] = useState<Refusal>();
  async function save(draft: IssueDraft) {
    setSaving(true); setRefused(undefined);
    const body = { title: draft.title, description: draft.description, priority: draft.priority, ready: draft.ready, labels: draft.labels, epic: blank(draft.epic), parent: blank(draft.parent), assignee: blank(draft.assignee), ...(parkable(issue) && draft.status !== issue.status ? { status: draft.status } : {}) };
    try {
      const { data, error: problem, response } = await api.PATCH("/issues/{key}", { params: { path: { key: issue.key } }, headers: { "If-Match": issue.updated_at }, body: body as never });
      if (!data) { setRefused(refusal(problem, response.status)); return; }
      onSaved(data);
    } catch { setRefused({ fields: {}, why: "The instance did not answer." }); } finally { setSaving(false); }
  }
  return <IssueForm initial={issue} submit="Save changes" saving={saving} refused={refused} onSubmit={save} onCancel={onCancel} />;
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

type IssueDraft = { title: string; description: string; priority: number; ready: boolean; labels: string[]; epic: string; parent: string; assignee: string; blockedBy: string[]; status: "backlog" | "todo" };

/**
 * A refused save, taken apart: what belongs at a field, and what is left for
 * the form. A refusal over the whole form for a key that was mistyped is what
 * the pickers exist to end, so a refusal that names a field is shown there.
 */
type Refusal = { why?: string; fields: Record<string, string> };

const atField = ["epic", "parent", "assignee", "blocked_by", "labels"];

/** Which refusals name a field, and which field that is. */
const codeField: Record<string, string> = {
  "one-level": "parent",
  "other-project": "parent",
  "epic-inherited": "epic",
  cycle: "blocked_by",
  "unknown-label": "labels",
};

function refusal(problem: Problem | undefined, status: number): Refusal {
  // `validation` carries `errors`, field to message (`docs/api.md`, Errors);
  // it is an extension member, so the generated type does not know it.
  const errors = (problem as { errors?: Record<string, string> } | undefined)?.errors ?? {};
  const named = codeField[codeOf(problem) ?? ""];
  const all = named === undefined ? errors : { ...errors, [named]: describe(problem, status) };
  const fields = Object.fromEntries(Object.entries(all).filter(([field]) => atField.includes(field)));
  const rest = Object.keys(all).filter((field) => !atField.includes(field));

  return { fields, why: rest.length > 0 || Object.keys(fields).length === 0 ? describe(problem, status) : undefined };
}

/** The form as it starts out, and what "changed" is measured against. */
function startingDraft(initial: Issue | undefined, epic: string | undefined): IssueDraft {
  return { title: initial?.title ?? "", description: initial?.description ?? "", priority: initial?.priority ?? 2, ready: initial?.ready ?? false, labels: initial?.labels.map((x) => x.name) ?? [], epic: initial?.epic?.key ?? epic ?? "", parent: initial?.parent?.key ?? "", assignee: initial?.assignee?.name ?? "", blockedBy: initial?.blocked_by.flatMap((x) => x.key ?? []) ?? [], status: initial?.status === "backlog" ? "backlog" : "todo" };
}

function IssueForm({ initial, epic, submit, saving, refused, onSubmit, onCancel }: { initial?: Issue; epic?: string; submit: string; saving: boolean; refused?: Refusal; onSubmit: (draft: IssueDraft) => void; onCancel: () => void }) {
  const [start] = useState(() => startingDraft(initial, epic));
  const [draft, setDraft] = useState<IssueDraft>(start);
  const { leave, dialog } = useAbandon(JSON.stringify(draft) !== JSON.stringify(start), onCancel);
  const set = <K extends keyof IssueDraft>(key: K, value: IssueDraft[K]) => setDraft((old) => ({ ...old, [key]: value }));
  const { project } = useParams();
  const { labels, create } = useLabels(project);
  const epics = useEpics(project);
  const at = refused?.fields ?? {};

  // Attaching an issue to a closed epic reopens the epic, silently as far as
  // the HTTP call goes. The choice says which epics are closed on their own
  // rows; this is the same fact once one of them is chosen, said as what
  // saving will do.
  const chosenEpic = epics.find((known) => known.key === draft.epic);
  const reopens = chosenEpic?.status === "closed" && chosenEpic.key !== initial?.epic?.key
    ? `${chosenEpic.key} is closed. Saving attaches this issue and reopens the epic.`
    : undefined;

  return <form className="mx-auto grid w-full max-w-3xl gap-4 p-4 md:p-6" onSubmit={(event) => { event.preventDefault(); void onSubmit(draft); }}>
    <label className="grid gap-1 text-sm font-medium">Title<Input required autoFocus value={draft.title} onChange={(e) => set("title", e.target.value)} /></label>
    <MarkdownField label="Description" value={draft.description} onChange={(value) => set("description", value)} />
    <div className="grid gap-3 sm:grid-cols-3"><Select label="Priority" value={draft.priority} onChange={(value) => set("priority", Number(value))}>{[0,1,2,3,4].map((x) => <option key={x} value={x}>{priorityLabel(x)}</option>)}</Select>{parkable(initial) ? <Select label="Status" value={draft.status} onChange={(value) => set("status", value as "backlog" | "todo")}><option value="todo">Todo</option><option value="backlog">Backlog</option></Select> : <Select label="Status" hint="Changed through the issue's own actions" value={initial!.status} disabled onChange={() => undefined}><option value={initial!.status}>{statusLabel(initial!.status)}</option></Select>}<label className="flex items-end gap-2 pb-2 text-sm"><input type="checkbox" checked={draft.ready} onChange={(e) => set("ready", e.target.checked)} /> Ready</label></div>
    <LabelPicker label="Labels" labels={labels} value={draft.labels} onChange={(names) => set("labels", names)} onCreate={create} error={at.labels} />
    <div className="grid gap-3 sm:grid-cols-2"><EpicPicker epics={epics} value={draft.epic} onChange={(key) => set("epic", key)} error={at.epic} /><IssuePicker label="Parent issue" project={project} exclude={initial ? [initial.key] : []} value={draft.parent === "" ? [] : [draft.parent]} onChange={(keys) => set("parent", keys[0] ?? "")} error={at.parent} /><AssigneePicker project={project} value={draft.assignee} onChange={(name) => set("assignee", name)} error={at.assignee} />{!initial && <IssuePicker label="Blocked by" project={project} multiple value={draft.blockedBy} onChange={(keys) => set("blockedBy", keys)} error={at.blocked_by} />}</div>
    {reopens && <p role="status" className="text-sm text-brand">{reopens}</p>}
    {refused?.why && <p role="alert" className="text-sm text-destructive">{refused.why}</p>}
    <div className="flex justify-end gap-2"><Button type="button" variant="outline" onClick={leave}>Cancel</Button><Button type="submit" disabled={saving}>{saving ? "Saving…" : submit}</Button></div>
    {dialog}
  </form>;
}

export function MarkdownField({ label, value, onChange, required }: { label: string; value: string; onChange: (value: string) => void; required?: boolean }) {
  const [preview, setPreview] = useState(false);
  const id = useId();
  return <div className="grid gap-1 text-sm font-medium"><span className="flex items-center justify-between"><label htmlFor={id}>{label}</label><button type="button" className="text-xs font-normal text-brand hover:underline" onClick={() => setPreview((x) => !x)}>{preview ? "Edit" : "Preview"}</button></span>{preview ? <div className="min-h-32 rounded-lg border p-3"><Markdown>{value || "_Nothing to preview._"}</Markdown></div> : <textarea id={id} required={required} value={value} onChange={(e) => onChange(e.target.value)} className="min-h-32 rounded-lg border bg-background px-3 py-2 font-mono text-sm outline-none focus-visible:ring-3 focus-visible:ring-ring/50" />}</div>;
}

function Select({ label, hint, value, disabled, onChange, children }: { label: string; hint?: string; value: string | number; disabled?: boolean; onChange: (value: string) => void; children: React.ReactNode }) { return <label className="grid gap-1 text-sm font-medium">{label}{hint && <span className="text-xs font-normal text-muted-foreground">{hint}</span>}<select className="h-8 rounded-lg border bg-background px-2 disabled:text-muted-foreground" value={value} disabled={disabled} onChange={(e) => onChange(e.target.value)}>{children}</select></label>; }
function blank(value: string) { return value.trim() || null; }
