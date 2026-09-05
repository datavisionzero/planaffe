import { useEffect, useRef, useState, type FormEvent } from "react";
import { useParams } from "react-router";
import { api, describe, type Schemas } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ActionDialog, TextActionDialog } from "@/shared/ActionDialog";
import { PageHeader } from "@/shared/PageHeader";
import { forgetLabels } from "./useLabels";

type Label = Schemas["Label"];
type Loaded = { at: "asking" } | { at: "failed"; why: string } | { at: "known"; labels: Label[] };

/** The labels of the project, grouped where they exclude one another. */
export function LabelsView() {
  const { project } = useParams();
  const [known, setKnown] = useState<{ of: string | undefined; loaded: Loaded } | null>(null);
  const [recentlyDeleted, setRecentlyDeleted] = useState<{ of: string | undefined; label: Label }>();
  const [restoreProblem, setRestoreProblem] = useState<{ of: string | undefined; why: string }>();
  const loaded: Loaded = known !== null && known.of === project ? known.loaded : { at: "asking" };
  const deleted = recentlyDeleted !== undefined && recentlyDeleted.of === project ? recentlyDeleted.label : undefined;
  const restoreError = restoreProblem !== undefined && restoreProblem.of === project ? restoreProblem.why : "";

  // Never throws. Every caller runs it after a write that already succeeded,
  // and a reload that cannot reach the instance would otherwise be reported as
  // the write having failed.
  async function reload() {
    // Every caller reloads after a write, which is also the moment the set the
    // pickers share stopped being what the project has.
    forgetLabels(project);
    try {
      const { data, error, response } = await api.GET("/projects/{key}/labels", { params: { path: { key: project! } } });
      setKnown({ of: project, loaded: data ? { at: "known", labels: data } : { at: "failed", why: describe(error, response.status) } });
    } catch {
      setKnown({ of: project, loaded: { at: "failed", why: "The instance did not answer." } });
    }
  }

  async function create(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const form = event.currentTarget; const data = new FormData(form);
    const result = await api.POST("/projects/{key}/labels", { params: { path: { key: project! } }, body: { name: String(data.get("name")), group: String(data.get("group")) || null, description: String(data.get("description")) || null } });
    if (result.data) { form.reset(); await reload(); }
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

  const restoreRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    restoreRef.current?.focus();
  }, [deleted?.name]);

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
      {/* Outside the loaded list on purpose. A reload that fails after the
          delete succeeded must not take the way back with it: the label is
          gone either way, and the grace period is the only thing that offers
          it again. */}
      {(deleted !== undefined || restoreError !== "") && (
        <div className="space-y-2 px-4 pt-4">
          {deleted && <div className="flex items-center gap-3 rounded-md border border-brand/40 bg-brand/5 p-3 text-sm"><p role="status" className="min-w-0 flex-1">Deleted <span className="font-mono">{deleted.name}</span>.</p><Button ref={restoreRef} type="button" size="sm" variant="outline" onClick={() => void (async () => { setRestoreProblem(undefined); const result = await api.POST("/projects/{key}/labels/{name}/restore", { params: { path: { key: project!, name: deleted.name } } }); if (!result.data) { setRestoreProblem({ of: project, why: describe(result.error, result.response.status) }); return; } setRecentlyDeleted(undefined); await reload(); })()}>Restore {deleted.name}</Button></div>}
          {restoreError && <p role="alert" className="text-sm text-destructive">{restoreError}</p>}
        </div>
      )}
      {loaded.at === "known" && (
        <div className="space-y-5 p-4">
          <form className="grid gap-2 rounded-md border p-3 sm:grid-cols-[1fr_1fr_2fr_auto]" onSubmit={(e) => void create(e)}>
            <Input name="name" placeholder="Label" aria-label="Label name" required />
            <Input name="group" placeholder="Optional group" aria-label="Label group" />
            <Input name="description" placeholder="What this label means" aria-label="Label description" />
            <Button type="submit">Create</Button>
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
                      <TextActionDialog
                        trigger={<Button variant="ghost" size="sm">Edit</Button>}
                        title={`Edit ${label.name}`}
                        description="Change the one-line description shown wherever this label appears."
                        label="Description"
                        initialValue={label.description ?? ""}
                        required={false}
                        submitLabel="Save label"
                        onSubmit={async (description) => {
                          const result = await api.PATCH("/projects/{key}/labels/{name}", { params: { path: { key: project!, name: label.name } }, body: { name: null, group: label.group, description } });
                          if (!result.data) throw new Error(describe(result.error, result.response.status));
                          await reload();
                        }}
                      />
                      <ActionDialog
                        trigger={<Button variant="ghost" size="sm">Delete</Button>}
                        title={`Delete ${label.name}?`}
                        description="The label will disappear from the project, but can be restored during the instance grace period."
                        confirmLabel="Delete label"
                        onConfirm={async () => {
                          const result = await api.DELETE("/projects/{key}/labels/{name}", { params: { path: { key: project!, name: label.name } } });
                          if (!result.response.ok) throw new Error(describe(result.error, result.response.status));
                          setRestoreProblem(undefined);
                          setRecentlyDeleted({ of: project, label });
                          await reload();
                        }}
                      />
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
