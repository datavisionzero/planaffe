import { DownloadIcon } from "lucide-react";
import { useEffect, useId, useState, type FormEvent, type ReactNode } from "react";
import { Link, useParams } from "react-router";
import { api, byAddress, codeOf, describe, type Problem } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Markdown } from "@/shared/Markdown";
import { MarkdownField } from "@/shared/MarkdownField";
import { PageHeader } from "@/shared/PageHeader";
import { useAbandon } from "@/shared/abandon";
import { stale } from "@/shared/stale";
import { spacePagePath, spacePath } from "@/shell/views";
import type { SpacePage } from "./context";
import { asFile, download } from "./download";
import { trailOf } from "./tree";
import { usePageTree, useSpaceList } from "./useSpaces";

type Load =
  | { at: "asking" }
  | { at: "failed"; why: string; code: string | undefined; until: string | undefined }
  | { at: "known"; page: SpacePage };

/**
 * A page of a space: the Markdown in the middle, rendered in the browser and
 * never as HTML (ADR 0007), with the path from the root down above it and the
 * editor in its place rather than beside it.
 *
 * Everything written here goes through the same `MarkdownField` as an issue's
 * description and a comment — same preview, same toolbar, same ⌘/Ctrl+Enter,
 * same question when a form with writing in it is left. The knowledge base
 * gets no second editor (VISION 18).
 */
export function SpacePageView() {
  const parameters = useParams();
  const name = parameters.name!;
  const path = parameters["*"] ?? "";
  const at = `${name}/${path}`;
  const [state, setState] = useState<{ at: string; load: Load }>();
  // The editor belongs to the page in the address: walking to another page
  // leaves no form open over a text it was never about.
  const [editingAt, setEditingAt] = useState<string>();
  const load: Load = state !== undefined && state.at === at ? state.load : { at: "asking" };
  const editing = editingAt === at;

  useEffect(() => {
    let live = true;

    void (async () => {
      try {
        const { data, error, response } = await api.GET("/spaces/{name}/pages/{path}", {
          ...byAddress,
          params: { path: { name, path } },
        });

        if (live) {
          setState({
            at,
            load:
              data === undefined
                ? {
                    at: "failed",
                    why: describe(error, response.status),
                    code: codeOf(error),
                    until: (error as (Problem & { restorable_until?: string }) | undefined)?.restorable_until,
                  }
                : { at: "known", page: data },
          });
        }
      } catch {
        if (live) {
          setState({ at, load: { at: "failed", why: "The instance did not answer.", code: undefined, until: undefined } });
        }
      }
    })();

    return () => {
      live = false;
    };
  }, [at, name, path]);

  if (load.at === "asking") {
    return (
      <>
        <PageHeader title={<Skeleton className="h-4 w-64" />} />
        <div className="space-y-3 p-4">
          <Skeleton className="h-3 w-full" />
          <Skeleton className="h-3 w-5/6" />
        </div>
      </>
    );
  }

  if (load.at === "failed") {
    return <Absent name={name} path={path} why={load.why} code={load.code} until={load.until} />;
  }

  const page = load.page;

  if (editing) {
    return (
      <>
        <PageHeader title={`Edit ${page.title}`} />
        <Trail name={name} path={path} />
        <EditForm
          name={name}
          page={page}
          onSaved={(saved) => {
            setState({ at, load: { at: "known", page: saved } });
            setEditingAt(undefined);
          }}
          onCancel={() => setEditingAt(undefined)}
        />
      </>
    );
  }

  return (
    <>
      <PageHeader title={page.title} meta={page.slug}>
        <Button variant="outline" size="sm" onClick={() => download(asFile(page))}>
          <DownloadIcon className="size-3.5" />
          Download
        </Button>
        <Button variant="outline" size="sm" onClick={() => setEditingAt(at)}>Edit</Button>
      </PageHeader>

      <Trail name={name} path={path} />

      <div className="max-w-3xl flex-1 p-4 md:p-6">
        {page.body === "" ? (
          <p className="text-sm text-muted-foreground">
            This page is empty. It is the place for what is true whatever anybody is working on — and what no ticket
            asks for.
          </p>
        ) : (
          <Markdown>{page.body}</Markdown>
        )}

        <section className="mt-6 border-t py-5">
          <h2 className="mb-3 text-xs font-medium tracking-wide text-muted-foreground uppercase">About</h2>
          <dl className="grid gap-1 text-sm sm:grid-cols-[8rem_1fr]">
            <dt className="text-muted-foreground">Address</dt><dd className="font-mono text-xs">{page.path}</dd>
            <dt className="text-muted-foreground">Author</dt><dd>{page.author.name}</dd>
            <dt className="text-muted-foreground">Created</dt><dd>{when(page.created_at)}</dd>
            <dt className="text-muted-foreground">Last change</dt><dd>{when(page.updated_at)} by {page.updated_by.name}</dd>
          </dl>
        </section>
      </div>
    </>
  );
}

/**
 * The path from the root down, out of the tree the frame already read: the
 * space, then every page above this one. It is a fact about the address
 * (ADR 0028), so the addresses are drawn even where the tree has not arrived —
 * what the tree adds is the titles.
 */
function Trail({ name, path }: { name: string; path: string }) {
  const { spaces } = useSpaceList();
  const { tree } = usePageTree();
  const space = spaces.at === "known" ? spaces.spaces.find((one) => one.name === name) : undefined;
  const above = tree.at === "known" ? trailOf(tree.pages, path).slice(0, -1) : [];

  return (
    <nav aria-label="The path to this page" className="flex flex-wrap items-center gap-1 border-b px-4 py-1.5 text-xs text-muted-foreground">
      <Link to={spacePath(name)} className="hover:text-foreground hover:underline">{space?.title ?? name}</Link>
      {above.map((page) => (
        <span key={page.path} className="flex items-center gap-1">
          <span aria-hidden>/</span>
          <Link to={spacePagePath(name, page.path)} className="hover:text-foreground hover:underline">{page.title}</Link>
        </span>
      ))}
    </nav>
  );
}

/**
 * A page that is not there, in the three ways it can be not there. A space the
 * caller may not see and a page that never existed answer the same sentence —
 * the knowledge base says nothing about what is closed to somebody (ADR 0027)
 * — and a deleted page says that it is deleted and until when, because that is
 * a page somebody here can have back.
 */
function Absent({ name, path, why, code, until }: {
  name: string;
  path: string;
  why: string;
  code: string | undefined;
  until: string | undefined;
}) {
  const deleted = code === "deleted";

  return (
    <>
      <PageHeader title={path.split("/").pop() ?? name} />
      <div className="m-auto grid max-w-md justify-items-center gap-3 p-8 text-center">
        <p role="status">{deleted ? "This page is deleted." : why}</p>
        <p className="text-sm text-muted-foreground">
          {deleted
            ? until === undefined
              ? "It can be brought back while its grace period lasts, and its address stays taken until then."
              : `It can be brought back until ${when(until)}, and its address stays taken until then.`
            : "It may have been renamed or moved, or it may sit in a space this account is not named on."}
        </p>
        <Link className="text-sm text-brand hover:underline" to={spacePath(name)}>
          Back to the space
        </Link>
      </div>
    </>
  );
}

/**
 * Saving is guarded with the `updated_at` last read, as everywhere else a text
 * is written here. A refusal is not a dead end: the typed text stays in the
 * field, the version that came back is what the next write is guarded with,
 * and the other side's text stands below to merge from.
 */
function EditForm({ name, page, onSaved, onCancel }: {
  name: string;
  page: SpacePage;
  onSaved: (page: SpacePage) => void;
  onCancel: () => void;
}) {
  const [title, setTitle] = useState(page.title);
  const [body, setBody] = useState(page.body);
  const [version, setVersion] = useState(page.updated_at);
  const [conflict, setConflict] = useState<SpacePage>();
  const [saving, setSaving] = useState(false);
  const [why, setWhy] = useState<string>();
  const titleId = useId();
  const changed = title !== page.title || body !== page.body;
  const { leave, dialog } = useAbandon(changed, onCancel);

  async function save(event?: FormEvent<HTMLFormElement>) {
    event?.preventDefault();
    setSaving(true);
    setWhy(undefined);
    setConflict(undefined);

    try {
      const answer = await api.PATCH("/spaces/{name}/pages/{path}", {
        ...byAddress,
        params: { path: { name, path: page.path } },
        headers: { "If-Match": version },
        body: { title, body },
      });

      const current = stale<SpacePage>(answer);

      if (current !== undefined) {
        setConflict(current);
        setVersion(current.updated_at);
        return;
      }

      if (answer.data === undefined) {
        setWhy(describe(answer.error, answer.response.status));
        return;
      }

      onSaved(answer.data);
    } catch {
      setWhy("The instance did not answer.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <form className="mx-auto grid w-full max-w-3xl gap-4 p-4 md:p-6" onSubmit={(event) => void save(event)}>
      <label className="grid gap-1 text-sm font-medium" htmlFor={titleId}>
        Title
        <Input id={titleId} name="title" required value={title} onChange={(event) => setTitle(event.target.value)} />
      </label>
      <MarkdownField label="Body" value={body} onChange={setBody} onSubmit={() => void save()} />
      {conflict === undefined
        ? why !== undefined && <p role="alert" className="text-sm text-destructive">{why}</p>
        : <Conflict page={conflict} />}
      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={leave}>Cancel</Button>
        <Button type="submit" disabled={saving}>{saving ? "Saving…" : "Save changes"}</Button>
      </div>
      {dialog}
    </form>
  );
}

/** What a stale refusal means, in the words it means it. */
function Conflict({ page }: { page: SpacePage }): ReactNode {
  return (
    <div role="alert" className="grid gap-2 rounded-lg border border-amber-500/40 bg-amber-500/5 p-3 text-sm">
      <p>
        <span className="font-medium">{page.title} was changed while you were editing it.</span>{" "}
        Your text is kept. Saving now writes it over the version below.
      </p>
      <details className="text-xs">
        <summary className="cursor-pointer text-muted-foreground">
          The page as it stands, saved {when(page.updated_at)}
        </summary>
        <pre className="mt-2 max-h-64 overflow-auto rounded-md bg-muted p-2 font-mono whitespace-pre-wrap">{page.body}</pre>
      </details>
    </div>
  );
}

function when(value: string) {
  return new Date(value).toLocaleString();
}
