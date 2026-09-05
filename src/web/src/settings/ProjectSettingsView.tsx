import { useEffect, useState } from "react";
import { useParams } from "react-router";
import { api, describe, type Project, type Schemas } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useSession } from "@/session/useSession";
import { ActionDialog } from "@/shared/ActionDialog";
import { submitting } from "./forms";
import { Row, Rows, Said, Section, SettingsShell } from "./SettingsShell";

type User = Schemas["UserSummary"];

/**
 * The project's own settings. Labels used to be a section here that was only a
 * link to another screen — a seam that explained nothing but that the split
 * had not been thought through. They are a daily working screen of the
 * project's navigation and not a setting, so the pseudo-section is gone.
 */
export function ProjectSettingsView() {
  const { project: key } = useParams();

  return (
    <SettingsShell
      title={`${key} settings`}
      areas={[
        { to: "general", label: "General", element: <General /> },
        { to: "members", label: "Members", element: <Members /> },
      ]}
    />
  );
}

function General() {
  const { project: key } = useParams();
  const { me } = useSession();
  const [project, setProject] = useState<Project>();
  const [notice, setNotice] = useState("");

  useEffect(() => {
    let current = true;
    void (async () => {
      const { data } = await api.GET("/projects/{key}", { params: { path: { key: key! } } });
      if (current) setProject(data);
    })();
    return () => { current = false; };
  }, [key]);

  return (
    <>
      <Section title="Project" description="The key is permanent; the name and workflow switches can change.">
        {project && (
          <form className="grid max-w-lg gap-3" onSubmit={(e) => void submitting(e, setNotice, async (data) => { const r = await api.PATCH("/projects/{key}", { params: { path: { key: key! } }, body: { name: String(data.get("name")), triage_required: data.has("triage"), review_required: data.has("review") } }); if (!r.data) throw new Error(describe(r.error, r.response.status)); setProject(r.data); })}>
            <label className="text-sm">Name<Input name="name" defaultValue={project.name} /></label>
            <label className="flex gap-2 text-sm"><input name="triage" type="checkbox" defaultChecked={project.triage_required} /> Require triage before agents take issues</label>
            <label className="flex gap-2 text-sm"><input name="review" type="checkbox" defaultChecked={project.review_required} /> Require review before issues are done</label>
            <Button type="submit" className="w-fit">Save project</Button>
          </form>
        )}
        <Said notice={notice} />
      </Section>
      {me.administrator && (
        <Section title="Project lifecycle" description="Deletion is reversible during the instance grace period.">
          <ActionDialog
            trigger={<Button variant="destructive">Delete project</Button>}
            title={`Delete project ${key}?`}
            description="The project and its content will disappear, but can be restored during the instance grace period."
            confirmLabel="Delete project"
            onConfirm={async () => {
              const result = await api.DELETE("/projects/{key}", { params: { path: { key: key! } } });
              if (!result.response.ok) throw new Error(describe(result.error, result.response.status));
              window.location.assign("/");
            }}
          />
        </Section>
      )}
    </>
  );
}

function Members() {
  const { project: key } = useParams();
  const [users, setUsers] = useState<User[]>([]);

  useEffect(() => {
    let current = true;
    void (async () => {
      const { data } = await api.GET("/projects/{key}/users", { params: { path: { key: key! } } });
      if (current) setUsers(data ?? []);
    })();
    return () => { current = false; };
  }, [key]);

  return (
    <Section title="Members" description="Project access is managed by administrators.">
      <Rows empty="Nobody has access.">
        {users.map((u) => <Row key={u.id} title={u.name} detail={`${u.email} · ${u.state}${u.administrator ? " · administrator" : ""}`} />)}
      </Rows>
    </Section>
  );
}
