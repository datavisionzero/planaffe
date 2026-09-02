import { useEffect, useState } from "react";
import { useParams } from "react-router";
import { api, describe, type Schemas } from "@/api/client";
import { PageHeader } from "@/shared/PageHeader";

type Label = Schemas["Label"];
type Loaded = { at: "asking" } | { at: "failed"; why: string } | { at: "known"; labels: Label[] };

/** The labels of the project, grouped where they exclude one another. */
export function LabelsView() {
  const { project } = useParams();
  const [known, setKnown] = useState<{ of: string | undefined; loaded: Loaded } | null>(null);
  const loaded: Loaded = known !== null && known.of === project ? known.loaded : { at: "asking" };

  useEffect(() => {
    let current = true;
    const setLoaded = (loaded: Loaded) => setKnown({ of: project, loaded });

    void (async () => {
      try {
        const { data, error, response } = await api.GET("/projects/{key}/labels", {
          params: { path: { key: project! } },
        });

        if (current) {
          setLoaded(
            data === undefined ? { at: "failed", why: describe(error, response.status) } : { at: "known", labels: data },
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
  }, [project]);

  const groups =
    loaded.at === "known"
      ? [...new Set(loaded.labels.map((label) => label.group ?? ""))].sort((a, b) =>
          a === "" ? 1 : b === "" ? -1 : a.localeCompare(b),
        )
      : [];

  return (
    <>
      <PageHeader title="Labels" meta={loaded.at === "known" ? `${loaded.labels.length}` : undefined} />
      {loaded.at === "failed" && <p className="p-4 text-sm text-destructive">{loaded.why}</p>}
      {loaded.at === "known" && (
        <div className="space-y-5 p-4">
          {groups.map((group) => (
            <section key={group}>
              <h2 className="mb-1 text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
                {group === "" ? "Ungrouped" : `${group} · one of`}
              </h2>
              <ul className="divide-y rounded-md border">
                {loaded.labels
                  .filter((label) => (label.group ?? "") === group)
                  .map((label) => (
                    <li key={label.name} className="flex h-9 items-center gap-3 px-3">
                      <span className="font-mono text-xs">{label.name}</span>
                      <span className="truncate text-xs text-muted-foreground">{label.description}</span>
                    </li>
                  ))}
              </ul>
            </section>
          ))}
        </div>
      )}
    </>
  );
}
