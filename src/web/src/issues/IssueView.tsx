import { CopyIcon, LinkIcon } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { Link, useLocation, useParams } from "react-router";
import { api, codeOf, describe, type HistoryEntry, type Issue, type Problem } from "@/api/client";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { celebrateIfCleared } from "@/projects/celebrate";
import { Tabs, TabsList, TabsPanel, TabsTab } from "@/components/ui/tabs";
import { cn } from "@/lib/utils";
import { useIsMobile } from "@/hooks/use-mobile";
import { useAct } from "@/shared/act";
import { Markdown } from "@/shared/Markdown";
import { PageHeader } from "@/shared/PageHeader";
import { keyPath, pathKey } from "@/shell/views";
import { useAttention } from "@/shell/useAttention";
import { PriorityMark } from "./priority";
import { StatusDot } from "./status";
import { DraftGuard } from "@/shared/abandon";
import { ActionBar, IssueAction } from "./ActionBar";
import { issueRequest, reread } from "./acts";
import { Conversation } from "./Conversation";
import { EditIssueForm } from "./IssueEditor";
import { IssueWorkability } from "./IssueWorkability";
import { Metadata } from "./Metadata";
import { NeedsYouFlow } from "./NeedsYouFlow";
import { Byline, Eyebrow } from "./parts";
import { IssuePicker } from "./pickers";
import { TextAction } from "./TextAction";

type Load<T> = { at: "asking" } | { at: "failed"; why: string } | { at: "known"; value: T };
/** The issue alone can also be gone: deleted, and restorable until a moment. */
type IssueLoad = Load<Issue> | { at: "gone"; until: string | null };
const asking = { at: "asking" } as const;

/**
 * An issue in its grace period is `404 deleted` and not simply missing
 * (`docs/api.md`): the screen that says so is the only way back to it, and
 * `restorable_until` is how long that way stays open. The extension member is
 * read by name — the generated type knows only RFC 9457's own five.
 */
function refused(problem: Problem | undefined, status: number): IssueLoad {
  if (status === 404 && codeOf(problem) === "deleted") {
    const until = (problem as { restorable_until?: string } | undefined)?.restorable_until;
    return { at: "gone", until: until ?? null };
  }

  return { at: "failed", why: describe(problem, status) };
}

/**
 * The complete issue, weighted by what a human does with it: the action bar
 * rides along in the header, what needs attention and the description stand
 * open above the fold, and the three long lists — conversation, relationships,
 * history — share one tabbed area at the bottom instead of stacking.
 */
export function IssueView() {
  const { project, number } = useParams();
  const key = pathKey(project!, number!);
  return <IssueContent key={key} issueKey={key} />;
}

function IssueContent({ issueKey: key }: { issueKey: string }) {
  const location = useLocation();
  const narrow = useIsMobile();
  const fromNeedsYou = new URLSearchParams(location.search).get("from") === "needs-you";
  const { issuesPulse, pulse } = useAttention();
  const [state, setState] = useState<{ key: string; issue: IssueLoad; history: Load<HistoryEntry[]> }>();
  const [editing, setEditing] = useState(false);
  const [copyFeedback, setCopyFeedback] = useState<{ text: string; failed: boolean }>();
  const [dirty, setDirty] = useState(false);
  const [external, setExternal] = useState<Issue>();
  const [refreshError, setRefreshError] = useState("");
  const [refreshRevision, setRefreshRevision] = useState(0);
  const [historyRevision, setHistoryRevision] = useState(0);
  const [contentRevision, setContentRevision] = useState(0);
  const [flowRevision, setFlowRevision] = useState(0);
  const [deleted, setDeleted] = useState<{ until: string | null }>();
  const current = state !== undefined && state.key === key ? state : { key, issue: asking, history: asking };
  const stateRef = useRef(state);
  const editingRef = useRef(editing);
  const dirtyRef = useRef(dirty);
  useEffect(() => { stateRef.current = state; editingRef.current = editing; dirtyRef.current = dirty; }, [state, editing, dirty]);

  useEffect(() => {
    let live = true;
    const stop = new AbortController();
    void api.GET("/issues/{key}", { params: { path: { key } }, signal: stop.signal }).then(({ data, error, response }) => {
      if (!live) return;
      const was = stateRef.current;
      if (data && was?.key === key && was.issue.at === "known") {
        if (data.updated_at < was.issue.value.updated_at) return;
        if (data.updated_at !== was.issue.value.updated_at && (editingRef.current || dirtyRef.current)) {
          setExternal(data);
          setRefreshError("");
          return;
        }
      }
      if (!data && was?.key === key && was.issue.at === "known") {
        setRefreshError(describe(error, response.status));
        return;
      }
      setRefreshError("");
      setExternal(undefined);
      setState((old) => ({ key, history: old !== undefined && old.key === key ? old.history : asking, issue: data ? { at: "known", value: data } : refused(error, response.status) }));
    }, () => {
      if (!live || stop.signal.aborted) return;
      if (stateRef.current?.key === key && stateRef.current.issue.at === "known") setRefreshError("The instance did not answer.");
      else setState((old) => ({ key, history: old?.history ?? asking, issue: { at: "failed", why: "The instance did not answer." } }));
    });
    return () => { live = false; stop.abort(); };
  }, [key, issuesPulse, refreshRevision]);

  useEffect(() => {
    let live = true;
    const stop = new AbortController();
    void api.GET("/issues/{key}/history", { params: { path: { key } }, signal: stop.signal }).then(
      ({ data, error, response }) => live && setState((old) => ({
        key,
        issue: old !== undefined && old.key === key ? old.issue : asking,
        history: data ? { at: "known", value: data } : { at: "failed", why: describe(error, response.status) },
      })),
      () => live && !stop.signal.aborted && setState((old) => ({ key, issue: old?.issue ?? asking, history: { at: "failed", why: "The instance did not answer." } })),
    );
    return () => { live = false; stop.abort(); };
  }, [key, issuesPulse, historyRevision]);

  const changed = (value: Issue) => {
    // An act that closed an issue which was open a moment ago may have been
    // the last one open in the project. The screen asks once, and celebrates
    // if it was — where the human is standing, not only on the overview.
    const was = current.issue.at === "known" ? current.issue.value.status : undefined;
    if (was !== undefined && !closed(was) && closed(value.status)) {
      void celebrateIfCleared(value.project);
    }
    setState((old) => ({ key, issue: { at: "known", value }, history: old?.history ?? asking }));
    setExternal(undefined);
    setHistoryRevision((x) => x + 1);
    setFlowRevision((x) => x + 1);
    setEditing(false);
  };
  const useLatest = () => {
    if (external !== undefined) setState((old) => ({ key, issue: { at: "known", value: external }, history: old?.history ?? asking }));
    setExternal(undefined);
    setDirty(false);
    setEditing(false);
    setContentRevision((value) => value + 1);
  };
  const restored = (value: Issue) => { setDeleted(undefined); changed(value); };
  const flow = fromNeedsYou ? <NeedsYouFlow issueKey={key} revision={flowRevision} pulse={pulse} /> : null;

  if (current.issue.at === "asking") return <><PageHeader title={<Skeleton className="h-4 w-64" />} /><div className="space-y-3 p-4"><Skeleton className="h-3 w-full" /><Skeleton className="h-3 w-5/6" /></div></>;
  // Deleted just now, or deleted long before this browser asked for it: the
  // same screen either way, and the deadline whenever the instance named one.
  if (deleted !== undefined) return <>{flow}<Gone issueKey={key} until={deleted.until} onRestored={restored} /></>;
  if (current.issue.at === "gone") return <>{flow}<Gone issueKey={key} until={current.issue.until} onRestored={restored} /></>;
  if (current.issue.at === "failed") return <><PageHeader title={key} />{flow}<p className="p-4 text-sm text-destructive">{current.issue.why}</p></>;
  const issue = current.issue.value;
  if (editing) return <><PageHeader title={`Edit ${issue.key}`} />{flow}<EditIssueForm issue={issue} external={external} onSaved={changed} onCancel={useLatest} /></>;

  async function copy(what: "key" | "link") {
    setCopyFeedback(undefined);
    if (!navigator.clipboard?.writeText) {
      setCopyFeedback({ text: "Clipboard is unavailable in this browser.", failed: true });
      return;
    }
    try {
      await navigator.clipboard.writeText(what === "key" ? issue.key : new URL(keyPath(issue.key), window.location.origin).href);
      setCopyFeedback({ text: what === "key" ? "Issue key copied." : "Issue link copied.", failed: false });
    } catch {
      setCopyFeedback({ text: "The browser did not allow copying. Check clipboard permissions and try again.", failed: true });
    }
  }

  const title = <span className="flex items-center gap-2">
    <button type="button" aria-label={`Copy issue key ${issue.key}`} title="Copy issue key" onClick={() => void copy("key")} className="inline-flex shrink-0 items-center gap-1 rounded px-1 font-mono text-xs font-normal text-brand hover:bg-muted focus-visible:outline-2 focus-visible:outline-ring">{issue.key}<CopyIcon className="size-3" aria-hidden /></button>
    {issue.title}
  </span>;

  return <><PageHeader className="sticky top-0 z-20 bg-background" headingLabel={`${issue.key} ${issue.title}`} title={title}>
      <Button size="sm" variant="outline" onClick={() => void copy("link")}><LinkIcon aria-hidden />Copy link</Button>
      <ActionBar issue={issue} onEdit={() => setEditing(true)} onChanged={changed} onDeleted={() => setDeleted({ until: null })} />
    </PageHeader>
    {copyFeedback && <p role={copyFeedback.failed ? "alert" : "status"} className={cn("border-b px-4 py-1 text-xs", copyFeedback.failed ? "text-destructive" : "text-muted-foreground")}>{copyFeedback.text}</p>}
    {flow}
    {refreshError && <div role="alert" className="flex flex-wrap items-center gap-2 border-b px-4 py-2 text-sm text-destructive">Could not refresh: {refreshError}<Button size="sm" variant="outline" onClick={() => setRefreshRevision((x) => x + 1)}>Try again</Button></div>}
    <DraftGuard key={contentRevision} external={external !== undefined} onDirtyChange={setDirty} onUseLatest={useLatest}>
    <div className="flex flex-1 flex-col md:flex-row">
      <main className="min-w-0 flex-1 p-4 md:p-6">
        <Chips issue={issue} />
        <IssueWorkability issue={issue} onChanged={changed} />
        <Attention issue={issue} onChanged={changed} />
        {narrow && <Metadata issue={issue} onChanged={changed} />}
        <Section title="Description"><Long>{issue.description}</Long></Section>
        {issue.result !== null && <Section title="Result"><Long>{issue.result}</Long></Section>}
        <Panels issue={issue} history={current.history} onChanged={changed} />
      </main>
      {!narrow && <Metadata issue={issue} onChanged={changed} />}
    </div>
    </DraftGuard></>;
}

/**
 * A deleted issue, and the way back. The focus lands on Restore, which is what
 * the accessibility floor asks for where the confirmed action removed the
 * control that started it.
 */
function Gone({ issueKey, until, onRestored }: { issueKey: string; until: string | null; onRestored: (issue: Issue) => void }) {
  const restore = useRef<HTMLButtonElement>(null);
  const { busy, error, run: act } = useAct();

  useEffect(() => { restore.current?.focus(); }, []);

  const run = () => act(async () => {
    const result = await api.POST("/issues/{key}/restore", { params: { path: { key: issueKey } } });
    if (!result.data) throw new Error(describe(result.error, result.response.status));
    onRestored(result.data);
  });

  return <><PageHeader title={issueKey} /><div className="m-auto grid max-w-md justify-items-center gap-3 p-8 text-center">
    <p role="status">This issue is deleted and hidden from the project.</p>
    {until !== null && <p className="text-sm text-muted-foreground">It can be restored until {new Date(until).toLocaleString()} — {timeLeft(until)}.</p>}
    <Button ref={restore} disabled={busy} onClick={() => void run()}>{busy ? "Working…" : "Restore issue"}</Button>
    {error !== undefined && <p role="alert" className="text-sm text-destructive">{error}</p>}
  </div></>;
}

/**
 * What the metadata column says, on a phone, where a column at the far end of
 * the page is a column nobody reads: status, priority, `ready` and the epic as
 * one line of chips under the title. The column itself starts at `md`.
 */
function Chips({ issue }: { issue: Issue }) {
  return <div className="mb-5 flex flex-wrap items-center gap-2 md:hidden" aria-label="At a glance">
    <Badge variant="outline" className="font-normal"><StatusDot status={issue.status} withLabel /></Badge>
    <Badge variant="outline" className="font-normal"><PriorityMark priority={issue.priority} withLabel /></Badge>
    <Badge variant={issue.ready ? "secondary" : "outline"} className="font-normal">{issue.ready ? "ready" : "not ready"}</Badge>
    {issue.epic && <Badge variant="outline" className="font-normal"><Link className="text-brand hover:underline" to={keyPath(issue.epic.key)}>{issue.epic.key}</Link></Badge>}
  </div>;
}

function Attention({ issue, onChanged }: { issue: Issue; onChanged: (issue: Issue) => void }) {
  async function answer(question: Issue["questions"][number], text: string) {
    const result = await api.POST("/questions/{id}/answer", { params: { path: { id: question.id } }, body: { answer: text } });
    if (!result.data) throw new Error(describe(result.error, result.response.status));
    return reread(issue.key, { ...issue, questions: issue.questions.map((x) => x.id === question.id ? result.data! : x), open_questions: issue.open_questions - 1 });
  }

  return <div className="mb-6 space-y-3" aria-label="Needs attention">
    {issue.questions.filter((q) => q.answer === null).map((q) => <aside key={q.id} id={`question-${q.id}`} className="rounded-lg border border-brand bg-accent p-4">
      <Eyebrow>Answer needed</Eyebrow>
      <Markdown className="mt-2">{q.question}</Markdown>
      <Byline name={q.asked_by.name} at={q.asked_at} />
      <TextAction draftKey={`issue:${issue.key}:answer:${q.id}`} version={issue.updated_at} label="Answer" onRun={(text) => answer(q, text)} onChanged={onChanged} />
    </aside>)}
    {/* Accepting is the header's primary in this status, so this box carries
        the result and the two decisions that are not it. */}
    {issue.status === "review" && <aside className="rounded-lg border border-brand bg-accent p-4">
      <Eyebrow>Review needed</Eyebrow>
      <p className="mt-1 text-sm">Decide whether this work is done, canceled, or should return to todo.</p>
      {issue.result !== null && <Markdown className="mt-3">{issue.result}</Markdown>}
      <div className="mt-3 flex flex-wrap gap-2"><IssueAction label="Accept as canceled" variant="outline" path="/issues/{key}/close" issue={issue} body={{ status: "canceled", result: issue.result }} onChanged={onChanged} /></div>
      <TextAction draftKey={`issue:${issue.key}:review-return`} version={issue.updated_at} label="Return to todo" placeholder="What needs to change?" onRun={(comment) => issueRequest("/issues/{key}/reopen", issue, { comment })} onChanged={onChanged} />
    </aside>}
    {issue.open_blockers > 0 && <aside id="open-blockers" className="rounded-lg border bg-muted p-4"><Eyebrow>Blocked</Eyebrow><p className="mt-1 text-sm">Waiting for:</p><IssueLinks links={issue.blocked_by.filter((x) => x.open)} /></aside>}
    {issue.claim !== null && <aside className="rounded-lg border bg-muted p-4"><Eyebrow>In progress</Eyebrow><p className="mt-1 text-sm"><strong>{issue.claim.holder.name}</strong> claimed this {relativeTime(issue.claim.since)}.</p></aside>}
  </div>;
}

/**
 * The three lists nobody reads at the same time, in one place: the
 * conversation is what an open issue is usually opened for, so it is the one
 * that stands open. Each tab carries its count, so the tab nobody chose still
 * says whether there is anything behind it.
 */
function Panels({ issue, history, onChanged }: { issue: Issue; history: Load<HistoryEntry[]>; onChanged: (issue: Issue) => void }) {
  const relations = (issue.parent ? 1 : 0) + issue.sub_issues.length + issue.blocked_by.length + issue.blocks.length;
  return <Tabs defaultValue="conversation" className="border-t pt-5">
    <TabsList>
      <TabsTab value="conversation">Conversation <Count of={issue.comments.length + issue.questions.length} /></TabsTab>
      <TabsTab value="relationships">Relationships <Count of={relations} /></TabsTab>
      <TabsTab value="history">History <Count of={history.at === "known" ? history.value.length : undefined} /></TabsTab>
    </TabsList>
    <TabsPanel value="conversation"><Conversation issue={issue} onChanged={onChanged} /></TabsPanel>
    <TabsPanel value="relationships"><Relationships issue={issue} onChanged={onChanged} /></TabsPanel>
    <TabsPanel value="history"><History loaded={history} /></TabsPanel>
  </Tabs>;
}

function Count({ of }: { of: number | undefined }) { return of === undefined ? null : <span className="rounded-full bg-muted px-1.5 text-xs tabular-nums">{of}</span>; }

function Relationships({ issue, onChanged }: { issue: Issue; onChanged: (issue: Issue) => void }) {
  const rows: Array<[string, Array<{ key: string | null; title?: string | null; open: boolean }>]> = [];
  if (issue.parent) rows.push(["Parent", [{ ...issue.parent, open: true }]]);
  if (issue.sub_issues.length) rows.push(["Sub-issues", issue.sub_issues.map((x) => ({ ...x, open: true }))]);
  if (issue.blocked_by.length) rows.push(["Blocked by", issue.blocked_by]);
  if (issue.blocks.length) rows.push(["Blocks", issue.blocks]);
  return <div className="space-y-5 text-sm">
    {rows.length === 0 ? <p className="text-muted-foreground">This issue stands on its own — no parent, no sub-issues, nothing blocking or blocked.</p> : rows.map(([name, links]) => <div key={name}><h3 className="font-medium">{name}</h3><IssueLinks links={links} /></div>)}
    <EdgeAction issue={issue} onChanged={onChanged} />
  </div>;
}

function IssueLinks({ links }: { links: Array<{ key: string | null; title?: string | null; open: boolean }> }) {
  return <ul className="mt-2 space-y-1">{links.map((x, i) => <li key={x.key ?? i}>
    {x.key === null
      ? <span className="text-muted-foreground">Issue outside your project access</span>
      : <Link className="text-brand hover:underline" to={keyPath(x.key)}><span className="font-mono text-xs">{x.key}</span>{x.title ? ` · ${x.title}` : ""}</Link>}
    {!x.open && <span className="text-muted-foreground"> · closed</span>}
  </li>)}</ul>;
}

function EdgeAction({ issue, onChanged }: { issue: Issue; onChanged: (issue: Issue) => void }) {
  const [selected, setSelected] = useState("");
  // One act at a time, adding or removing, and one place the refusal is said:
  // at the picker. `doing` is "add" or the key of the blocker being removed.
  const { doing, error, setError, run } = useAct();
  const busy = doing === "add";
  const removing = doing !== null && doing !== "add" ? doing : undefined;

  async function add() {
    if (!selected || doing !== null) return;
    await run(async () => {
      const result = await api.POST("/issues/{key}/blocked-by/{blockerKey}", { params: { path: { key: issue.key, blockerKey: selected } } });
      if (!result.data) throw new Error(describe(result.error, result.response.status));
      setSelected("");
      onChanged(result.data);
    }, "add");
  }

  async function remove(key: string) {
    if (doing !== null) return;
    await run(async () => {
      const result = await api.DELETE("/issues/{key}/blocked-by/{blockerKey}", { params: { path: { key: issue.key, blockerKey: key } } });
      if (!result.data) throw new Error(describe(result.error, result.response.status));
      onChanged(result.data);
    }, key);
  }

  return <div className="grid gap-2 border-t pt-5">
    <div className="flex max-w-md flex-wrap items-end gap-2">
      <div className="min-w-56 flex-1"><IssuePicker label="Add blocker" project={issue.project} crossProject exclude={[issue.key, ...issue.blocked_by.flatMap((edge) => edge.key ?? [])]} value={selected ? [selected] : []} onChange={(keys) => { setSelected(keys[0] ?? ""); setError(undefined); }} error={error || undefined} /></div>
      <Button variant="outline" disabled={!selected || busy || removing !== undefined} onClick={() => void add()}>{busy ? "Adding…" : "Add"}</Button>
    </div>
    <div className="flex flex-wrap gap-2">{issue.blocked_by.filter((edge) => edge.key !== null).map((edge) => <Button key={edge.key} size="xs" variant="ghost" disabled={busy || removing !== undefined} onClick={() => void remove(edge.key!)}>{removing === edge.key ? "Removing…" : `Remove ${edge.key}`}</Button>)}</div>
  </div>;
}

function History({ loaded }: { loaded: Load<HistoryEntry[]> }) {
  return loaded.at === "asking" ? <Skeleton className="h-12 w-full" /> : loaded.at === "failed" ? <p className="text-sm text-destructive">{loaded.why}</p> : <ol className="space-y-3 text-sm">{loaded.value.map((x) => <li key={x.id}><span className="font-medium">{x.actor.name}</span> {historyText(x)}<Byline at={x.at} /></li>)}</ol>;
}

function historyText(x: HistoryEntry) {
  if (x.field === "created") return "created the issue";
  // A comment entry names the fact and which comment, never the text (ADR
  // 0022): a history that carried what somebody withdrew would keep the one
  // thing the withdrawal was for.
  if (x.field === "comment") return x.note === "withdrawn" ? "took a comment away" : "edited a comment";
  const from = value(x.old_value), to = value(x.new_value);
  if (from === null) return <>set <strong>{x.field}</strong> to {to}</>;
  if (to === null) return <>cleared <strong>{x.field}</strong> from {from}</>;
  return <>changed <strong>{x.field}</strong> from {from} to {to}</>;
}
function value(x: unknown) { if (x == null) return null; if (typeof x === "object" && "name" in x && typeof x.name === "string") return x.name; return String(x); }

/**
 * A body that is allowed to be long. Nothing is folded away — a description is
 * hidden exactly when it has something to say — but a very long one is cut off
 * at a readable height behind a fade until somebody asks for the rest.
 */
function Long({ children }: { children: string }) {
  const [whole, setWhole] = useState(false);
  const long = isLong(children);
  return <>
    <div className={cn("relative", long && !whole && "max-h-72 overflow-hidden")}>
      <Markdown>{children}</Markdown>
      {long && !whole && <div aria-hidden className="pointer-events-none absolute inset-x-0 bottom-0 h-16 bg-linear-to-b from-transparent to-background" />}
    </div>
    {long && <button type="button" className="mt-2 text-sm font-medium text-brand hover:underline" onClick={() => setWhole((x) => !x)}>{whole ? "Show less" : "Show more"}</button>}
  </>;
}

function Section({ title, children }: { title: string; children: React.ReactNode }) { return <section className="border-t py-5 first:border-t-0 first:pt-0"><h2 className="mb-3 text-xs font-medium tracking-wide text-muted-foreground uppercase">{title}</h2>{children}</section>; }
function relativeTime(x: string) { const hours = Math.max(0, Math.floor((Date.now() - new Date(x).getTime()) / 3_600_000)); return hours < 24 ? `${hours} hour${hours === 1 ? "" : "s"} ago` : `${Math.floor(hours / 24)} days ago`; }
/** How much of the grace period is left, in the words the deadline is read in. */
function timeLeft(x: string) { const hours = Math.floor((new Date(x).getTime() - Date.now()) / 3_600_000); if (hours < 1) return "less than an hour left"; return hours < 48 ? `${hours} hour${hours === 1 ? "" : "s"} left` : `${Math.floor(hours / 24)} days left`; }
function isLong(x: string) { return x.length > 600 || x.split("\n").length > 8; }

/** The two statuses that close an issue (`CONTEXT.md`, Closed). */
function closed(status: string) { return status === "done" || status === "canceled"; }
