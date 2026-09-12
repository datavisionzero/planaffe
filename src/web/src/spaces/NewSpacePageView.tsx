import { useId, useState, type FormEvent } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router";
import { api, describe } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Choose } from "@/shared/Choose";
import { MarkdownField } from "@/shared/MarkdownField";
import { PageHeader } from "@/shared/PageHeader";
import { useAbandon } from "@/shared/abandon";
import { spacePagePath, spacePath } from "@/shell/views";
import type { SpacePageSummary } from "./context";
import { takesChildren } from "./tree";
import { usePageTree } from "./useSpaces";

/**
 * A new page in a space. The slug is given here and never derived from the
 * title (ADR 0021), and the parent is a choice rather than a field to spell:
 * it is an address, and every address that could be one is already in the tree
 * the frame read.
 *
 * The screen is at `/spaces/{name}/new` rather than under `pages/`, so that no
 * page can ever stand where it does: a page's address always begins with
 * `pages/`.
 */
export function NewSpacePageView() {
  const { name } = useParams();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { tree, reload } = usePageTree();
  const [parent, setParent] = useState(params.get("parent") ?? "");
  const [slug, setSlug] = useState("");
  const [title, setTitle] = useState("");
  const [body, setBody] = useState("");
  const [saving, setSaving] = useState(false);
  const [why, setWhy] = useState<string>();
  const slugId = useId();
  const titleId = useId();
  const written = slug !== "" || title !== "" || body !== "";
  const { leave, dialog } = useAbandon(written, () => void navigate(spacePath(name!)));

  // Only a page with room below it can be a parent — there is no fourth level
  // (VISION 18). The instance says so too, and its refusal is shown where it
  // comes; this is the offer, not the guarantee.
  const parents = tree.at === "known" ? tree.pages.filter((page) => takesChildren(page.depth)) : [];

  async function create(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    setWhy(undefined);

    try {
      const { data, error, response } = await api.POST("/spaces/{name}/pages", {
        params: { path: { name: name! } },
        body: { parent: parent === "" ? null : parent, slug, title, body },
      });

      if (data === undefined) {
        setWhy(describe(error, response.status));
        return;
      }

      // The frame holds the tree, so the frame is what catches up — before the
      // address it is about to lead to is drawn under a navigation without it.
      await reload();
      void navigate(spacePagePath(name!, data.path), { replace: true });
    } catch {
      setWhy("The instance did not answer.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <>
      <PageHeader title="Create page" />
      <form className="mx-auto grid w-full max-w-3xl gap-4 p-4 md:p-6" onSubmit={(event) => void create(event)}>
        <Choose label="Under" value={parent} onChange={setParent}>
          <option value="">Directly under the space</option>
          {parents.map((page) => (
            <option key={page.path} value={page.path}>{indented(page)}</option>
          ))}
        </Choose>
        <div className="grid gap-1 text-sm font-medium">
          <label htmlFor={slugId}>Slug</label>
          <Input id={slugId} name="slug" required autoFocus value={slug} onChange={(event) => setSlug(event.target.value)} />
          <span className="text-xs font-normal text-muted-foreground">
            The last part of the address: lower case letters and digits, hyphens between the words. It is not derived
            from the title, it has to be free under the page above, and renaming it later leaves nothing behind at the
            old address.
          </span>
        </div>
        <label className="grid gap-1 text-sm font-medium" htmlFor={titleId}>
          Title
          <Input id={titleId} name="title" required value={title} onChange={(event) => setTitle(event.target.value)} />
        </label>
        <MarkdownField label="Body" value={body} onChange={setBody} />
        {why !== undefined && <p role="alert" className="text-sm text-destructive">{why}</p>}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={leave}>Cancel</Button>
          <Button type="submit" disabled={saving}>{saving ? "Creating…" : "Create page"}</Button>
        </div>
        {dialog}
      </form>
    </>
  );
}

/** The tree, flattened into a list of choices that still says what hangs where. */
function indented(page: SpacePageSummary): string {
  return `${"— ".repeat(page.depth)}${page.title}`;
}
