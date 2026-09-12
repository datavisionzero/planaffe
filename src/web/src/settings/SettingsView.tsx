import { useEffect, useState } from "react";
import { api, describe, type Schemas } from "@/api/client";
import { Button } from "@/components/ui/button";
import { DropdownMenuItem } from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { useSession } from "@/session/useSession";
import { ActionDialog, TextActionDialog } from "@/shared/ActionDialog";
import { Choose } from "@/shared/Choose";
import { reporting } from "@/shared/report";
import { date, submitting } from "./forms";
import { Row, RowMenu, Rows, Said, Secret, Section, SettingsShell } from "./SettingsShell";

type Session = Schemas["BrowserSessionSummary"];
type Token = Schemas["TokenSummary"];
type Agent = Schemas["AgentSummary"];

/** The identity's own settings, one area per address. */
export function SettingsView() {
  return (
    <SettingsShell
      title="Personal settings"
      areas={[
        { to: "profile", label: "Profile", element: <Profile /> },
        { to: "security", label: "Security", element: <Security /> },
        { to: "tokens", label: "User tokens", element: <Tokens /> },
        { to: "agents", label: "Agents", element: <Agents /> },
      ]}
    />
  );
}

function Profile() {
  const { me } = useSession();
  const [notice, setNotice] = useState("");

  return (
    <Section title="Profile" description="Your name is used on comments and history.">
      <form className="flex max-w-lg gap-2" onSubmit={(e) => void submitting(e, setNotice, async (data) => { const r = await api.PATCH("/me", { body: { name: String(data.get("name")) } }); if (!r.data) throw new Error(describe(r.error, r.response.status)); window.location.reload(); })}>
        <Input name="name" defaultValue={me.name} aria-label="Name" />
        <Button type="submit">Save name</Button>
      </form>
      <form className="mt-3 flex max-w-lg gap-2" onSubmit={(e) => void submitting(e, setNotice, async (data) => { const r = await api.POST("/me/email", { body: { email: String(data.get("email")) } }); if (!r.response.ok) throw new Error(describe(r.error, r.response.status)); setNotice("Check the new address to confirm the change."); })}>
        <Input name="email" type="email" defaultValue={me.email ?? ""} aria-label="Email" />
        <Button type="submit" variant="outline">Change email</Button>
      </form>
      <Said notice={notice} />
    </Section>
  );
}

/** The password and the browsers it signs in: one subject, one address. */
function Security() {
  const [sessions, setSessions] = useState<Session[]>([]);
  const [passwordNotice, setPasswordNotice] = useState("");
  const [notice, setNotice] = useState("");
  async function load() { setSessions((await api.GET("/sessions")).data ?? []); }
  useEffect(() => { void (async () => { await load(); })(); }, []);
  const report = reporting(setNotice, load);

  return (
    <>
      <Section title="Password" description="Changing it signs every other browser session out.">
        <form className="grid max-w-lg gap-2" onSubmit={(e) => void submitting(e, setPasswordNotice, async (data, form) => { const r = await api.POST("/me/password", { body: { current_password: String(data.get("current")), password: String(data.get("password")) } }); if (!r.response.ok) throw new Error(describe(r.error, r.response.status)); form.reset(); })}>
          <Input name="current" type="password" placeholder="Current password" aria-label="Current password" />
          <Input name="password" type="password" placeholder="New password (12 characters or more)" minLength={12} aria-label="New password" />
          <Button type="submit" className="w-fit">Change password</Button>
        </form>
        <Said notice={passwordNotice} />
      </Section>
      <Section title="Browser sessions" description="Revoke browsers you no longer use.">
        <Rows empty="No browser sessions.">
          {sessions.map((s) => <Row key={s.id} title={s.current ? "This browser" : `Used ${date(s.last_used_at)}`} detail={`Created ${date(s.created_at)} · expires ${date(s.expires_at)}`} action={!s.current && <Button variant="outline" size="sm" onClick={() => void report(api.DELETE("/sessions/{id}", { params: { path: { id: s.id } } }), "Session revoked.")}>Revoke</Button>} />)}
        </Rows>
        <Button className="mt-3" variant="outline" onClick={() => void report(api.DELETE("/sessions"), "Every other session revoked.")}>Revoke all other sessions</Button>
        <Said notice={notice} />
      </Section>
    </>
  );
}

/**
 * Whether a list shows what no longer authenticates.
 *
 * A revoked token is not deleted and never will be: the identity it names is
 * still the author of everything that token ever wrote, and taking the row
 * away would quietly rewrite that record (ADR 0013). Which is a reason to keep
 * it, and no reason at all to keep it in the way — an agent retired months ago
 * is not what anybody opening this list came to see. So both lists show what
 * still works, and the rest is one switch away. It is the same case as the
 * deleted project of the instance administration, answered the same way.
 */
type Revoked = "hidden" | "shown";

function ShowRevoked({ value, onChange }: { value: Revoked; onChange: (value: Revoked) => void }) {
  return (
    <div className="mb-3 w-40">
      <Choose label="Revoked" value={value} onChange={(next) => onChange(next as Revoked)}>
        <option value="hidden">Hidden</option>
        <option value="shown">Shown</option>
      </Choose>
    </div>
  );
}

/**
 * The two ways these lists can have no rows, which are not the same sentence.
 * Saying "No agents." to somebody whose three agents are all revoked is a lie
 * they have no way to see through.
 */
function emptily(held: number, what: string) {
  return held === 0 ? `No ${what}.` : `No active ${what}. Revoked ones are hidden.`;
}

function Tokens() {
  const [tokens, setTokens] = useState<Token[]>([]);
  const [revoked, setRevoked] = useState<Revoked>("hidden");
  const [secret, setSecret] = useState("");
  const [notice, setNotice] = useState("");
  async function load() { setTokens((await api.GET("/tokens")).data ?? []); }
  useEffect(() => { void (async () => { await load(); })(); }, []);
  const report = reporting(setNotice, load);

  const shown = tokens.filter((t) => revoked === "shown" || !t.revoked_at);

  return (
    <Section title="User tokens" description="For the CLI and direct API use. A new secret is shown once.">
      {secret && <Secret value={secret} />}
      <Button className="mb-3" onClick={() => void report(api.POST("/tokens"), "Token created.").then((created) => { if (created) setSecret(created.secret); })}>Create token</Button>
      <ShowRevoked value={revoked} onChange={setRevoked} />
      <Rows empty={emptily(tokens.length, "user tokens")}>
        {shown.map((t) => <Row key={t.id} title={`${t.prefix}…`} detail={t.revoked_at ? `Revoked ${date(t.revoked_at)}` : `Created ${date(t.created_at)}`} action={!t.revoked_at && <Button variant="outline" size="sm" onClick={() => void report(api.DELETE("/tokens/{id}", { params: { path: { id: t.id } } }), "Token revoked.")}>Revoke</Button>} />)}
      </Rows>
      <Said notice={notice} />
    </Section>
  );
}

function Agents() {
  const [agents, setAgents] = useState<Agent[]>([]);
  const [revoked, setRevoked] = useState<Revoked>("hidden");
  const [secret, setSecret] = useState("");
  const [notice, setNotice] = useState("");
  async function load() { setAgents((await api.GET("/agents")).data ?? []); }
  useEffect(() => { void (async () => { await load(); })(); }, []);
  const report = reporting(setNotice, load);

  const shown = agents.filter((a) => revoked === "shown" || !a.token.revoked_at);

  return (
    <Section title="Agents" description="Each agent has one token that works and inherits your project access. Rotating gives it the next one.">
      {secret && <Secret value={secret} />}
      <form className="mb-3 flex max-w-lg gap-2" onSubmit={(e) => void submitting(e, setNotice, async (data, form) => { const r = await api.POST("/agents", { body: { name: String(data.get("name")) || null } }); if (!r.data) throw new Error(describe(r.error, r.response.status)); setSecret(r.data.token.secret); await load(); form.reset(); })}>
        <Input name="name" placeholder="Optional agent name" aria-label="Agent name" />
        <Button type="submit">Create agent</Button>
      </form>
      <ShowRevoked value={revoked} onChange={setRevoked} />
      <Rows empty={emptily(agents.length, "agents")}>
        {shown.map((a) => <AgentRow key={a.id} agent={a} report={report} reload={load} onIssued={setSecret} />)}
      </Rows>
      <Said notice={notice} />
    </Section>
  );
}

/** A write on this screen, and the one sentence it leaves behind. */
type Report = ReturnType<typeof reporting>;

/**
 * One agent and the three acts its owner has on it.
 *
 * Two buttons side by side already crowded the row on a phone, and a third
 * does not carry at all, so they stand in the row's menu — and the dialogs
 * beside it rather than inside it, because a menu closes on the click and
 * would take its own trigger down with it.
 *
 * Rotating asks first, and the question says what it costs rather than
 * asserting that it is safe: the secret in the agent's environment is dead the
 * moment it is confirmed, and a run still holding it fails on its next
 * request. For a revoked agent the same act is the way back — the only one —
 * and it takes nothing away, so it is worded and coloured as what it is.
 */
function AgentRow({ agent, report, reload, onIssued }: { agent: Agent; report: Report; reload: () => Promise<void>; onIssued: (secret: string) => void }) {
  const [asking, setAsking] = useState<"rename" | "rotate">();
  const revoked = agent.token.revoked_at;

  return (
    <Row
      title={agent.name}
      detail={`${agent.token.prefix}… · ${revoked ? `revoked ${date(revoked)}` : "active"}`}
      action={
        <>
          <RowMenu label={`Actions for ${agent.name}`}>
            <DropdownMenuItem onClick={() => setAsking("rename")}>Rename</DropdownMenuItem>
            <DropdownMenuItem onClick={() => setAsking("rotate")}>{revoked ? "New token" : "Rotate token"}</DropdownMenuItem>
            {!revoked && <DropdownMenuItem variant="destructive" onClick={() => void report(api.DELETE("/agents/{id}", { params: { path: { id: agent.id } } }), "Agent revoked.")}>Revoke</DropdownMenuItem>}
          </RowMenu>
          <TextActionDialog
            open={asking === "rename"}
            onOpenChange={(open) => setAsking(open ? "rename" : undefined)}
            title="Rename agent"
            description="Change the name used to identify this agent."
            label="Agent name"
            initialValue={agent.name}
            submitLabel="Save name"
            onSubmit={async (name) => { const result = await api.PATCH("/agents/{id}", { params: { path: { id: agent.id } }, body: { name } }); if (!result.data) throw new Error(describe(result.error, result.response.status)); await reload(); }}
          />
          <ActionDialog
            open={asking === "rotate"}
            onOpenChange={(open) => setAsking(open ? "rotate" : undefined)}
            title={revoked ? `Give ${agent.name} a new token?` : `Rotate the token of ${agent.name}?`}
            description={revoked
              ? `${agent.name} is revoked and authenticates as nobody. A new token brings it back under the same name, and everything it ever wrote stays its own. The secret is shown once.`
              : `The secret ${agent.name} is configured with stops working at once, and a run still holding it fails on its next request. The new one is shown once. Nothing ${agent.name} wrote changes.`}
            confirmLabel={revoked ? "Issue token" : "Rotate token"}
            confirmVariant={revoked ? "default" : "destructive"}
            onConfirm={async () => {
              const issued = await report(api.POST("/agents/{id}/token", { params: { path: { id: agent.id } } }), `${agent.name} has a new token.`);
              if (issued) onIssued(issued.secret);
            }}
          />
        </>
      }
    />
  );
}
