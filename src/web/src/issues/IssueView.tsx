import { useEffect, useState } from "react";
import { Link, useParams } from "react-router";
import { api, describe, type Issue } from "@/api/client";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Markdown } from "@/shared/Markdown";
import { PageHeader } from "@/shared/PageHeader";
import { priorityLabel } from "./priority";
import { StatusDot } from "./status";

/**
 * The issue: content before metadata (VISION 6.2). The description and the
 * result are Markdown rendered by the pipeline of ADR 0007; the rest is the
 * strip beside it. Comments, questions and history arrive with their ticket.
 */
type Loaded = { at: "asking" } | { at: "failed"; why: string } | { at: "known"; issue: Issue };

export function IssueView() {
  const { project, key } = useParams();
  const [known, setKnown] = useState<{ of: string; loaded: Loaded } | null>(null);
  const loaded: Loaded = known !== null && known.of === key ? known.loaded : { at: "asking" };

  useEffect(() => {
    let current = true;
    const setLoaded = (loaded: Loaded) => setKnown({ of: key!, loaded });

    void (async () => {
      try {
        const { data, error, response } = await api.GET("/issues/{key}", { params: { path: { key: key! } } });

        if (current) {
          setLoaded(
            data === undefined ? { at: "failed", why: describe(error, response.status) } : { at: "known", issue: data },
          );
        }
      } catch {
        if (current) {
          setLoaded({ at: "failed", why: "The instance did not answer." });
        }
      }
    })();

    return () => {
      current = false;
    };
  }, [key]);

  if (loaded.at === "asking") {
    return (
      <>
        <PageHeader title={<Skeleton className="h-4 w-64" />} />
        <div className="space-y-3 p-4">
          <Skeleton className="h-3 w-full" />
          <Skeleton className="h-3 w-5/6" />
          <Skeleton className="h-3 w-2/3" />
        </div>
      </>
    );
  }

  if (loaded.at === "failed") {
    return (
      <>
        <PageHeader title={key} />
        <p className="p-4 text-sm text-destructive">{loaded.why}</p>
      </>
    );
  }

  const { issue } = loaded;

  return (
    <>
      <PageHeader
        title={
          <span className="flex items-center gap-2">
            <span className="font-mono text-xs font-normal text-muted-foreground">{issue.key}</span>
            {issue.title}
          </span>
        }
      />

      <div className="flex flex-1 flex-col md:flex-row">
        <article className="min-w-0 flex-1 p-4 md:p-6">
          <Markdown>{issue.description}</Markdown>

          {issue.result !== null && (
            <section className="mt-6 border-t pt-4">
              <h2 className="mb-2 text-xs font-medium tracking-wide text-muted-foreground uppercase">Result</h2>
              <Markdown>{issue.result}</Markdown>
            </section>
          )}
        </article>

        <aside className="shrink-0 space-y-3 border-t p-4 text-sm md:w-64 md:border-t-0 md:border-l">
          <Field name="Status">
            <StatusDot status={issue.status} withLabel />
          </Field>
          <Field name="Priority">
            <span className="font-mono text-xs">{priorityLabel(issue.priority)}</span>
          </Field>
          <Field name="Ready">{issue.ready ? "yes" : "no"}</Field>
          {issue.epic !== null && (
            <Field name="Epic">
              <Link to={`/${project}/epics/${issue.epic.key}`} className="text-brand hover:underline">
                {issue.epic.key}
              </Link>{" "}
              <span className="text-muted-foreground">{issue.epic.title}</span>
            </Field>
          )}
          {issue.claim !== null && (
            <Field name="Claimed by">
              {issue.claim.holder.name}
              <span className="text-muted-foreground">
                {issue.claim.expires_at === null
                  ? " · does not expire"
                  : ` · until ${new Date(issue.claim.expires_at).toLocaleString()}`}
              </span>
            </Field>
          )}
          {issue.assignee !== null && <Field name="Assignee">{issue.assignee.name}</Field>}
          {issue.labels.length > 0 && (
            <Field name="Labels">
              <span className="flex flex-wrap gap-1">
                {issue.labels.map((label) => (
                  <Badge key={label.name} variant="secondary" className="font-normal">
                    {label.name}
                  </Badge>
                ))}
              </span>
            </Field>
          )}
          {issue.blocked_by.length > 0 && (
            <Field name="Blocked by">
              <span className="flex flex-wrap gap-1 font-mono text-xs">
                {issue.blocked_by.map((blocker) => (
                  <Link key={blocker.key} to={`/${project}/issues/${blocker.key}`} className="text-brand hover:underline">
                    {blocker.key}
                  </Link>
                ))}
              </span>
            </Field>
          )}
          <Field name="Author">{issue.author.name}</Field>
        </aside>
      </div>
    </>
  );
}

function Field({ name, children }: { name: string; children: React.ReactNode }) {
  return (
    <div>
      <div className="text-[11px] font-medium tracking-wide text-muted-foreground uppercase">{name}</div>
      <div className="mt-0.5">{children}</div>
    </div>
  );
}
