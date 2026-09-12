import { useId, type ReactNode } from "react";

/**
 * A labelled choice out of a short, known set — the browser's own select, with
 * a label tied to it.
 *
 * It stood in `settings/SettingsShell.tsx` while the two settings areas were
 * the only screens narrowing a list. The knowledge base picks a space and a
 * parent with the same control, and what two areas share belongs where the
 * other shared controls are.
 */
export function Choose({ label, value, onChange, children }: { label: string; value: string; onChange: (value: string) => void; children: ReactNode }) {
  const id = useId();

  return (
    <div className="grid gap-1 text-sm font-medium">
      <label htmlFor={id}>{label}</label>
      <select
        id={id}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="h-9 rounded-md border bg-background px-2 text-sm font-normal"
      >
        {children}
      </select>
    </div>
  );
}
