import { MoreHorizontalIcon } from "lucide-react";
import { useState } from "react";
import { api, describe, type Issue } from "@/api/client";
import { Button } from "@/components/ui/button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { useAct } from "@/shared/act";
import { ActionDialog } from "@/shared/ActionDialog";
import { stale } from "@/shared/stale";
import { issueRequest, type ActPath } from "./acts";

/**
 * The header's action bar: the one move this status makes sense of, `Edit`
 * beside it, everything else behind the overflow — including the delete, whose
 * dialog is held here rather than under the menu item that opens it, because
 * the menu closes on that click and would take its own trigger down with it.
 */
export function ActionBar({ issue, onEdit, onChanged, onDeleted }: { issue: Issue; onEdit: () => void; onChanged: (issue: Issue) => void; onDeleted: () => void }) {
  const { busy, error, run: act } = useAct();
  const [deleting, setDeleting] = useState(false);
  const open = !["review", "done", "canceled"].includes(issue.status);

  const run = (make: () => Promise<Issue>) => act(async () => onChanged(await make()));

  const close = (status: "done" | "canceled") => () => run(() => issueRequest("/issues/{key}/close", issue, { status, result: issue.result }));
  const hand = () => run(() => issueRequest("/issues/{key}/review", issue, { result: issue.result }));
  // Nothing is typed here, so a stale refusal has nothing to merge — but it
  // still may not be a dead end. The screen takes the version the refusal
  // carried, says what happened, and the same press writes against that one.
  const ready = () => run(async () => {
    const result = await api.PATCH("/issues/{key}", { params: { path: { key: issue.key } }, headers: { "If-Match": issue.updated_at }, body: { ready: !issue.ready } as never });
    const current = stale<Issue>(result);
    if (current !== undefined) { onChanged(current); throw new Error(`${issue.key} changed while it was open. It is shown as it is now; set it again to write against that version.`); }
    if (!result.data) throw new Error(describe(result.error, result.response.status));
    return result.data;
  });
  // "Moving a ticket by hand still works — a ticket that has not shipped yet
  // simply does not belong" (VISION 7). The act is the release's; the answer is
  // the release, so the issue is read again.
  const shipped = issue.release !== null && issue.release !== "unreleased";
  const moveRelease = (into: boolean) => run(async () => {
    const path = { key: issue.project, name: "unreleased", issue: issue.key };
    const result = into
      ? await api.PUT("/projects/{key}/releases/{name}/issues/{issue}", { params: { path } })
      : await api.DELETE("/projects/{key}/releases/{name}/issues/{issue}", { params: { path } });
    if (!result.data) throw new Error(describe(result.error, result.response.status));
    const read = await api.GET("/issues/{key}", { params: { path: { key: issue.key } } });
    if (!read.data) throw new Error(describe(read.error, read.response.status));
    return read.data;
  });

  async function remove() {
    const result = await api.DELETE("/issues/{key}", { params: { path: { key: issue.key } } });
    if (!result.response.ok) throw new Error(describe(result.error, result.response.status));
    onDeleted();
  }

  // One primary per status, and only one: accept what was handed in, hand in
  // what is being worked on, take what is free, reopen what is closed.
  const primary = issue.status === "review" ? { label: "Accept as done", act: close("done") }
    : ["done", "canceled"].includes(issue.status) ? { label: "Reopen", act: () => run(() => issueRequest("/issues/{key}/reopen", issue, { comment: null })) }
    : issue.claim === null ? { label: "Claim", act: () => run(() => issueRequest("/issues/{key}/claim", issue, { force: false })) }
    : { label: "Hand in for review", act: hand };

  return <>
    {error !== undefined && <span role="alert" className="text-xs text-destructive">{error}</span>}
    <Button size="sm" disabled={busy} onClick={() => void primary.act()}>{busy ? "Working…" : primary.label}</Button>
    <Button variant="outline" size="sm" onClick={onEdit}>Edit</Button>
    <DropdownMenu>
      <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label="More actions" />}><MoreHorizontalIcon /></DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="min-w-48">
        {issue.claim !== null && <DropdownMenuItem onClick={() => void run(() => issueRequest("/issues/{key}/release", issue))}>Release claim</DropdownMenuItem>}
        {open && issue.claim === null && <DropdownMenuItem onClick={() => void hand()}>Hand in for review</DropdownMenuItem>}
        {open && <DropdownMenuItem onClick={() => void close("done")()}>Close as done</DropdownMenuItem>}
        {open && <DropdownMenuItem onClick={() => void close("canceled")()}>Close as canceled</DropdownMenuItem>}
        <DropdownMenuItem onClick={() => void ready()}>{issue.ready ? "Clear ready" : "Set ready"}</DropdownMenuItem>
        {!shipped && <DropdownMenuItem onClick={() => void moveRelease(issue.release === null)}>{issue.release === null ? "Put into the open release" : "Take out of the open release"}</DropdownMenuItem>}
        <DropdownMenuSeparator />
        <DropdownMenuItem variant="destructive" onClick={() => setDeleting(true)}>Delete issue</DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
    <ActionDialog open={deleting} onOpenChange={setDeleting} title={`Delete ${issue.key}?`} description="The issue will be hidden from the project, but can be restored during the grace period." confirmLabel="Delete issue" onConfirm={remove} />
  </>;
}

/** One act as a button of its own, where the header's primary is not it. */
export function IssueAction({ label, path, issue, body, onChanged, variant = "default" }: { label: string; path: ActPath; issue: Issue; body?: object; onChanged: (issue: Issue) => void; variant?: "default" | "outline" }) {
  const { busy, error, run } = useAct();
  return <span>
    <Button variant={variant} disabled={busy} onClick={() => void run(async () => onChanged(await issueRequest(path, issue, body)))}>{busy ? "Working…" : label}</Button>
    {error && <span role="alert" className="ml-2 text-xs text-destructive">{error}</span>}
  </span>;
}
