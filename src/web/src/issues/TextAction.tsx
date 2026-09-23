import { useId, useState } from "react";
import type { Issue } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useAct } from "@/shared/act";
import { ActionDialog } from "@/shared/ActionDialog";
import { MarkdownField } from "@/shared/MarkdownField";
import { useDraft } from "@/shared/useDraft";

/**
 * A text an act on the issue carries — an answer, a comment, a question, the
 * reason a review goes back — in a field that keeps its draft and asks before
 * throwing what was written away.
 */
export function TextAction({ draftKey, version, label, placeholder, multiline, initial, onRun, onChanged, onCancel }: { draftKey: string; version: string; label: string; placeholder?: string; multiline?: boolean; initial?: string; onRun: (text: string) => Promise<Issue>; onChanged: (issue: Issue) => void; onCancel?: () => void }) {
  // A correction opens in the text it is correcting, in the same field it was
  // written in — not in an empty box that makes the author type it again.
  const { value: text, setValue: setText, clear, recovery } = useDraft(draftKey, initial ?? "", version);
  const { busy, error, run: act } = useAct();
  const [discarding, setDiscarding] = useState(false);
  const id = useId();
  async function run() { if (!text.trim()) return; await act(async () => { const next = await onRun(text); clear(); onChanged(next); setText(""); }); }
  const cancel = () => { if (text !== (initial ?? "")) setDiscarding(true); else onCancel?.(); };
  return <div className="mt-3 grid max-w-xl gap-2">
    {recovery}
    {multiline
      ? <MarkdownField label={label} value={text} onChange={setText} size="compact" hint={placeholder} onSubmit={() => void run()} />
      : <label className="grid gap-1 text-sm font-medium">{label}<Input id={id} placeholder={placeholder} value={text} onChange={(e) => setText(e.target.value)} /></label>}
    <div className="flex gap-2">
      <Button size="sm" disabled={busy || !text.trim()} onClick={() => void run()}>{busy ? "Saving…" : label}</Button>
      {onCancel && <Button size="sm" variant="ghost" disabled={busy} onClick={cancel}>Cancel</Button>}
    </div>
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    <ActionDialog open={discarding} onOpenChange={setDiscarding} title="Discard what you wrote?" description="Your changes have not been saved." confirmLabel="Discard" onConfirm={async () => { clear(); onCancel?.(); }} />
  </div>;
}
