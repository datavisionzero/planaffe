import { useEffect, useState, type FormEvent, type ReactNode } from "react";
import { api, describe, type Schemas } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useSession } from "@/session/useSession";
import { TextActionDialog } from "@/shared/ActionDialog";
import { PageHeader } from "@/shared/PageHeader";

type Session = Schemas["BrowserSessionSummary"];
type Token = Schemas["TokenSummary"];
type Agent = Schemas["AgentSummary"];

export function SettingsView() {
  const { me } = useSession();
  const [sessions, setSessions] = useState<Session[]>([]), [tokens, setTokens] = useState<Token[]>([]), [agents, setAgents] = useState<Agent[]>([]);
  const [notice, setNotice] = useState(""), [secret, setSecret] = useState("");
  async function load() { const [s, t, a] = await Promise.all([api.GET("/sessions"), api.GET("/tokens"), api.GET("/agents")]); setSessions(s.data ?? []); setTokens(t.data ?? []); setAgents(a.data ?? []); }
  useEffect(() => { void (async () => { await load(); })(); }, []);
  // React empties `currentTarget` once the event has been dispatched, so the
  // form is taken here and handed to the action: one that awaits and then
  // reads it back off the event finds null, and the `TypeError` that follows
  // is reported as if the write had failed.
  async function submit(event: FormEvent<HTMLFormElement>, action: (data: FormData, form: HTMLFormElement) => Promise<void>) { event.preventDefault(); const form = event.currentTarget; setNotice(""); try { await action(new FormData(form), form); setNotice("Saved."); } catch (error) { setNotice(error instanceof Error ? error.message : "The instance did not answer."); } }

  return <><PageHeader title="Personal settings" /><SettingsLayout>
    <Section title="Profile" description="Your name is used on comments and history.">
      <form className="flex max-w-lg gap-2" onSubmit={(e) => void submit(e, async data => { const r = await api.PATCH("/me", { body: { name: String(data.get("name")) } }); if (!r.data) throw new Error(describe(r.error, r.response.status)); window.location.reload(); })}><Input name="name" defaultValue={me.name} aria-label="Name" /><Button type="submit">Save name</Button></form>
      <form className="mt-3 flex max-w-lg gap-2" onSubmit={(e) => void submit(e, async data => { const r = await api.POST("/me/email", { body: { email: String(data.get("email")) } }); if (!r.response.ok) throw new Error(describe(r.error, r.response.status)); setNotice("Check the new address to confirm the change."); })}><Input name="email" type="email" defaultValue={me.email ?? ""} aria-label="Email" /><Button type="submit" variant="outline">Change email</Button></form>
    </Section>
    <Section title="Password" description="Changing it signs every other browser session out.">
      <form className="grid max-w-lg gap-2" onSubmit={(e) => void submit(e, async (data, form) => { const r = await api.POST("/me/password", { body: { current_password: String(data.get("current")), password: String(data.get("password")) } }); if (!r.response.ok) throw new Error(describe(r.error, r.response.status)); form.reset(); })}><Input name="current" type="password" placeholder="Current password" aria-label="Current password" /><Input name="password" type="password" placeholder="New password (12 characters or more)" minLength={12} aria-label="New password" /><Button type="submit" className="w-fit">Change password</Button></form>
    </Section>
    <Section title="Browser sessions" description="Revoke browsers you no longer use."><Rows empty="No browser sessions.">{sessions.map(s => <Row key={s.id} title={s.current ? "This browser" : `Used ${date(s.last_used_at)}`} detail={`Created ${date(s.created_at)} · expires ${date(s.expires_at)}`} action={!s.current && <Button variant="outline" size="sm" onClick={() => void api.DELETE("/sessions/{id}", { params: { path: { id: s.id } } }).then(load)}>Revoke</Button>} />)}</Rows><Button className="mt-3" variant="outline" onClick={() => void api.DELETE("/sessions").then(load)}>Revoke all other sessions</Button></Section>
    <Section title="User tokens" description="For the CLI and direct API use. A new secret is shown once.">{secret && <Secret value={secret} />}<Button className="mb-3" onClick={() => void api.POST("/tokens").then(r => { if (r.data) { setSecret(r.data.secret); void load(); } })}>Create token</Button><Rows empty="No user tokens.">{tokens.map(t => <Row key={t.id} title={`${t.prefix}…`} detail={t.revoked_at ? `Revoked ${date(t.revoked_at)}` : `Created ${date(t.created_at)}`} action={!t.revoked_at && <Button variant="outline" size="sm" onClick={() => void api.DELETE("/tokens/{id}", { params: { path: { id: t.id } } }).then(load)}>Revoke</Button>} />)}</Rows></Section>
    <Section title="Agents" description="Each agent has one token and inherits your project access."><form className="mb-3 flex max-w-lg gap-2" onSubmit={(e) => void submit(e, async (data, form) => { const r = await api.POST("/agents", { body: { name: String(data.get("name")) || null } }); if (!r.data) throw new Error(describe(r.error, r.response.status)); setSecret(r.data.token.secret); await load(); form.reset(); })}><Input name="name" placeholder="Optional agent name" aria-label="Agent name" /><Button type="submit">Create agent</Button></form><Rows empty="No agents.">{agents.map(a => <Row key={a.id} title={a.name} detail={`${a.token.prefix}… · ${a.token.revoked_at ? "revoked" : "active"}`} action={<div className="flex gap-1"><TextActionDialog trigger={<Button variant="outline" size="sm">Rename</Button>} title="Rename agent" description="Change the name used to identify this agent." label="Agent name" initialValue={a.name} submitLabel="Save name" onSubmit={async (name) => { const result = await api.PATCH("/agents/{id}", { params: { path: { id: a.id } }, body: { name } }); if (!result.data) throw new Error(describe(result.error, result.response.status)); await load(); }} />{!a.token.revoked_at && <Button variant="outline" size="sm" onClick={() => void api.DELETE("/agents/{id}", { params: { path: { id: a.id } } }).then(load)}>Revoke</Button>}</div>} />)}</Rows></Section>
    {notice && <p role="status" className="text-sm text-muted-foreground">{notice}</p>}
  </SettingsLayout></>;
}

export function SettingsLayout({ children }: { children: ReactNode }) { return <div className="mx-auto w-full max-w-4xl space-y-8 p-4 sm:p-6">{children}</div>; }
export function Section({ title, description, children }: { title: string; description?: string; children: ReactNode }) { const id = `section-${title.replaceAll(" ", "-")}`; return <section aria-labelledby={id}><h2 id={id} className="font-medium">{title}</h2>{description && <p className="mb-3 text-sm text-muted-foreground">{description}</p>}<div className="rounded-md border p-3">{children}</div></section>; }
function Rows({ children, empty }: { children: ReactNode[]; empty: string }) { return <div className="divide-y rounded-md border">{children.length ? children : <p className="p-3 text-sm text-muted-foreground">{empty}</p>}</div>; }
export function Row({ title, detail, action }: { title: string; detail?: string; action?: ReactNode }) { return <div className="flex min-h-12 items-center gap-3 p-3"><div className="min-w-0 flex-1"><p className="truncate text-sm font-medium">{title}</p>{detail && <p className="text-xs text-muted-foreground">{detail}</p>}</div>{action}</div>; }
function Secret({ value }: { value: string }) { return <div role="status" className="mb-3 rounded-md border border-brand/40 bg-brand/5 p-3"><p className="text-xs text-muted-foreground">Copy this secret now. It will not be shown again.</p><code className="break-all text-xs">{value}</code></div>; }
const date = (value: string) => new Date(value).toLocaleString();
