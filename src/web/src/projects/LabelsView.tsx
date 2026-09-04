import { useEffect, useState, type FormEvent } from "react";
import { useParams } from "react-router";
import { api, describe, type Schemas } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/shared/PageHeader";

type Label = Schemas["Label"];
type Loaded = { at: "asking" } | { at: "failed"; why: string } | { at: "known"; labels: Label[] };

/** The labels of the project, grouped where they exclude one another. */
export function LabelsView() {
  const { project } = useParams();
  const [known, setKnown] = useState<{ of: string | undefined; loaded: Loaded } | null>(null);
  const loaded: Loaded = known !== null && known.of === project ? known.loaded : { at: "asking" };

  async function reload() {
    const { data, error, response } = await api.GET("/projects/{key}/labels", { params: { path: { key: project! } } });
    setKnown({ of: project, loaded: data ? { at: "known", labels: data } : { at: "failed", why: describe(error, response.status) } });
  }

  async function create(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const data = new FormData(event.currentTarget);
    const result = await api.POST("/projects/{key}/labels", { params: { path: { key: project! } }, body: { name: String(data.get("name")), group: String(data.get("group")) || null, description: String(data.get("description")) || null } });
    if (result.data) { event.currentTarget.reset(); await reload(); }
  }

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
          <form className="grid gap-2 rounded-md border p-3 sm:grid-cols-[1fr_1fr_2fr_auto]" onSubmit={(e) => void create(e)}>
            <Input name="name" placeholder="Label" aria-label="Label name" required />
            <Input name="group" placeholder="Optional group" aria-label="Label group" />
            <Input name="description" placeholder="What this label means" aria-label="Label description" />
            <Button>Create</Button>
          </form>
          {groups.map((group) => (
            <section key={group}>
              <h2 className="mb-1 text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
                {group === "" ? "Ungrouped" : `${group} · one of`}
              </h2>
              <ul className="divide-y rounded-md border">
                {loaded.labels
                  .filter((label) => (label.group ?? "") === group)
                  .map((label) => (
                    <li key={label.name} className="flex min-h-9 items-center gap-3 px-3 py-1">
                      <span className="font-mono text-xs">{label.name}</span>
                      <span className="min-w-0 flex-1 truncate text-xs text-muted-foreground">{label.description}</span>
                      <Button variant="ghost" size="sm" onClick={() => { const description = window.prompt("Description", label.description ?? ""); if (description !== null) void api.PATCH("/projects/{key}/labels/{name}", { params: { path: { key: project!, name: label.name } }, body: { name: null, group: label.group, description } }).then(reload); }}>Edit</Button>
                      <Button variant="ghost" size="sm" onClick={() => { if (window.confirm(`Delete label ${label.name}?`)) void api.DELETE("/projects/{key}/labels/{name}", { params: { path: { key: project!, name: label.name } } }).then(reload); }}>Delete</Button>
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
