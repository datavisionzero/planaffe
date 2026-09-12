import { BotOffIcon } from "lucide-react";
import { Link, useParams } from "react-router";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { PageHeader } from "@/shared/PageHeader";
import { spacePagePath } from "@/shell/views";
import { childrenOf } from "./tree";
import { usePageTree, useSpaceList } from "./useSpaces";

/**
 * A space itself: what stands directly under it, which is the table of
 * contents the tree in the navigation says the same thing about. A space has
 * no text of its own — the knowledge is in the pages — so this is deliberately
 * a way in and not a screen with content of its own.
 */
export function SpaceView() {
  const { name } = useParams();
  const { spaces } = useSpaceList();
  const { tree } = usePageTree();
  const space = spaces.at === "known" ? spaces.spaces.find((one) => one.name === name) : undefined;


  if (spaces.at === "known" && space === undefined) {
    return <Missing name={name!} />;
  }

  return (
    <>
      <PageHeader title={space?.title ?? <Skeleton className="h-4 w-48" />} meta={space === undefined ? undefined : space.name}>
        {space?.closed_to_agents === true && (
          <Badge variant="secondary" className="gap-1 font-normal">
            <BotOffIcon className="size-3" aria-hidden />
            Closed to agents
          </Badge>
        )}
      </PageHeader>

      <div className="max-w-3xl flex-1 p-4 md:p-6">
        {tree.at === "asking" && <Skeleton className="h-3 w-64" />}
        {tree.at === "failed" && <p className="text-sm text-destructive">{tree.why}</p>}
        {tree.at === "known" && tree.pages.length === 0 && (
          <p className="text-sm text-muted-foreground">
            This space has no page yet. A page is a Markdown document with a place in the tree, at most three levels
            under the space.
          </p>
        )}
        {tree.at === "known" && tree.pages.length > 0 && (
          <ul aria-label="Directly under this space" className="divide-y rounded-lg border">
            {childrenOf(tree.pages, null).map((page) => (
              <li key={page.path}>
                <Link
                  to={spacePagePath(name!, page.path)}
                  className="flex min-h-10 items-center gap-3 px-3 py-1 hover:bg-accent"
                >
                  <span className="min-w-0 flex-1 truncate">{page.title}</span>
                  <span className="hidden shrink-0 text-xs text-muted-foreground sm:block">
                    {childrenOf(tree.pages, page.path).length > 0
                      ? `${childrenOf(tree.pages, page.path).length} below`
                      : ""}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </div>
    </>
  );
}

/**
 * A space that is not in the list is not there for this caller, and the
 * knowledge base says nothing else: a closed space and one that never existed
 * answer the same, and the existence is half of what is being protected
 * (ADR 0027).
 */
export function Missing({ name }: { name: string }) {
  return (
    <>
      <PageHeader title={name} />
      <div className="m-auto grid max-w-md justify-items-center gap-3 p-8 text-center">
        <p role="status">There is no space under this address.</p>
        <p className="text-sm text-muted-foreground">
          It may have been renamed, or it may be one this account is not named on.
        </p>
        <Link className="text-sm text-brand hover:underline" to="/spaces">
          All spaces
        </Link>
      </div>
    </>
  );
}
