import type { SpacePageSummary } from "./context";

/**
 * Three levels and no fourth (VISION 18, ADR 0028). The number is here because
 * the navigation, the create form and the move dialog all have to know it, and
 * a limit written down three times is a limit that moves twice.
 */
export const levels = 3;

/** The address of the page a segment list names: the slugs from the root down. */
export function pathOf(segments: string[]): string {
  return segments.join("/");
}

/** The slugs of an address — `company/handbook/onboarding` is three of them. */
export function segmentsOf(path: string): string[] {
  return path.split("/").filter((segment) => segment !== "");
}

/**
 * The addresses above a page, outermost first: `company/handbook` for
 * `company/handbook/onboarding`. It is a fact about the address and needs no
 * tree, which is what makes the path above a page drawable before the tree has
 * arrived.
 */
export function ancestorsOf(path: string): string[] {
  const segments = segmentsOf(path);

  return segments.slice(0, -1).map((_, index) => pathOf(segments.slice(0, index + 1)));
}

/**
 * The pages directly under a parent — `null` for the ones directly under the
 * space — in the order the instance sent them, which is siblings by title
 * (`docs/api.md`, Space pages). The order is not remade here: a client that
 * sorts a list it was given sorted is a second opinion about it.
 */
export function childrenOf(pages: SpacePageSummary[], parent: string | null): SpacePageSummary[] {
  return pages.filter((page) => page.parent === parent);
}

/**
 * The rows from the root down to a page, the page itself last. A row the tree
 * does not hold is left out rather than guessed at: the path above a page says
 * what it knows, and the address carries the rest.
 */
export function trailOf(pages: SpacePageSummary[], path: string): SpacePageSummary[] {
  const wanted = [...ancestorsOf(path), path];
  const known = new Map(pages.map((page) => [page.path, page] as const));

  return wanted.flatMap((address) => {
    const page = known.get(address);

    return page === undefined ? [] : [page];
  });
}

/** Every page under this one, however deep. Deleting takes them, renaming moves them. */
export function descendantsOf(pages: SpacePageSummary[], path: string): SpacePageSummary[] {
  return pages.filter((page) => page.path.startsWith(`${path}/`));
}

/**
 * Whether a page could take a child at all — the fourth level does not exist.
 * `depth` is the instance's, counted from zero directly under the space, so a
 * page at the third level is `2` and has no room below it.
 */
export function takesChildren(depth: number): boolean {
  return depth + 1 < levels;
}
