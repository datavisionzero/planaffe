import { BotOffIcon } from "lucide-react";
import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router";
import { api, describe } from "@/api/client";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { useSession } from "@/session/useSession";
import { PageHeader } from "@/shared/PageHeader";
import { spacePath } from "@/shell/views";
import { SearchResults } from "./SearchResults";
import { useSpaceList } from "./useSpaces";

/**
 * The spaces the caller may see — the door of the knowledge base. A space
 * closed to agents says so in its row: a switch that is only visible in the
 * settings of the space is one nobody remembers, and this one is what somebody
 * writing a personnel matter into a page is relying on (ADR 0027).
 */
export function SpacesView() {
  const { spaces } = useSpaceList();
  const { me } = useSession();
  const [params] = useSearchParams();
  const query = params.get("q") ?? "";

  // The door doubles as the search across everything behind it. The question
  // is in the address, so this screen reads it rather than holding one, and a
  // pasted link shows what it showed.
  if (query !== "") {
    return (
      <>
        <PageHeader title="Search" meta="The whole knowledge base" />
        <SearchResults query={query} space={undefined} />
      </>
    );
  }

  return (
    <>
      <PageHeader title="Spaces" meta={spaces.at === "known" ? `${spaces.spaces.length}` : undefined}>
        {/* A bracket is a human's to draw (ADR 0015, ADR 0027). An agent never
            reaches this screen, and the button is not what stops it — the
            instance is. */}
        {me.kind === "user" && <CreateSpace />}
      </PageHeader>

      {spaces.at === "asking" && (
        <div className="space-y-3 p-4" aria-busy>
          <Skeleton className="h-3 w-64" />
          <Skeleton className="h-3 w-48" />
        </div>
      )}

      {spaces.at === "failed" && <p className="p-4 text-sm text-destructive">The spaces could not be loaded.</p>}

      {spaces.at === "known" && spaces.spaces.length === 0 && (
        <div className="flex flex-1 flex-col items-center justify-center gap-2 p-8 text-center">
          <p className="font-medium">No space yet.</p>
          <p className="max-w-md text-sm text-muted-foreground">
            A space is the bracket of the knowledge base: a company, a domain, a customer, a handbook. It carries who
            may see what is in it, and the pages hang inside it — what is true whatever anybody is working on, and
            belongs to no project.
          </p>
        </div>
      )}

      {spaces.at === "known" && spaces.spaces.length > 0 && (
        <ul className="divide-y">
          {spaces.spaces.map((space) => (
            <li key={space.name}>
              <Link to={spacePath(space.name)} className="flex min-h-10 items-center gap-3 px-4 py-1 hover:bg-accent">
                <span className="min-w-0 flex-1 truncate">{space.title}</span>
                <span className="w-40 shrink-0 truncate font-mono text-xs text-muted-foreground">{space.name}</span>
                {space.closed_to_agents && (
                  <Badge variant="secondary" className="gap-1 font-normal">
                    <BotOffIcon className="size-3" aria-hidden />
                    Closed to agents
                  </Badge>
                )}
              </Link>
            </li>
          ))}
        </ul>
      )}
    </>
  );
}

/**
 * A new space: a name, a title and the switch, in a dialog rather than on a
 * screen of its own. Three fields are not a screen, and `/spaces/new` would be
 * a name somebody can take — a space is called what a person calls it, and
 * `/projects/new` only works because a project key is upper case.
 */
function CreateSpace() {
  const navigate = useNavigate();
  const { reload } = useSpaceList();
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [title, setTitle] = useState("");
  const [closed, setClosed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [why, setWhy] = useState<string>();

  async function create(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setWhy(undefined);

    try {
      const { data, error, response } = await api.POST("/spaces", {
        body: { name, title, closed_to_agents: closed },
      });

      if (data === undefined) {
        setWhy(describe(error, response.status));
        return;
      }

      await reload();
      setOpen(false);
      void navigate(spacePath(data.name));
    } catch {
      setWhy("The instance did not answer.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (busy) return;
        if (next) {
          setName("");
          setTitle("");
          setClosed(false);
        }
        setWhy(undefined);
        setOpen(next);
      }}
    >
      <DialogTrigger render={<Button size="sm" />}>New space</DialogTrigger>
      <DialogContent>
        <form onSubmit={(event) => void create(event)}>
          <DialogHeader>
            <DialogTitle>Create a space</DialogTitle>
            <DialogDescription>
              A space is the bracket of the knowledge base and the only thing in it that carries access. Who sees it is
              named on it afterwards, in its settings.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-3 py-3">
            <label className="grid gap-1 text-sm font-medium">
              Name
              <Input name="name" required autoFocus value={name} onChange={(event) => setName(event.target.value)} />
              <span className="text-xs font-normal text-muted-foreground">
                The address of the space: lower case letters and digits, hyphens between the words. It is not derived
                from the title.
              </span>
            </label>
            <label className="grid gap-1 text-sm font-medium">
              Title
              <Input name="title" required value={title} onChange={(event) => setTitle(event.target.value)} />
            </label>
            <label className="flex gap-2 text-sm">
              <input name="closed" type="checkbox" checked={closed} onChange={(event) => setClosed(event.target.checked)} />
              Closed to agents
            </label>
          </div>
          {why !== undefined && <p role="alert" className="text-sm text-destructive">{why}</p>}
          <DialogFooter>
            <DialogClose render={<Button type="button" variant="outline" disabled={busy} />}>Cancel</DialogClose>
            <Button type="submit" disabled={busy}>{busy ? "Creating…" : "Create space"}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
