import { BotOffIcon } from "lucide-react";
import { Link } from "react-router";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { PageHeader } from "@/shared/PageHeader";
import { spacePath } from "@/shell/views";
import { useSpaceList } from "./useSpaces";

/**
 * The spaces the caller may see — the door of the knowledge base. A space
 * closed to agents says so in its row: a switch that is only visible in the
 * settings of the space is one nobody remembers, and this one is what somebody
 * writing a personnel matter into a page is relying on (ADR 0027).
 */
export function SpacesView() {
  const { spaces } = useSpaceList();

  return (
    <>
      <PageHeader title="Spaces" meta={spaces.at === "known" ? `${spaces.spaces.length}` : undefined} />

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
