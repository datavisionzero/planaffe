import { MoreHorizontalIcon } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { api, describe, type Issue } from "@/api/client";
import { Button } from "@/components/ui/button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { useSession } from "@/session/useSession";
import { ActionDialog } from "@/shared/ActionDialog";
import { Markdown } from "@/shared/Markdown";
import { reread } from "./acts";
import { Byline, Eyebrow } from "./parts";
import { TextAction } from "./TextAction";

/**
 * The conversation, and the two ways to add to it. The fields open on a
 * button: a comment box that stands open on every issue invites the comment
 * nobody needed, and it sat under the actions rather than under the thread it
 * belongs to.
 */
export function Conversation({ issue, onChanged }: { issue: Issue; onChanged: (issue: Issue) => void }) {
  const [writing, setWriting] = useState<"comment" | "question">();
  const entries = [...issue.questions.map((value) => ({ kind: "question" as const, at: value.asked_at, value })), ...issue.comments.map((value) => ({ kind: "comment" as const, at: value.created_at, value }))].sort((a, b) => a.at.localeCompare(b.at));
  const added = (issue: Issue) => { setWriting(undefined); onChanged(issue); };
  // A deleted comment takes its own menu, and with it the dialog's trigger,
  // away: the focus goes to what the conversation offers next rather than
  // falling to the page (`docs/human-interface.md`, accessibility floor).
  const root = useRef<HTMLDivElement>(null);
  const [refocus, setRefocus] = useState(0);
  useEffect(() => {
    if (refocus === 0) return;
    const next = root.current?.querySelector<HTMLElement>("[data-conversation-next]") ?? root.current;
    next?.focus();
  }, [refocus]);
  const removed = (issue: Issue) => { onChanged(issue); setRefocus((count) => count + 1); };

  async function comment(body: string) {
    const result = await api.POST("/issues/{key}/comments", { params: { path: { key: issue.key } }, body: { body } });
    if (!result.data) throw new Error(describe(result.error, result.response.status));
    return reread(issue.key, { ...issue, comments: [...issue.comments, result.data] });
  }

  async function ask(question: string) {
    const result = await api.POST("/issues/{key}/questions", { params: { path: { key: issue.key } }, body: { question } });
    if (!result.data) throw new Error(describe(result.error, result.response.status));
    return reread(issue.key, { ...issue, questions: [...issue.questions, result.data], open_questions: issue.open_questions + 1 });
  }

  return <div ref={root} tabIndex={-1} className="space-y-5 outline-none">
    {entries.length === 0
      ? <p className="text-sm text-muted-foreground">Nothing has been said on this issue yet.</p>
      : entries.map((entry) => entry.kind === "comment"
        ? <CommentEntry key={entry.value.id} issue={issue} comment={entry.value} onChanged={onChanged} onRemoved={removed} />
        : <article key={entry.value.id}>
            <Eyebrow>{entry.value.answer === null ? "Open question" : "Question"}</Eyebrow>
            <Markdown className="mt-1">{entry.value.question}</Markdown>
            <Byline name={entry.value.asked_by.name} at={entry.value.asked_at} />
            {entry.value.answer !== null && <div className="mt-3 border-l-2 pl-3"><Markdown>{entry.value.answer}</Markdown><Byline name={entry.value.answered_by?.name ?? "Unknown"} at={entry.value.answered_at!} /></div>}
          </article>)}
    {writing === undefined && <div className="flex flex-wrap gap-2">
      <Button data-conversation-next variant="outline" size="sm" onClick={() => setWriting("comment")}>Add comment</Button>
      <Button variant="outline" size="sm" onClick={() => setWriting("question")}>Ask question</Button>
    </div>}
    {writing === "comment" && <TextAction draftKey={`issue:${issue.key}:new-comment`} version={issue.updated_at} label="Add comment" multiline onCancel={() => setWriting(undefined)} onRun={comment} onChanged={added} />}
    {writing === "question" && <TextAction draftKey={`issue:${issue.key}:new-question`} version={issue.updated_at} label="Ask question" multiline onCancel={() => setWriting(undefined)} onRun={ask} onChanged={added} />}
  </div>;
}

/**
 * One comment, with the two things its author can do to it (ADR 0022). The
 * acts sit in the same overflow menu the issue header uses rather than as two
 * text links under every paragraph: a conversation is read, and a row of verbs
 * under each entry is read too.
 *
 * Shown only where they are allowed, because a menu that offers what the
 * instance refuses teaches the reader nothing. Hiding is not the check — the
 * instance makes it, and it is `forbidden` there.
 */
function CommentEntry({ issue, comment, onChanged, onRemoved }: { issue: Issue; comment: Issue["comments"][number]; onChanged: (issue: Issue) => void; onRemoved: (issue: Issue) => void }) {
  const { me } = useSession();
  const [editing, setEditing] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const mine = comment.author.id === me.id;
  // The author rewrites; the author or any user clears up, on anybody's.
  const removable = mine || me.kind === "user";
  const without = (issue: Issue) => ({ ...issue, comments: issue.comments.filter((x) => x.id !== comment.id) });

  async function correct(body: string) {
    const result = await api.PATCH("/comments/{id}", { params: { path: { id: comment.id } }, body: { body } });
    if (!result.data) throw new Error(describe(result.error, result.response.status));
    return reread(issue.key, { ...issue, comments: issue.comments.map((x) => x.id === comment.id ? result.data! : x) });
  }

  async function withdraw() {
    const result = await api.DELETE("/comments/{id}", { params: { path: { id: comment.id } } });
    if (!result.response.ok) throw new Error(describe(result.error, result.response.status));
    onRemoved(await reread(issue.key, without(issue)));
  }

  return <article>
    <div className="flex items-start justify-between gap-2">
      <Byline name={comment.author.name} at={comment.created_at} edited={comment.edited_at} />
      {(mine || removable) && <DropdownMenu>
        <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label={`Actions on the comment by ${comment.author.name}`} />}><MoreHorizontalIcon /></DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          {mine && <DropdownMenuItem onClick={() => setEditing(true)}>Edit comment</DropdownMenuItem>}
          {removable && <DropdownMenuItem variant="destructive" onClick={() => setDeleting(true)}>Delete comment</DropdownMenuItem>}
        </DropdownMenuContent>
      </DropdownMenu>}
    </div>
    {editing
      ? <TextAction draftKey={`issue:${issue.key}:comment:${comment.id}`} version={comment.edited_at ?? comment.created_at} label="Save comment" multiline initial={comment.body} onCancel={() => setEditing(false)} onRun={correct} onChanged={(next) => { setEditing(false); onChanged(next); }} />
      : <Markdown className="mt-1">{comment.body}</Markdown>}
    <ActionDialog open={deleting} onOpenChange={setDeleting} title="Delete this comment?" description="It is gone for good — there is no grace period for a comment. The history keeps that it was taken away." confirmLabel="Delete comment" onConfirm={withdraw} />
  </article>;
}
