import type { SpacePage } from "./context";

/**
 * A page as the file it already is. The body is written out exactly as it is
 * stored — no front matter, no title heading added on the way out, nothing of
 * ours around it: the text was never ours to hold on to, and `pg_dump` and
 * `pa export` are the instance's way out while this is the reader's
 * (VISION 18). The name is the slug, because that is the page's name.
 */
export function asFile(page: Pick<SpacePage, "slug" | "body">): { name: string; text: string } {
  return { name: `${page.slug}.md`, text: page.body };
}

/**
 * Hand the file to the browser. It is a blob and not a second way over the
 * server: the body is in the browser already, and a download route would be an
 * endpoint that answers what `GET` on the page just answered.
 */
export function download(file: { name: string; text: string }): void {
  const url = URL.createObjectURL(new Blob([file.text], { type: "text/markdown;charset=utf-8" }));
  const link = document.createElement("a");

  link.href = url;
  link.download = file.name;
  document.body.append(link);
  link.click();
  link.remove();

  // The object outlives the click, and nothing else will free it.
  URL.revokeObjectURL(url);
}
