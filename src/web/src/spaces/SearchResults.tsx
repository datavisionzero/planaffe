import { useCallback, useRef } from "react";
import { Link } from "react-router";
import { Skeleton } from "@/components/ui/skeleton";
import { spacePagePath } from "@/shell/views";
import { shortest, useKnowledgeSearch, type SpacePageHit } from "./search";

/**
 * What a search found: a hit says where the page stands and shows the words
 * that matched inside the text they were found in.
 *
 * A row is one link and the way down to it is text inside that link rather
 * than a chain of its own. One target per hit is what makes the list walkable
 * with the arrow keys, and the page it leads to carries the same path above it
 * as links.
 */
export function SearchResults({ query, space }: { query: string; space: string | undefined }) {
  const hits = useKnowledgeSearch(query, space);
  const list = useRef<HTMLUListElement>(null);

  const walk = useCallback((event: React.KeyboardEvent<HTMLUListElement>) => {
    if (event.key !== "ArrowDown" && event.key !== "ArrowUp") {
      return;
    }

    const rows = Array.from(list.current?.querySelectorAll<HTMLAnchorElement>("a[href]") ?? []);
    const at = rows.indexOf(document.activeElement as HTMLAnchorElement);
    const next = event.key === "ArrowDown" ? at + 1 : at - 1;

    if (rows.length > 0) {
      event.preventDefault();
      rows[Math.max(0, Math.min(rows.length - 1, at < 0 ? 0 : next))]?.focus();
    }
  }, []);

  if (query.trim().length < shortest) {
    return (
      <p className="p-4 text-sm text-muted-foreground md:p-6">
        Two letters is the shortest question. The search reads the titles and the bodies of the pages you may see.
      </p>
    );
  }

  if (hits.at === "asking") {
    return (
      <div className="space-y-3 p-4 md:p-6" aria-busy>
        <Skeleton className="h-3 w-64" />
        <Skeleton className="h-3 w-80" />
        <Skeleton className="h-3 w-48" />
      </div>
    );
  }

  if (hits.at === "failed") {
    return <p className="p-4 text-sm text-destructive md:p-6">{hits.why}</p>;
  }

  // Nothing matched is a state and not an empty list. The way out of one
  // space is not offered twice: the screen above a search inside a space
  // carries it whether anything was found or not.
  if (hits.hits.length === 0) {
    return (
      <p className="p-4 text-sm md:p-6">
        Nothing matched “{query.trim()}”.{space === undefined ? "" : " Not in this space, at least."}
      </p>
    );
  }

  return (
    <ul ref={list} aria-label="Hits" className="divide-y" onKeyDown={walk}>
      {hits.hits.map((hit) => (
        <li key={`${hit.space}/${hit.path}`}>
          <Link
            to={spacePagePath(hit.space, hit.path)}
            className="block px-4 py-2 hover:bg-accent focus-visible:bg-accent focus-visible:outline-none md:px-6"
          >
            <p className="truncate text-xs text-muted-foreground">
              {[hit.space_title, ...hit.trail.map((step) => step.title)].join(" / ")}
            </p>
            <p className="font-medium">{hit.title}</p>
            <p className="mt-0.5 line-clamp-2 text-sm text-muted-foreground">
              <Excerpt excerpt={hit.excerpt} />
            </p>
          </Link>
        </li>
      ))}
    </ul>
  );
}

/**
 * The excerpt as the instance sent it: pieces with a flag each, never a string
 * with markup in it. Nothing the server writes reaches the tree as HTML
 * (ADR 0007), and marking is the client's to do.
 */
export function Excerpt({ excerpt }: { excerpt: SpacePageHit["excerpt"] }) {
  return (
    <>
      {excerpt.map((piece, at) =>
        // The pieces are a sequence and have no identity of their own, so the
        // place in it is the key it has.
        piece.hit ? (
          <mark key={at} className="bg-transparent font-medium text-foreground">
            {piece.text}
          </mark>
        ) : (
          <span key={at}>{piece.text}</span>
        ),
      )}
    </>
  );
}
