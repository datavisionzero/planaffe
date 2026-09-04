import { useEffect, useState } from "react";
import { Link, useParams } from "react-router";
import { api, describe, type HistoryEntry, type Issue } from "@/api/client";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Markdown } from "@/shared/Markdown";
import { ActionDialog } from "@/shared/ActionDialog";
import { PageHeader } from "@/shared/PageHeader";
import { keyPath, pathKey } from "@/shell/views";
import { priorityLabel } from "./priority";
import { StatusDot } from "./status";
import { EditIssueForm, MarkdownField } from "./IssueEditor";

type Load<T> = { at: "asking" } | { at: "failed"; why: string } | { at: "known"; value: T };
const asking = { at: "asking" } as const;

/** The complete issue, ordered by what a human needs from it now. */
export function IssueView() {
  const { project, number } = useParams();
  const key = pathKey(project!, number!);
  const [state, setState] = useState<{ key: string; issue: Load<Issue>; history: Load<HistoryEntry[]> }>();
  const [editing, setEditing] = useState(false);
  const [deleted, setDeleted] = useState<Issue>();
  const current = state !== undefined && state.key === key ? state : { key, issue: asking, history: asking };

  useEffect(() => {
    let live = true;
    void api.GET("/issues/{key}", { params: { path: { key } } }).then(({ data, error, response }) => live && setState((old) => ({ key, history: old !== undefined && old.key === key ? old.history : asking, issue: data ? { at: "known", value: data } : { at: "failed", why: describe(error, response.status) } })), () => live && setState((old) => ({ key, history: old?.history ?? asking, issue: { at: "failed", why: "The instance did not answer." } })));
    void api.GET("/issues/{key}/history", { params: { path: { key } } }).then(({ data, error, response }) => live && setState((old) => ({ key, issue: old !== undefined && old.key === key ? old.issue : asking, history: data ? { at: "known", value: data } : { at: "failed", why: describe(error, response.status) } })), () => live && setState((old) => ({ key, issue: old?.issue ?? asking, history: { at: "failed", why: "The instance did not answer." } })));
    return () => { live = false; };
  }, [key]);

  if (current.issue.at === "asking") return <><PageHeader title={<Skeleton className="h-4 w-64" />} /><div className="space-y-3 p-4"><Skeleton className="h-3 w-full" /><Skeleton className="h-3 w-5/6" /></div></>;
  if (current.issue.at === "failed") return <><PageHeader title={key} /><p className="p-4 text-sm text-destructive">{current.issue.why}</p></>;
  const issue = current.issue.value;
  const changed = (value: Issue) => { setState((old) => ({ key, issue: { at: "known", value }, history: old?.history ?? asking })); setEditing(false); };

  if (deleted) return <><PageHeader title={deleted.key} /><div className="m-auto grid max-w-md justify-items-center gap-3 p-8 text-center"><p>This issue is deleted and hidden from the project.</p><IssueAction label="Restore issue" path="/issues/{key}/restore" issue={deleted} body={undefined} onChanged={(value) => { setDeleted(undefined); changed(value); }} /></div></>;
  if (editing) return <><PageHeader title={`Edit ${issue.key}`} /><EditIssueForm issue={issue} onSaved={changed} onCancel={() => setEditing(false)} /></>;

  return <><PageHeader title={<span className="flex items-center gap-2"><span className="font-mono text-xs font-normal text-muted-foreground">{issue.key}</span>{issue.title}</span>}><Button variant="outline" size="sm" onClick={() => setEditing(true)}>Edit</Button></PageHeader>
    <div className="flex flex-1 flex-col md:flex-row">
      <main className="min-w-0 flex-1 p-4 md:p-6">
        <Attention issue={issue} onChanged={changed} />
        <Section title="Description">{isLong(issue.description) ? <details><summary className="mb-3 cursor-pointer text-sm font-medium text-brand">Show full description</summary><Markdown>{issue.description}</Markdown></details> : <Markdown>{issue.description}</Markdown>}</Section>
        {issue.result !== null && <Section title="Result"><Markdown>{issue.result}</Markdown></Section>}
        <Relationships issue={issue} />
        <Conversation issue={issue} />
        <Actions issue={issue} onChanged={changed} onDeleted={() => setDeleted(issue)} />
        <History loaded={current.history} />
      </main>
      <Metadata issue={issue} />
    </div>
  </>;
}

function Attention({ issue, onChanged }: { issue: Issue; onChanged: (issue: Issue) => void }) {
  return <div className="mb-6 space-y-3" aria-label="Needs attention">
    {issue.questions.filter((q) => q.answer === null).map((q) => <aside key={q.id} className="rounded-lg border border-brand bg-accent p-4"><Eyebrow>Answer needed</Eyebrow><Markdown className="mt-2">{q.question}</Markdown><Byline name={q.asked_by.name} at={q.asked_at} /><TextAction label="Answer" onRun={async (text) => { const result = await api.POST("/questions/{id}/answer", { params: { path: { id: q.id } }, body: { answer: text } }); if (!result.data) throw new Error(describe(result.error, result.response.status)); return { ...issue, questions: issue.questions.map((x) => x.id === q.id ? result.data! : x), open_questions: issue.open_questions - 1 }; }} onChanged={onChanged} /></aside>)}
    {issue.status === "review" && <aside className="rounded-lg border border-brand bg-accent p-4"><Eyebrow>Review needed</Eyebrow><p className="mt-1 text-sm">Decide whether this work is done, canceled, or should return to todo.</p>{issue.result !== null && <Markdown className="mt-3">{issue.result}</Markdown>}<div className="mt-3 flex flex-wrap gap-2"><IssueAction label="Accept as done" path="/issues/{key}/close" issue={issue} body={{ status: "done", result: issue.result }} onChanged={onChanged} /><IssueAction label="Accept as canceled" variant="outline" path="/issues/{key}/close" issue={issue} body={{ status: "canceled", result: issue.result }} onChanged={onChanged} /></div><TextAction label="Return to todo" placeholder="What needs to change?" onRun={(comment) => issueRequest("/issues/{key}/reopen", issue, { comment })} onChanged={onChanged} /></aside>}
    {issue.open_blockers > 0 && <aside className="rounded-lg border bg-muted p-4"><Eyebrow>Blocked</Eyebrow><p className="mt-1 text-sm">Waiting for:</p><IssueLinks links={issue.blocked_by.filter((x) => x.open)} /></aside>}
    {issue.claim !== null && <aside className="rounded-lg border bg-muted p-4"><Eyebrow>In progress</Eyebrow><p className="mt-1 text-sm"><strong>{issue.claim.holder.name}</strong> claimed this {relativeTime(issue.claim.since)}.</p></aside>}
  </div>;
}

function Relationships({ issue }: { issue: Issue }) {
  const rows: Array<[string, Array<{ key: string | null; title?: string | null; open: boolean }>]> = [];
  if (issue.parent) rows.push(["Parent", [{ ...issue.parent, open: true }]]);
  if (issue.sub_issues.length) rows.push(["Sub-issues", issue.sub_issues.map((x) => ({ ...x, open: true }))]);
  if (issue.blocked_by.length) rows.push(["Blocked by", issue.blocked_by]);
  if (issue.blocks.length) rows.push(["Blocks", issue.blocks]);
  return rows.length ? <Section title="Relationships"><div className="space-y-3 text-sm">{rows.map(([name, links]) => <div key={name}><h3 className="font-medium">{name}</h3><IssueLinks links={links} /></div>)}</div></Section> : null;
}

function IssueLinks({ links }: { links: Array<{ key: string | null; title?: string | null; open: boolean }> }) {
  return <ul className="mt-2 space-y-1">{links.map((x, i) => <li key={x.key ?? i}>{x.key === null ? <span className="text-muted-foreground">Issue outside your project access</span> : <Link className="text-brand hover:underline" to={keyPath(x.key)}><span className="font-mono text-xs">{x.key}</span>{x.title ? ` · ${x.title}` : ""}</Link>}{!x.open && <span className="text-muted-foreground"> · closed</span>}</li>)}</ul>;
}

function Conversation({ issue }: { issue: Issue }) {
  const entries = [...issue.questions.map((value) => ({ kind: "question" as const, at: value.asked_at, value })), ...issue.comments.map((value) => ({ kind: "comment" as const, at: value.created_at, value }))].sort((a, b) => a.at.localeCompare(b.at));
  return entries.length ? <Section title="Conversation"><div className="space-y-5">{entries.map((entry) => entry.kind === "comment" ? <article key={entry.value.id}><Byline name={entry.value.author.name} at={entry.value.created_at} /><Markdown className="mt-1">{entry.value.body}</Markdown></article> : <article key={entry.value.id}><Eyebrow>{entry.value.answer === null ? "Open question" : "Question"}</Eyebrow><Markdown className="mt-1">{entry.value.question}</Markdown><Byline name={entry.value.asked_by.name} at={entry.value.asked_at} />{entry.value.answer !== null && <div className="mt-3 border-l-2 pl-3"><Markdown>{entry.value.answer}</Markdown><Byline name={entry.value.answered_by?.name ?? "Unknown"} at={entry.value.answered_at!} /></div>}</article>)}</div></Section> : null;
}

function Actions({ issue, onChanged, onDeleted }: { issue: Issue; onChanged: (issue: Issue) => void; onDeleted: () => void }) {
  return <Section title="Actions"><div className="grid gap-5">
    <div className="flex flex-wrap gap-2">
      {issue.claim === null && !["review", "done", "canceled"].includes(issue.status) && <IssueAction label="Claim" path="/issues/{key}/claim" issue={issue} body={{ force: false }} onChanged={onChanged} />}
      {issue.claim !== null && <IssueAction label="Release claim" variant="outline" path="/issues/{key}/release" issue={issue} onChanged={onChanged} />}
      {!['done','canceled','review'].includes(issue.status) && <><IssueAction label="Close as done" variant="outline" path="/issues/{key}/close" issue={issue} body={{ status: "done", result: issue.result }} onChanged={onChanged} /><IssueAction label="Hand in for review" variant="outline" path="/issues/{key}/review" issue={issue} body={{ result: issue.result }} onChanged={onChanged} /></>}
      {['done','canceled'].includes(issue.status) && <IssueAction label="Reopen" path="/issues/{key}/reopen" issue={issue} body={{ comment: null }} onChanged={onChanged} />}
      <IssueAction label={issue.ready ? "Clear ready" : "Set ready"} variant="outline" path="patch" issue={issue} body={{ ready: !issue.ready }} onChanged={onChanged} />
    </div>
    <TextAction label="Add comment" placeholder="Write a comment in Markdown…" multiline onRun={async (body) => { const result = await api.POST("/issues/{key}/comments", { params: { path: { key: issue.key } }, body: { body } }); if (!result.data) throw new Error(describe(result.error, result.response.status)); return { ...issue, comments: [...issue.comments, result.data] }; }} onChanged={onChanged} />
    <TextAction label="Ask question" placeholder="What do you need to know?" multiline onRun={async (question) => { const result = await api.POST("/issues/{key}/questions", { params: { path: { key: issue.key } }, body: { question } }); if (!result.data) throw new Error(describe(result.error, result.response.status)); return { ...issue, questions: [...issue.questions, result.data], open_questions: issue.open_questions + 1 }; }} onChanged={onChanged} />
    <EdgeAction issue={issue} onChanged={onChanged} />
    <div><ActionDialog trigger={<Button variant="destructive">Delete issue</Button>} title={`Delete ${issue.key}?`} description="The issue will be hidden from the project, but can be restored during the grace period." confirmLabel="Delete issue" onConfirm={async () => { const result = await api.DELETE("/issues/{key}", { params: { path: { key: issue.key } } }); if (!result.response.ok) throw new Error(describe(result.error, result.response.status)); onDeleted(); }} /></div>
  </div></Section>;
}

function EdgeAction({ issue, onChanged }: { issue: Issue; onChanged: (issue: Issue) => void }) {
  const [key, setKey] = useState(""); const [error, setError] = useState<string>();
  async function add() { const value = key.trim(); if (!value) return; const result = await api.POST("/issues/{key}/blocked-by/{blockerKey}", { params: { path: { key: issue.key, blockerKey: value } } }); if (!result.response.ok) { setError(describe(result.error, result.response.status)); return; } const read = await api.GET("/issues/{key}", { params: { path: { key: issue.key } } }); if (read.data) { setKey(""); onChanged(read.data); } }
  return <div className="grid gap-2"><label className="text-sm font-medium">Add blocker</label><div className="flex max-w-sm gap-2"><Input aria-label="Blocker issue key" placeholder="PLAN-42" value={key} onChange={(e) => setKey(e.target.value)} /><Button variant="outline" onClick={() => void add()}>Add</Button></div>{error && <p className="text-sm text-destructive">{error}</p>}<div className="flex flex-wrap gap-2">{issue.blocked_by.filter((x) => x.key !== null).map((x) => <Button key={x.key} size="xs" variant="ghost" onClick={async () => { const result = await api.DELETE("/issues/{key}/blocked-by/{blockerKey}", { params: { path: { key: issue.key, blockerKey: x.key! } } }); if (result.response.ok) onChanged({ ...issue, blocked_by: issue.blocked_by.filter((edge) => edge !== x), open_blockers: issue.open_blockers - Number(x.open) }); }}>Remove {x.key}</Button>)}</div></div>;
}

type ActPath = "/issues/{key}/claim" | "/issues/{key}/release" | "/issues/{key}/close" | "/issues/{key}/review" | "/issues/{key}/reopen" | "/issues/{key}/restore";
async function issueRequest(path: ActPath, issue: Issue, body?: object): Promise<Issue> {
  const result = await api.POST(path as "/issues/{key}/claim", { params: { path: { key: issue.key } }, body: body as never });
  if (!result.data) throw new Error(describe(result.error, result.response.status));
  return result.data;
}

function IssueAction({ label, path, issue, body, onChanged, variant = "default" }: { label: string; path: ActPath | "patch"; issue: Issue; body?: object; onChanged: (issue: Issue) => void; variant?: "default" | "outline" }) {
  const [busy, setBusy] = useState(false); const [error, setError] = useState<string>();
  async function run() { setBusy(true); setError(undefined); try { if (path === "patch") { const result = await api.PATCH("/issues/{key}", { params: { path: { key: issue.key } }, headers: { "If-Match": issue.updated_at }, body: body as never }); if (!result.data) throw new Error(describe(result.error, result.response.status)); onChanged(result.data); } else onChanged(await issueRequest(path, issue, body)); } catch (reason) { setError(reason instanceof Error ? reason.message : "The instance did not answer."); } finally { setBusy(false); } }
  return <span><Button variant={variant} disabled={busy} onClick={() => void run()}>{busy ? "Working…" : label}</Button>{error && <span role="alert" className="ml-2 text-xs text-destructive">{error}</span>}</span>;
}

function TextAction({ label, placeholder, multiline, onRun, onChanged }: { label: string; placeholder?: string; multiline?: boolean; onRun: (text: string) => Promise<Issue>; onChanged: (issue: Issue) => void }) {
  const [text, setText] = useState(""); const [busy, setBusy] = useState(false); const [error, setError] = useState<string>();
  async function run() { if (!text.trim()) return; setBusy(true); setError(undefined); try { onChanged(await onRun(text)); setText(""); } catch (reason) { setError(reason instanceof Error ? reason.message : "The instance did not answer."); } finally { setBusy(false); } }
  return <div className="mt-3 grid max-w-xl gap-2">{multiline ? <MarkdownField label={label} value={text} onChange={setText} /> : <label className="grid gap-1 text-sm font-medium">{label}<Input placeholder={placeholder} value={text} onChange={(e) => setText(e.target.value)} /></label>}<div><Button size="sm" disabled={busy || !text.trim()} onClick={() => void run()}>{busy ? "Saving…" : label}</Button></div>{error && <p role="alert" className="text-sm text-destructive">{error}</p>}</div>;
}

function History({ loaded }: { loaded: Load<HistoryEntry[]> }) {
  return <Section title="History">{loaded.at === "asking" ? <Skeleton className="h-12 w-full" /> : loaded.at === "failed" ? <p className="text-sm text-destructive">{loaded.why}</p> : <ol className="space-y-3 text-sm">{loaded.value.map((x) => <li key={x.id}><span className="font-medium">{x.actor.name}</span> {historyText(x)}<Byline at={x.at} /></li>)}</ol>}</Section>;
}

function historyText(x: HistoryEntry) {
  if (x.field === "created") return "created the issue";
  const from = value(x.old_value), to = value(x.new_value);
  if (from === null) return <>set <strong>{x.field}</strong> to {to}</>;
  if (to === null) return <>cleared <strong>{x.field}</strong> from {from}</>;
  return <>changed <strong>{x.field}</strong> from {from} to {to}</>;
}
function value(x: unknown) { if (x == null) return null; if (typeof x === "object" && "name" in x && typeof x.name === "string") return x.name; return String(x); }

function Metadata({ issue }: { issue: Issue }) {
  return <aside className="shrink-0 space-y-3 border-t p-4 text-sm md:w-64 md:border-t-0 md:border-l"><Field name="Status"><StatusDot status={issue.status} withLabel /></Field><Field name="Priority"><span className="font-mono text-xs">{priorityLabel(issue.priority)}</span></Field><Field name="Ready">{issue.ready ? "yes" : "no"}</Field>{issue.epic && <Field name="Epic"><Link to={keyPath(issue.epic.key)} className="text-brand hover:underline">{issue.epic.key}</Link> <span className="text-muted-foreground">{issue.epic.title}</span></Field>}{issue.claim && <Field name="Claimed by">{issue.claim.holder.name}<span className="text-muted-foreground">{issue.claim.expires_at === null ? " · does not expire" : ` · until ${date(issue.claim.expires_at)}`}</span></Field>}{issue.assignee && <Field name="Assignee">{issue.assignee.name}</Field>}{issue.labels.length > 0 && <Field name="Labels"><span className="flex flex-wrap gap-1">{issue.labels.map((x) => <Badge key={x.name} variant="secondary" className="font-normal">{x.name}</Badge>)}</span></Field>}<Field name="Author">{issue.author.name}</Field><Field name="Created">{date(issue.created_at)}</Field><Field name="Updated">{date(issue.updated_at)}</Field>{issue.release && <Field name="Release">{issue.release}</Field>}</aside>;
}

function Section({ title, children }: { title: string; children: React.ReactNode }) { return <section className="border-t py-5 first:border-t-0 first:pt-0"><h2 className="mb-3 text-xs font-medium tracking-wide text-muted-foreground uppercase">{title}</h2>{children}</section>; }
function Field({ name, children }: { name: string; children: React.ReactNode }) { return <div><div className="text-[11px] font-medium tracking-wide text-muted-foreground uppercase">{name}</div><div className="mt-0.5">{children}</div></div>; }
function Eyebrow({ children }: { children: React.ReactNode }) { return <h2 className="text-xs font-semibold tracking-wide uppercase">{children}</h2>; }
function Byline({ name, at }: { name?: string; at: string }) { return <p className="mt-1 text-xs text-muted-foreground">{name && <>{name} · </>}<time dateTime={at}>{date(at)}</time></p>; }
function date(x: string) { return new Date(x).toLocaleString(); }
function relativeTime(x: string) { const hours = Math.max(0, Math.floor((Date.now() - new Date(x).getTime()) / 3_600_000)); return hours < 24 ? `${hours} hour${hours === 1 ? "" : "s"} ago` : `${Math.floor(hours / 24)} days ago`; }
function isLong(x: string) { return x.length > 600 || x.split("\n").length > 8; }
