import type { IssueSummary } from "@/api/client";
import { cn } from "@/lib/utils";

type Status = IssueSummary["status"];

const labels: Record<Status, string> = {
  backlog: "backlog",
  todo: "todo",
  in_progress: "in progress",
  review: "review",
  done: "done",
  canceled: "canceled",
};

const dots: Record<Status, string> = {
  backlog: "border-status-canceled",
  todo: "border-status-todo",
  in_progress: "border-status-progress bg-status-progress",
  review: "border-status-review bg-status-review",
  done: "border-status-done bg-status-done",
  canceled: "border-status-canceled bg-status-canceled",
};

/** The status as a dot and a word, the same everywhere an issue is listed. */
export function StatusDot({ status, withLabel = false }: { status: Status; withLabel?: boolean }) {
  return (
    <span className="inline-flex items-center gap-1.5" title={labels[status]}>
      <span aria-hidden className={cn("size-2.5 rounded-full border-[1.5px]", dots[status])} />
      {withLabel && <span className="text-xs text-muted-foreground">{labels[status]}</span>}
      {!withLabel && <span className="sr-only">{labels[status]}</span>}
    </span>
  );
}
