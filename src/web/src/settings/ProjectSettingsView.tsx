import { useEffect, useState, type FormEvent } from "react";
import { useParams } from "react-router";
import { api, describe, type Project, type Schemas } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useSession } from "@/session/useSession";
import { ActionDialog } from "@/shared/ActionDialog";
import { PageHeader } from "@/shared/PageHeader";
import { Row, Section, SettingsLayout } from "./SettingsView";

type User = Schemas["UserSummary"];
export function ProjectSettingsView() {
  const { project: key } = useParams(); const { me } = useSession();
  const [project, setProject] = useState<Project>(), [users, setUsers] = useState<User[]>([]), [notice, setNotice] = useState("");
  useEffect(() => { void (async () => { const [p, u] = await Promise.all([api.GET("/projects/{key}", { params: { path: { key: key! } } }), api.GET("/projects/{key}/users", { params: { path: { key: key! } } })]); setProject(p.data); setUsers(u.data ?? []); })(); }, [key]);
  async function save(event: FormEvent<HTMLFormElement>) { event.preventDefault(); const data = new FormData(event.currentTarget); const r = await api.PATCH("/projects/{key}", { params: { path: { key: key! } }, body: { name: String(data.get("name")), triage_required: data.has("triage"), review_required: data.has("review") } }); setNotice(r.data ? "Saved." : describe(r.error, r.response.status)); if (r.data) setProject(r.data); }
  return <><PageHeader title={`${key} settings`} /><SettingsLayout>
    <Section title="Project" description="The key is permanent; the name and workflow switches can change.">{project && <form className="grid max-w-lg gap-3" onSubmit={(e) => void save(e)}><label className="text-sm">Name<Input name="name" defaultValue={project.name} /></label><label className="flex gap-2 text-sm"><input name="triage" type="checkbox" defaultChecked={project.triage_required} /> Require triage before agents take issues</label><label className="flex gap-2 text-sm"><input name="review" type="checkbox" defaultChecked={project.review_required} /> Require review before issues are done</label><Button type="submit" className="w-fit">Save project</Button></form>}</Section>
    <Section title="Members" description="Project access is managed by administrators."><div className="divide-y rounded-md border">{users.map(u => <Row key={u.id} title={u.name} detail={`${u.email} · ${u.state}${u.administrator ? " · administrator" : ""}`} />)}</div></Section>
    <Section title="Labels" description="Labels are managed in their dedicated section."><a className="text-sm text-brand hover:underline" href={`/${key}/labels`}>Open labels</a></Section>
    {me.administrator && <Section title="Project lifecycle" description="Deletion is reversible during the instance grace period."><ActionDialog trigger={<Button variant="destructive">Delete project</Button>} title={`Delete project ${key}?`} description="The project and its content will disappear, but can be restored during the instance grace period." confirmLabel="Delete project" onConfirm={async () => { const result = await api.DELETE("/projects/{key}", { params: { path: { key: key! } } }); if (!result.response.ok) throw new Error(describe(result.error, result.response.status)); window.location.assign("/"); }} /></Section>}
    {notice && <p role="status" className="text-sm text-muted-foreground">{notice}</p>}
  </SettingsLayout></>;
}
