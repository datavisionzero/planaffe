import { useSession } from "@/session/useSession";
import { PageHeader } from "@/shared/PageHeader";

/**
 * The account: who this browser is. Agent tokens and user administration are
 * screens of cut three that arrive with their tickets; for now the CLI is the
 * place (`pa token`, `pa agent`, `pa user`).
 */
export function SettingsView() {
  const { me } = useSession();

  return (
    <>
      <PageHeader title="Settings" />
      <div className="space-y-4 p-4 text-sm">
        <dl className="grid grid-cols-[8rem_1fr] gap-y-2">
          <dt className="text-muted-foreground">Signed in as</dt>
          <dd>{me.name}</dd>
          <dt className="text-muted-foreground">Kind</dt>
          <dd>{me.kind}{me.administrator ? " · administrator" : ""}</dd>
          <dt className="text-muted-foreground">Access</dt>
          <dd className="text-xs">
            {me.token ? <span className="font-mono">{me.token.prefix}… <span className="font-sans text-muted-foreground">since {new Date(me.token.created_at).toLocaleDateString()}</span></span> : "Browser session"}
          </dd>
        </dl>
        <p className="text-xs text-muted-foreground">
          Tokens, agents and users are managed with <code className="font-mono">pa token</code>,{" "}
          <code className="font-mono">pa agent</code> and <code className="font-mono">pa user</code> until their
          screens arrive.
        </p>
      </div>
    </>
  );
}
