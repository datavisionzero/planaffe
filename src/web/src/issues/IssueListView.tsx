import { useEffect, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router";
import { api, describe, type IssueSummary } from "@/api/client";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { PageHeader } from "@/shared/PageHeader";
import type { View } from "@/shell/views";
import { priorityLabel } from "./priority";
import { StatusDot } from "./status";

/**
 * The four issue views are one screen over one list (docs/api.md, ADR 0012):
 * the view supplies the default filter, the URL may add `label`, `epic`,
 * `status` and `ready`, and what the URL says is what the list shows. Dense
 * and virtualized is the next ticket; this is the list the shell was built
 * around, so that the frame has something to frame.
 */
type Page =
  | { at: "asking" }
  | { at: "failed"; why: string }
  | { at: "known"; items: IssueSummary[]; total: number };

export function IssueListView({ view }: { view: View }) {
  const { project } = useParams();
  const [search] = useSearchParams();
  const [loaded, setLoaded] = useState<{ of: string; page: Page } | null>(null);

  const status = search.getAll("status").length > 0 ? search.getAll("status") : view.filter?.status;
  const label = search.getAll("label");
  const epic = search.get("epic") ?? undefined;
  const ready = search.has("ready") ? search.get("ready") === "true" : view.filter?.ready;

  const fingerprint = JSON.stringify({ project, status, label, epic, ready });
  const page: Page = loaded?.of === fingerprint ? loaded.page : { at: "asking" };

  useEffect(() => {
    let current = true;
    const setPage = (page: Page) => setLoaded({ of: fingerprint, page });

    void (async () => {
      try {
        const { data, error, response } = await api.GET("/issues", {
          params: {
            query: {
              project,
              status: status as never,
              label,
              epic,
              ready,
              limit: 100,
            },
          },
        });

        if (!current) {
          return;
        }

        setPage(
          data === undefined
            ? { at: "failed", why: describe(error, response.status) }
            : { at: "known", items: data.items, total: data.total },
        );
      } catch {
        if (current) {
          setPage({ at: "failed", why: "The instance did not answer." });
        }
      }
    })();

    return () => {
      current = false;
    };
    // The fingerprint is the dependency; the parts are derived from it.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fingerprint]);

  return (
    <>
      <PageHeader
        title={view.label}
        meta={
          page.at === "known"
            ? `${page.total} ${page.total === 1 ? "issue" : "issues"}`
            : page.at === "asking"
              ? "…"
              : undefined
        }
      />

      {page.at === "asking" && (
        <ul className="divide-y" aria-busy>
          {Array.from({ length: 6 }, (_, i) => (
            <li key={i} className="flex h-9 items-center gap-3 px-4">
              <Skeleton className="h-3 w-16" />
              <Skeleton className="h-3 flex-1" />
            </li>
          ))}
        </ul>
      )}

      {page.at === "failed" && <p className="p-4 text-sm text-destructive">{page.why}</p>}

      {page.at === "known" && page.items.length === 0 && (
        <div className="flex flex-1 flex-col items-center justify-center gap-1 p-8 text-center">
          <p className="text-sm">Nothing here.</p>
          <p className="text-xs text-muted-foreground">{view.hint}</p>
        </div>
      )}

      {page.at === "known" && page.items.length > 0 && (
        <ul className="divide-y">
          {page.items.map((issue) => (
            <li key={issue.key}>
              <Link
                to={`/${project}/issues/${issue.key}`}
                className="flex h-9 items-center gap-3 px-4 hover:bg-accent focus-visible:bg-accent focus-visible:outline-hidden"
              >
                <StatusDot status={issue.status} />
                <span className="w-20 shrink-0 font-mono text-xs text-muted-foreground">{issue.key}</span>
                <span className="min-w-0 flex-1 truncate">{issue.title}</span>
                <span className="hidden items-center gap-1 md:flex">
                  {issue.labels.map((name) => (
                    <Badge key={name} variant="secondary" className="font-normal">
                      {name}
                    </Badge>
                  ))}
                </span>
                {issue.claim !== null && (
                  <span className="hidden truncate text-xs text-muted-foreground sm:inline">{issue.claim.holder.name}</span>
                )}
                <span className="w-6 shrink-0 text-right font-mono text-xs text-muted-foreground">
                  {priorityLabel(issue.priority)}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
