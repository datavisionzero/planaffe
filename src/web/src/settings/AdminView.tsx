import { useEffect, useState, type FormEvent } from "react";
import { Navigate } from "react-router";
import { api, describe, type Schemas } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useSession } from "@/session/useSession";
import { PageHeader } from "@/shared/PageHeader";
import { reporting } from "@/shared/report";
import { Row, Section, SettingsLayout } from "./SettingsView";

type User = Schemas["UserSummary"]; type AdminProject = Schemas["AdminProject"]; type Smtp = Schemas["SmtpStatus"];
export function AdminView() {
  const { me } = useSession(); const [users, setUsers] = useState<User[]>([]), [projects, setProjects] = useState<AdminProject[]>([]), [smtp, setSmtp] = useState<Smtp>(), [notice, setNotice] = useState("");
  const [access, setAccess] = useState<Record<string, User[]>>({});
  async function load() { const [u, p, s] = await Promise.all([api.GET("/users"), api.GET("/admin/projects", { params: { query: { deleted: "all" } } }), api.GET("/admin/smtp")]); const nextProjects = p.data ?? []; const rows = await Promise.all(nextProjects.filter(x => !x.deleted_at).map(async x => [x.key, (await api.GET("/projects/{key}/users", { params: { path: { key: x.key } } })).data ?? []] as const)); setUsers(u.data ?? []); setProjects(nextProjects); setSmtp(s.data); setAccess(Object.fromEntries(rows)); }
  useEffect(() => { if (me.administrator) void Promise.resolve().then(load); }, [me.administrator]);
  if (!me.administrator) return <Navigate to="/" replace />;
  const report = reporting(setNotice, load);
  // The form is taken before the await: React empties `currentTarget` once
  // the event has been dispatched, and reading it back threw where the list
  // was about to be reloaded, so an invited user did not appear until the
  // page was loaded again.
  async function invite(e: FormEvent<HTMLFormElement>) { e.preventDefault(); const form = e.currentTarget; const d = new FormData(form); const invited = await report(api.POST("/users", { body: { name: String(d.get("name")), email: String(d.get("email")), administrator: d.has("administrator") } }), "Invitation sent."); if (invited) form.reset(); }
  // Everyone who does not have access to this project yet. With nobody left
  // the select has no options, contributes no form entry, and the id read
  // back out of it was the string "null".
  const grantable = (key: string) => users.filter(u => !(access[key] ?? []).some(x => x.id === u.id));
  return <><PageHeader title="Instance administration" /><SettingsLayout>
    <Section title="Users" description="Invite users and manage their instance role and lifecycle."><form className="mb-3 grid gap-2 sm:grid-cols-[1fr_1fr_auto]" onSubmit={(e) => void invite(e)}><Input name="name" placeholder="Name" aria-label="Name" /><Input name="email" type="email" placeholder="Email" aria-label="Email" /><Button type="submit">Invite</Button><label className="flex gap-2 text-sm sm:col-span-3"><input name="administrator" type="checkbox" /> Administrator</label></form><div className="divide-y rounded-md border">{users.map(u => <Row key={u.id} title={u.name} detail={`${u.email} · ${u.state}${u.administrator ? " · administrator" : ""}`} action={<div className="flex gap-1">{u.state === "invited" && <Button size="sm" variant="outline" onClick={() => void report(api.POST("/users/{id}/invitation", { params: { path: { id: u.id } } }), "Invitation resent.")}>Resend</Button>}<Button size="sm" variant="outline" onClick={() => void report(api.PATCH("/users/{id}", { params: { path: { id: u.id } }, body: { administrator: !u.administrator } }), u.administrator ? `${u.name} is no longer an administrator.` : `${u.name} is now an administrator.`)}>{u.administrator ? "Demote" : "Make admin"}</Button><Button size="sm" variant="outline" onClick={() => void report(api.POST(u.state === "deactivated" ? "/users/{id}/reactivate" : "/users/{id}/deactivate", { params: { path: { id: u.id } } }), u.state === "deactivated" ? `${u.name} is active again.` : `${u.name} is deactivated.`)}>{u.state === "deactivated" ? "Reactivate" : "Deactivate"}</Button></div>} />)}</div></Section>
    <Section title="Projects" description="All projects, including deleted ones, and their project access."><div className="divide-y rounded-md border">{projects.map(p => <div key={p.key} className="p-3"><Row title={`${p.key} · ${p.name}`} detail={p.deleted_at ? `Deleted ${new Date(p.deleted_at).toLocaleString()}` : `Access: ${(access[p.key] ?? []).map(x => x.name).join(", ") || "nobody"}`} action={p.deleted_at && <Button size="sm" variant="outline" onClick={() => void report(api.POST("/projects/{key}/restore", { params: { path: { key: p.key } } }), `${p.key} restored.`)}>Restore</Button>} />{!p.deleted_at && <form className="mt-2 flex gap-2" onSubmit={(e) => { e.preventDefault(); const id = String(new FormData(e.currentTarget).get("user")); if (grantable(p.key).length === 0) return; void report(api.PUT("/projects/{key}/users/{id}", { params: { path: { key: p.key, id } } }), `Access to ${p.key} granted.`); }}><select name="user" aria-label={`User for ${p.key}`} className="h-8 flex-1 rounded-md border bg-background px-2 text-sm">{grantable(p.key).map(u => <option key={u.id} value={u.id}>{u.name}</option>)}</select><Button type="submit" size="sm" variant="outline" disabled={grantable(p.key).length === 0}>Grant access</Button>{(access[p.key] ?? []).map(u => <Button key={u.id} type="button" size="sm" variant="ghost" onClick={() => void report(api.DELETE("/projects/{key}/users/{id}", { params: { path: { key: p.key, id: u.id } } }), `${u.name} no longer has access to ${p.key}.`)}>Remove {u.name}</Button>)}</form>}</div>)}</div></Section>
    <Section title="Transactional email" description="Credentials remain in environment variables.">{smtp && <p className="mb-3 text-sm">{smtp.configured ? `${smtp.host}:${smtp.port} · ${smtp.security} · ${smtp.sender}` : "Not configured"}</p>}<form className="flex max-w-lg gap-2" onSubmit={(e) => { e.preventDefault(); const email = String(new FormData(e.currentTarget).get("email")); void sendTest(email, setNotice); }}><Input name="email" type="email" placeholder="Test recipient" aria-label="Test recipient" /><Button type="submit" disabled={!smtp?.configured}>Send test</Button></form></Section>
    {notice && <p role="status" className="text-sm text-muted-foreground">{notice}</p>}
  </SettingsLayout></>;
}

/**
 * The only write on this screen that changes nothing, so it reports what the
 * instance answered without reloading the three lists behind it.
 */
async function sendTest(email: string, setNotice: (notice: string) => void) {
  try {
    const { error, response } = await api.POST("/admin/smtp/test", { body: { email } });
    setNotice(response.ok ? "Test email sent." : describe(error, response.status));
  } catch {
    setNotice("The instance did not answer.");
  }
}
