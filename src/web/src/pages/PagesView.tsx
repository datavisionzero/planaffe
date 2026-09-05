import { useEffect, useState } from "react";
import { Link, useParams } from "react-router";
import { api, describe, type Schemas } from "@/api/client";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { PageHeader } from "@/shared/PageHeader";
import { pagePath } from "@/shell/views";

type PageSummary = Schemas["PageSummary"];
type Loaded = { at: "asking" } | { at: "failed"; why: string } | { at: "known"; items: PageSummary[] };

/**
 * The project's wiki, flat and by slug — no tree, no table of contents. What
 * replaces the navigation a hierarchy would give is the search, which the
 * command palette already reaches (VISION 7).
 */
export function PagesView() {
  const { project } = useParams();
  const [known, setKnown] = useState<{ of: string | undefined; loaded: Loaded } | null>(null);
  const loaded: Loaded = known !== null && known.of === project ? known.loaded : { at: "asking" };

  useEffect(() => {
    let current = true;
    const setLoaded = (loaded: Loaded) => setKnown({ of: project, loaded });

    void (async () => {
      try {
        const { data, error, response } = await api.GET("/projects/{key}/pages", {
          params: { path: { key: project! } },
        });

        if (current) {
          setLoaded(data === undefined ? { at: "failed", why: describe(error, response.status) } : { at: "known", items: data });
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
  }, [project]);

  return (
    <>
      <PageHeader title="Pages" meta={loaded.at === "known" ? `${loaded.items.length}` : undefined}>
        <Button size="sm" render={<Link to={`/${project}/pages/new`} />}>New page</Button>
      </PageHeader>
      {loaded.at === "failed" && <p className="p-4 text-sm text-destructive">{loaded.why}</p>}
      {loaded.at === "known" && loaded.items.length === 0 && (
        <div className="flex flex-1 flex-col items-center justify-center gap-2 p-8 text-center">
          <p className="font-medium">No pages yet.</p>
          <p className="max-w-md text-sm text-muted-foreground">
            A page is what a project knows and no ticket asks for: the architecture, the conventions, what an
            operator has to know — and the plan tickets are cut from later.
          </p>
        </div>
      )}
      {loaded.at === "known" && loaded.items.length > 0 && (
        <ul className="divide-y">
          {loaded.items.map((page) => (
            <li key={page.slug}>
              <Link to={pagePath(project!, page.slug)} className="flex min-h-10 items-center gap-3 px-4 py-1 hover:bg-accent">
                <span className="w-40 shrink-0 truncate font-mono text-xs text-muted-foreground">{page.slug}</span>
                <span className="min-w-0 flex-1 truncate">{page.title}</span>
                <span className="hidden flex-wrap gap-1 sm:flex">
                  {page.labels.map((label) => (
                    <Badge key={label} variant="secondary" className="font-normal">{label}</Badge>
                  ))}
                </span>
                <span className="hidden w-44 shrink-0 truncate text-right text-xs text-muted-foreground md:block">
                  {new Date(page.updated_at).toLocaleDateString()} · {page.updated_by.name}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
