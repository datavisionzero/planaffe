import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { api, describe, type Schemas } from "@/api/client";
import { Button } from "@/components/ui/button";
import { DropdownMenuItem } from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { useSession } from "@/session/useSession";
import { ActionDialog, TextActionDialog } from "@/shared/ActionDialog";
import { Row, RowMenu, Rows, Said, Section, SettingsShell } from "@/settings/SettingsShell";
import { submitting } from "@/settings/forms";
import { spacePath } from "@/shell/views";
import type { Space } from "./context";
import { useSpaceList } from "./useSpaces";

type User = Schemas["UserSummary"];

/**
 * A space's own settings, in the area the space is in: managing one happens
 * nowhere else (VISION 18). It is drawn by the same `SettingsShell` the
 * project and the instance use, because it is the same kind of screen and not
 * a second kind.
 */
export function SpaceSettingsView() {
  const { name } = useParams();

  return (
    <SettingsShell
      title={`${name} settings`}
      areas={[
        { to: "general", label: "General", element: <General /> },
        { to: "members", label: "Members", element: <Members /> },
      ]}
    />
  );
}

function General() {
  const { name } = useParams();
  const navigate = useNavigate();
  const { me } = useSession();
  const { spaces, reload } = useSpaceList();
  const [notice, setNotice] = useState("");
  const space = spaces.at === "known" ? spaces.spaces.find((one) => one.name === name) : undefined;

  const changed = async (next: Space) => {
    await reload();
    return next;
  };

  return (
    <>
      <Section title="Space" description="The title is what a reader sees; the name is the address.">
        {space && (
          <form
            className="grid max-w-lg gap-3"
            onSubmit={(event) =>
              void submitting(event, setNotice, async (data) => {
                const answer = await api.PATCH("/spaces/{name}", {
                  params: { path: { name: name! } },
                  body: { name: null, title: String(data.get("title")), closed_to_agents: data.has("closed") },
                });

                if (!answer.data) throw new Error(describe(answer.error, answer.response.status));
                await changed(answer.data);
              })
            }
          >
            <label className="text-sm">
              Title
              <Input name="title" defaultValue={space.title} />
            </label>
            <label className="flex gap-2 text-sm">
              <input name="closed" type="checkbox" defaultChecked={space.closed_to_agents} />
              Closed to agents
            </label>
            {/* What the switch means, beside the switch. Whoever writes
                something sensitive into a page is relying on this sentence, so
                it says the thing that is true: not forbidden, not there. */}
            <p className="text-xs text-muted-foreground">
              A closed space is not refused to an agent — it is absent. Not in a list, not in a search result, not
              under its own address, whatever route the agent uses: the console, a script against the API, or
              whatever comes after them. It says nothing about who else may see it; that is the members below.
            </p>
            <Button type="submit" className="w-fit">Save space</Button>
          </form>
        )}
        <Said notice={notice} />
      </Section>

      <Section title="Name" description="The address of the space, and the one thing renaming really moves.">
        {space && (
          <TextActionDialog
            trigger={<Button variant="outline">Rename space</Button>}
            title={`Rename ${space.name}?`}
            description="The space and every page in it move to a new address. Nothing forwards: the old name leads nowhere afterwards, and links written to it stop working."
            label="New name"
            initialValue={space.name}
            submitLabel="Rename space"
            onSubmit={async (next) => {
              const answer = await api.PATCH("/spaces/{name}", {
                params: { path: { name: name! } },
                body: { name: next, title: null, closed_to_agents: null },
              });

              if (!answer.data) throw new Error(describe(answer.error, answer.response.status));
              await changed(answer.data);
              void navigate(`${spacePath(answer.data.name)}/settings/general`, { replace: true });
            }}
          />
        )}
      </Section>

      {me.administrator && (
        <Section title="Space lifecycle" description="Deletion is reversible during the instance grace period.">
          <ActionDialog
            trigger={<Button variant="destructive">Delete space</Button>}
            title={`Delete space ${name}?`}
            description="The space and every page in it disappear. It can be restored under Administration while the instance grace period lasts, and its name stays taken until that is over, so nothing else can move into it in the meantime."
            confirmLabel="Delete space"
            onConfirm={async () => {
              const answer = await api.DELETE("/spaces/{name}", { params: { path: { name: name! } } });
              if (!answer.response.ok) throw new Error(describe(answer.error, answer.response.status));
              await reload();
              void navigate("/spaces", { replace: true });
            }}
          />
        </Section>
      )}
    </>
  );
}

/**
 * Who sees this space. Access is per user and coarse, exactly as project
 * access is (VISION 12, ADR 0027) — no rights per page, no roles, no
 * inheritance to reason about. An agent is never named here: it inherits its
 * owner, minus every space the switch above closes.
 */
function Members() {
  const { name } = useParams();
  const { me } = useSession();
  const [named, setNamed] = useState<User[]>([]);
  const [everybody, setEverybody] = useState<User[]>([]);
  const [notice, setNotice] = useState("");
  const [why, setWhy] = useState<string>();

  const load = useCallback(async () => {
    const [here, all] = await Promise.all([
      api.GET("/spaces/{name}/users", { params: { path: { name: name! } } }),
      me.administrator ? api.GET("/users") : Promise.resolve({ data: undefined }),
    ]);

    setNamed(here.data ?? []);
    setEverybody(all.data ?? []);
    setWhy(here.data === undefined ? "The people named on this space could not be read." : undefined);
  }, [me.administrator, name]);

  useEffect(() => {
    void (async () => {
      await load();
    })();
  }, [load]);

  const grantable = everybody.filter((candidate) => !named.some((one) => one.id === candidate.id));

  async function grant(event: FormEvent<HTMLFormElement>) {
    await submitting(event, setNotice, async (data) => {
      if (grantable.length === 0) return;

      const id = String(data.get("user"));
      const answer = await api.PUT("/spaces/{name}/users/{id}", { params: { path: { name: name!, id } } });

      if (!answer.response.ok) throw new Error(describe(answer.error, answer.response.status));
      await load();
    });
  }

  return (
    <Section title="Members" description="The users who see this space. Their agents see it too, unless it is closed to agents.">
      {why !== undefined && <p role="alert" className="mb-3 text-sm text-destructive">{why}</p>}
      {me.administrator && (
        <form className="mb-3 flex gap-2" onSubmit={(event) => void grant(event)}>
          <select name="user" aria-label={`User for ${name}`} className="h-8 flex-1 rounded-md border bg-background px-2 text-sm">
            {grantable.map((one) => <option key={one.id} value={one.id}>{one.name}</option>)}
          </select>
          <Button type="submit" size="sm" variant="outline" disabled={grantable.length === 0}>Grant access</Button>
        </form>
      )}
      <Rows empty="Nobody is named on this space.">
        {named.map((one) => (
          <Row
            key={one.id}
            title={one.name}
            detail={`${one.email} · ${one.state}${one.administrator ? " · administrator" : ""}`}
            action={
              me.administrator ? (
                <RowMenu label={`Actions for ${one.name}`}>
                  <DropdownMenuItem
                    onClick={() =>
                      void (async () => {
                        const answer = await api.DELETE("/spaces/{name}/users/{id}", {
                          params: { path: { name: name!, id: one.id } },
                        });

                        setNotice(
                          answer.response.ok
                            ? `${one.name} no longer sees ${name}.`
                            : describe(answer.error, answer.response.status),
                        );
                        await load();
                      })()
                    }
                  >
                    Remove access
                  </DropdownMenuItem>
                </RowMenu>
              ) : undefined
            }
          />
        ))}
      </Rows>
      {!me.administrator && (
        <p className="mt-3 text-sm text-muted-foreground">
          Naming somebody on a space, and taking them off it, is an administrator's act.
        </p>
      )}
      <Said notice={notice} />
      <p className="mt-3 text-sm">
        <Link className="text-brand hover:underline" to={spacePath(name!)}>Back to the space</Link>
      </p>
    </Section>
  );
}
