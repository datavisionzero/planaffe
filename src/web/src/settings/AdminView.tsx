import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { Link, Route, Routes, useParams } from "react-router";
import { api, describe, type Schemas } from "@/api/client";
import { Button } from "@/components/ui/button";
import { DropdownMenuItem } from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { useSession } from "@/session/useSession";
import { PageHeader } from "@/shared/PageHeader";
import { ActionDialog } from "@/shared/ActionDialog";
import { reporting } from "@/shared/report";
import { date, submitting } from "./forms";
import { Row, RowMenu, Rows, Said, Section, SettingsShell } from "./SettingsShell";

type User = Schemas["UserSummary"];
type AccessLink = Schemas["AccessLink"];
type AdminProject = Schemas["AdminProject"];
type Smtp = Schemas["SmtpStatus"];

/** The instance's own administration, one area per address. */
export function AdminView() {
  const { me } = useSession();

  if (!me.administrator) return <NotYours />;

  return (
    <SettingsShell
      title="Instance administration"
      areas={[
        { to: "users", label: "Users", element: <Users /> },
        { to: "projects", path: "projects/*", label: "Projects", element: <Projects /> },
        { to: "email", label: "Transactional email", element: <Email /> },
      ]}
    />
  );
}

/**
 * What `/admin` answers a reader who does not have the role.
 *
 * It used to be a redirect to the issue list. Whoever opened the address out
 * of a bookmark or out of a colleague's message learned nothing at all: not
 * whether it exists, not whether it moved, not whether it is simply not
 * theirs. `docs/human-interface.md` counts a permission state among the
 * designed ones, and a redirect is the blank page, only faster.
 *
 * That the navigation does not offer the area to a non-administrator stays as
 * it is; the same paragraph foresees the direct call and has the server refuse
 * it with `403`. This check is the courtesy in front of that answer and never
 * the authorization, which stays in the API.
 */
function NotYours() {
  const back = useRef<HTMLAnchorElement>(null);

  useEffect(() => { back.current?.focus(); }, []);

  return <><PageHeader title="Instance administration" /><div className="m-auto grid max-w-md justify-items-center gap-3 p-8 text-center">
    <p role="status">Instance administration belongs to administrators, and this account is not one.</p>
    <Button render={<Link ref={back} to="/" />}>Back to your projects</Button>
  </div></>;
}

function Users() {
  const [users, setUsers] = useState<User[]>([]);
  const [notice, setNotice] = useState("");
  const [issued, setIssued] = useState<AccessLink>();
  async function load() { setUsers((await api.GET("/users")).data ?? []); }
  useEffect(() => { void (async () => { await load(); })(); }, []);
  const report = reporting(setNotice, load);

  // The form is taken before the await: React empties `currentTarget` once
  // the event has been dispatched, and reading it back threw where the list
  // was about to be reloaded, so an invited user did not appear until the
  // page was loaded again.
  async function invite(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const invited = await report(api.POST("/users", { body: { name: String(data.get("name")), email: String(data.get("email")), administrator: data.has("administrator") } }), "Invitation sent.");
    if (invited) form.reset();
  }

  return (
    <Section title="Users" description="Invite users and manage their instance role and lifecycle.">
      <form className="mb-3 grid gap-2 sm:grid-cols-[1fr_1fr_auto]" onSubmit={(e) => void invite(e)}>
        <Input name="name" placeholder="Name" aria-label="Name" />
        <Input name="email" type="email" placeholder="Email" aria-label="Email" />
        <Button type="submit">Invite</Button>
        <label className="flex gap-2 text-sm sm:col-span-3"><input name="administrator" type="checkbox" /> Administrator</label>
      </form>
      {issued && <Handover issued={issued} />}
      <Rows empty="No users.">
        {users.map((u) => <UserRow key={u.id} user={u} report={report} onIssued={setIssued} />)}
      </Rows>
      <Said notice={notice} />
    </Section>
  );
}

/**
 * A link an administrator has just been handed, to carry over themselves.
 *
 * There is no second way into a browser account: a password is recovered
 * through a link in an email, and transactional email is optional (ADR 0018).
 * Whoever forgot their password in an instance without SMTP was locked out for
 * good, and an administrator could do nothing about it. So the same one-time
 * secret the email would have carried is shown here instead and passed on by
 * hand — which is why an administrator never learns anybody's password.
 *
 * The instance answers a whole URL where it has been told its public address
 * and a path where it has not; it never invents a host out of a request
 * header. The browser is standing at that address either way, so it is the one
 * that can complete it.
 */
function Handover({ issued }: { issued: AccessLink }) {
  return (
    <div role="status" className="mb-3 rounded-md border border-brand/40 bg-brand/5 p-3">
      <p className="text-xs text-muted-foreground">Copy this link now — it will not be shown again — and hand it over yourself. It works once, until {date(issued.expires_at)}.</p>
      <code className="break-all text-xs">{new URL(issued.link, window.location.origin).toString()}</code>
    </div>
  );
}

/** A write on this screen, and the one sentence it leaves behind. */
type Report = ReturnType<typeof reporting>;

/**
 * One user and the acts an administrator has on them.
 *
 * Two of the four used to hang straight on the click of a menu entry — one
 * click, no question, done — and "Deactivate" stands directly under
 * "Make admin". A deactivation is the most consequential act this interface
 * knows: it ends every browser session of that person and stops their user
 * tokens and all of their agents in the same minute, so whoever has an agent
 * running next door has just halted it.
 *
 * So those two ask first, and the dialog writes the consequences out instead
 * of asserting them. Reactivating and "Make admin" ask nothing: a question in
 * front of every act is no longer a warning, only a second click.
 *
 * The dialog is opened from a menu, which closes on the click and would take
 * its own trigger down with it, so the row keeps the open state and the dialog
 * stands beside the menu.
 */
function UserRow({ user, report, onIssued }: { user: User; report: Report; onIssued: (issued: AccessLink) => void }) {
  const [asking, setAsking] = useState<"deactivate" | "demote">();
  const deactivated = user.state === "deactivated";

  async function hand(path: "/users/{id}/invitation-link" | "/users/{id}/recovery-link", said: string) {
    const link = await report(api.POST(path, { params: { path: { id: user.id } } }), said);
    if (link) onIssued(link);
  }

  return (
    <Row
      title={user.name}
      detail={`${user.email} · ${user.state}${user.administrator ? " · administrator" : ""}`}
      action={
        <>
          <RowMenu label={`Actions for ${user.name}`}>
            {user.state === "invited" && <DropdownMenuItem onClick={() => void report(api.POST("/users/{id}/invitation", { params: { path: { id: user.id } } }), "Invitation resent.")}>Resend invitation</DropdownMenuItem>}
            {/* The way in that does not go through an email, and it stands here
                whether or not SMTP is configured: an entry that appears and
                disappears with an operating setting explains itself to nobody,
                and a link is worth having beside a mail that is slow, filtered
                or misaddressed. Which of the two a row offers is the user's
                state — an invited user has no password to recover, an active
                one no invitation left to hand over. */}
            {user.state === "invited" && <DropdownMenuItem onClick={() => void hand("/users/{id}/invitation-link", `Invitation link for ${user.name} issued.`)}>Invitation link</DropdownMenuItem>}
            {user.state === "active" && <DropdownMenuItem onClick={() => void hand("/users/{id}/recovery-link", `Password link for ${user.name} issued.`)}>Password link</DropdownMenuItem>}
            {user.administrator
              ? <DropdownMenuItem onClick={() => setAsking("demote")}>Demote</DropdownMenuItem>
              : <DropdownMenuItem onClick={() => void report(api.PATCH("/users/{id}", { params: { path: { id: user.id } }, body: { administrator: true } }), `${user.name} is now an administrator.`)}>Make admin</DropdownMenuItem>}
            {deactivated
              ? <DropdownMenuItem onClick={() => void report(api.POST("/users/{id}/reactivate", { params: { path: { id: user.id } } }), `${user.name} is active again.`)}>Reactivate</DropdownMenuItem>
              : <DropdownMenuItem variant="destructive" onClick={() => setAsking("deactivate")}>Deactivate</DropdownMenuItem>}
          </RowMenu>
          <ActionDialog
            open={asking === "deactivate"}
            onOpenChange={(open) => setAsking(open ? "deactivate" : undefined)}
            title={`Deactivate ${user.name}?`}
            description={`Every browser session of ${user.name} ends, and their user tokens and all of their agents stop authenticating — an agent running right now is halted with them. Nothing they wrote or changed disappears; the history keeps its author. An administrator can reactivate them at any time.`}
            confirmLabel="Deactivate"
            onConfirm={async () => { await report(api.POST("/users/{id}/deactivate", { params: { path: { id: user.id } } }), `${user.name} is deactivated.`); }}
          />
          <ActionDialog
            open={asking === "demote"}
            onOpenChange={(open) => setAsking(open ? "demote" : undefined)}
            title={`Demote ${user.name}?`}
            description={`${user.name} loses the instance role: no more inviting or deactivating users, no project assignments, no SMTP. Their project access, their sessions, their tokens and their agents are untouched, and an administrator can grant the role again.`}
            confirmLabel="Demote"
            confirmVariant="default"
            onConfirm={async () => { await report(api.PATCH("/users/{id}", { params: { path: { id: user.id } }, body: { administrator: false } }), `${user.name} is no longer an administrator.`); }}
          />
        </>
      }
    />
  );
}

/**
 * The projects of the instance, as a list with a detail behind each one. The
 * single box held a form and a button per permitted user for every project at
 * once — it grew with projects times users — and it asked every project for
 * its access list on opening, to show one of them.
 */
function Projects() {
  return (
    <Routes>
      <Route index element={<ProjectList />} />
      <Route path=":key" element={<ProjectAccess />} />
    </Routes>
  );
}

function useAdminProjects(): AdminProject[] {
  const [projects, setProjects] = useState<AdminProject[]>([]);

  useEffect(() => {
    let current = true;
    void (async () => {
      const { data } = await api.GET("/admin/projects", { params: { query: { deleted: "all" } } });
      if (current) setProjects(data ?? []);
    })();
    return () => { current = false; };
  }, []);

  return projects;
}

function ProjectList() {
  const projects = useAdminProjects();

  return (
    <Section title="Projects" description="Every project of the instance, deleted ones included.">
      <Rows empty="No projects.">
        {projects.map((p) => (
          <Row
            key={p.key}
            title={<Link className="hover:underline" to={p.key}>{p.key} · {p.name}</Link>}
            detail={p.deleted_at ? `Deleted ${date(p.deleted_at)}` : undefined}
          />
        ))}
      </Rows>
    </Section>
  );
}

function ProjectAccess() {
  const { key } = useParams();
  const [project, setProject] = useState<AdminProject>();
  const [users, setUsers] = useState<User[]>([]);
  const [permitted, setPermitted] = useState<User[]>([]);
  const [notice, setNotice] = useState("");

  const load = useCallback(async () => {
    const [all, everybody, projects] = await Promise.all([
      api.GET("/projects/{key}/users", { params: { path: { key: key! } } }),
      api.GET("/users"),
      api.GET("/admin/projects", { params: { query: { deleted: "all" } } }),
    ]);
    setPermitted(all.data ?? []);
    setUsers(everybody.data ?? []);
    setProject((projects.data ?? []).find((candidate) => candidate.key === key));
  }, [key]);

  useEffect(() => { void (async () => { await load(); })(); }, [load]);
  const report = reporting(setNotice, load);

  // Everyone who does not have access to this project yet. With nobody left
  // the select has no options, contributes no form entry, and the id read
  // back out of it was the string "null".
  const grantable = users.filter((candidate) => !permitted.some((x) => x.id === candidate.id));

  return (
    <Section title={project === undefined ? key! : `${project.key} · ${project.name}`} description="Who may see and write in this project.">
      <p className="mb-3 text-sm"><Link className="text-brand hover:underline" to="..">All projects</Link></p>
      {project?.deleted_at != null ? (
        <>
          <p className="mb-3 text-sm text-muted-foreground">Deleted {date(project.deleted_at)}.</p>
          <Button size="sm" variant="outline" onClick={() => void report(api.POST("/projects/{key}/restore", { params: { path: { key: key! } } }), `${key} restored.`)}>Restore</Button>
        </>
      ) : (
        <>
          <form className="mb-3 flex gap-2" onSubmit={(e) => void submitting(e, setNotice, async (data) => { if (grantable.length === 0) return; const id = String(data.get("user")); const r = await api.PUT("/projects/{key}/users/{id}", { params: { path: { key: key!, id } } }); if (!r.response.ok) throw new Error(describe(r.error, r.response.status)); await load(); })}>
            <select name="user" aria-label={`User for ${key}`} className="h-8 flex-1 rounded-md border bg-background px-2 text-sm">
              {grantable.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
            </select>
            <Button type="submit" size="sm" variant="outline" disabled={grantable.length === 0}>Grant access</Button>
          </form>
          <Rows empty="Nobody has access.">
            {permitted.map((u) => (
              <Row
                key={u.id}
                title={u.name}
                detail={`${u.email} · ${u.state}${u.administrator ? " · administrator" : ""}`}
                action={
                  <RowMenu label={`Actions for ${u.name}`}>
                    <DropdownMenuItem onClick={() => void report(api.DELETE("/projects/{key}/users/{id}", { params: { path: { key: key!, id: u.id } } }), `${u.name} no longer has access to ${key}.`)}>Remove access</DropdownMenuItem>
                  </RowMenu>
                }
              />
            ))}
          </Rows>
        </>
      )}
      <Said notice={notice} />
    </Section>
  );
}

function Email() {
  const [smtp, setSmtp] = useState<Smtp>();
  const [notice, setNotice] = useState("");
  useEffect(() => { void (async () => { setSmtp((await api.GET("/admin/smtp")).data); })(); }, []);

  return (
    <Section title="Transactional email" description="Credentials remain in environment variables.">
      {smtp && <p className="mb-3 text-sm">{smtp.configured ? `${smtp.host}:${smtp.port} · ${smtp.security} · ${smtp.sender}` : "Not configured"}</p>}
      <form className="flex max-w-lg gap-2" onSubmit={(e) => { e.preventDefault(); const email = String(new FormData(e.currentTarget).get("email")); void sendTest(email, setNotice); }}>
        <Input name="email" type="email" placeholder="Test recipient" aria-label="Test recipient" />
        <Button type="submit" disabled={!smtp?.configured}>Send test</Button>
      </form>
      <Said notice={notice} />
    </Section>
  );
}

/**
 * The only write on this screen that changes nothing, so it reports what the
 * instance answered without reloading anything behind it.
 */
async function sendTest(email: string, setNotice: (notice: string) => void) {
  try {
    const { error, response } = await api.POST("/admin/smtp/test", { body: { email } });
    setNotice(response.ok ? "Test email sent." : describe(error, response.status));
  } catch {
    setNotice("The instance did not answer.");
  }
}
