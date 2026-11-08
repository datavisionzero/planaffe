import { useContext, useState } from "react";
import { api, describe } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog, DialogContent, DialogTitle, DialogDescription, DialogTrigger } from "@/components/ui/dialog";
import { ActionDialog } from "@/shared/ActionDialog";
import { ProjectsContext } from "./context";
import type { ProjectGroup } from "./useProjectGroups";
export function GroupManager({ groups, reload }: { groups: ProjectGroup[]; reload: () => Promise<void> }) {
  const projects = useContext(ProjectsContext);
  const refresh = async () => { await reload(); await projects?.reload(); };
  const [editing, setEditing] = useState<ProjectGroup>();
  const [notice, setNotice] = useState("");
  const [busy, setBusy] = useState(false);
  const [version, setVersion] = useState(0);
  return <Dialog><DialogTrigger render={<Button variant="outline" size="sm" />}>Manage groups</DialogTrigger>
    <DialogContent className="max-h-[85dvh] overflow-auto sm:max-w-lg">
      <DialogTitle>Project groups</DialogTitle><DialogDescription>Organize projects by customer, product or area. Access stays with each project.</DialogDescription>
      <div className="space-y-2">{groups.map(g => <div key={g.id} className="flex items-center gap-2 rounded-lg border p-3"><div className="min-w-0 flex-1"><p className="truncate font-medium">{g.name}</p><p className="text-xs text-muted-foreground">{g.description}</p></div><Button variant="ghost" size="sm" onClick={() => { setEditing(g); setVersion(v => v + 1); }}>Edit</Button><ActionDialog trigger={<Button variant="ghost" size="sm">Dissolve</Button>} title={`Dissolve ${g.name}?`} description="Projects and their content stay intact. They will appear under Ungrouped." confirmLabel="Dissolve group" onConfirm={async () => {
        const r = await api.DELETE("/project-groups/{id}", { params: { path: { id: g.id } } });
        if (!r.response.ok) throw new Error(describe(r.error, r.response.status));
        setEditing(undefined); setVersion(v => v + 1); await refresh();
      }} /></div>)}</div>
      <form key={version} className="grid gap-3 border-t pt-4" onSubmit={async e => {
        e.preventDefault(); const data = new FormData(e.currentTarget); setBusy(true); setNotice("");
        const body = { name: String(data.get("name")), description: String(data.get("description")) || null };
        try {
          const r = editing ? await api.PUT("/project-groups/{id}", { params: { path: { id: editing.id } }, body }) : await api.POST("/project-groups", { body });
          if (!r.response.ok) throw new Error(describe(r.error, r.response.status));
          setEditing(undefined); setVersion(v => v + 1); await refresh(); setNotice("Saved.");
        } catch (error) { setNotice(error instanceof Error ? error.message : "Could not save the group."); } finally { setBusy(false); }
      }}>
        <p className="font-medium">{editing ? "Edit group" : "New group"}</p>
        <label>Name<Input name="name" required maxLength={100} defaultValue={editing?.name} /></label>
        <label>Description <span className="text-muted-foreground">(optional)</span><Input name="description" maxLength={500} defaultValue={editing?.description ?? ""} /></label>
        <div className="flex gap-2"><Button disabled={busy}>{busy ? "Saving…" : editing ? "Save changes" : "Create group"}</Button>{editing && <Button type="button" variant="ghost" onClick={() => { setEditing(undefined); setVersion(v => v + 1); }}>Cancel</Button>}</div>
        {notice && <p role="status" className="text-sm">{notice}</p>}
      </form>
    </DialogContent>
  </Dialog>;
}
