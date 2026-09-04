import type { ReactNode } from "react";

/** The first line of every screen: what it is, and what it says about itself. */
export function PageHeader({ title, meta, children }: { title: ReactNode; meta?: ReactNode; children?: ReactNode }) {
  return (
    <div className="flex min-h-12 flex-wrap items-center gap-x-3 gap-y-1 border-b px-4 py-2">
      <h1 className="text-sm font-semibold">{title}</h1>
      {meta !== undefined && <span className="text-xs text-muted-foreground">{meta}</span>}
      {children !== undefined && <div className="ml-auto flex items-center gap-2">{children}</div>}
    </div>
  );
}

