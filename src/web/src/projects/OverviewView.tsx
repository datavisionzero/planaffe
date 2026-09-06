import { Link } from "react-router";
import { PageHeader } from "@/shared/PageHeader";
import { cn } from "@/lib/utils";
import { counts, look, reason, type ProjectStanding } from "./standing";
import { useOverview } from "./useOverview";

/**
 * The overview (`docs/human-interface.md`): one tile per project, saying how
 * that project stands and why. The one screen of the application that answers
 * across projects, and what `/` lands on.
 *
 * The instance decides the step and the order (ADR 0024); this screen draws
 * them. Worst first, so that what needs a human is the first thing under the
 * reader's eye — and the whole tile is the link, leading at the reason rather
 * than merely at the project.
 */
export function OverviewView() {
  const { overview, reload } = useOverview();

  return (
    <>
      <PageHeader
        title="Overview"
        meta={overview.at === "known" ? `${overview.projects.length}` : undefined}
      />

      {overview.at === "asking" && (
        <p role="status" aria-busy className="p-4 text-sm text-muted-foreground">Looking at your projects…</p>
      )}

      {overview.at === "failed" && (
        <div className="space-y-2 p-4 text-sm">
          <p className="text-destructive">{overview.why}</p>
          <button type="button" className="text-brand underline-offset-4 hover:underline" onClick={reload}>
            Try again
          </button>
        </div>
      )}

      {overview.at === "known" && overview.projects.length === 0 && (
        <div className="flex flex-1 flex-col items-center justify-center gap-2 p-8 text-center">
          <p className="font-medium">No project yet.</p>
          <p className="text-sm text-muted-foreground">
            <Link to="/projects/new" className="text-brand underline-offset-4 hover:underline">Create one</Link>
            {" "}and it appears here.
          </p>
        </div>
      )}

      {overview.at === "known" && overview.projects.length > 0 && (
        <div className="p-4">
          {/* One number for the instance, not one per tile: with no agent,
              nothing anywhere is picked up, and creating one is a single act
              that has nothing to do with any project on this screen. */}
          {overview.agents === 0 && (
            <p className="mb-3 text-sm text-muted-foreground">
              No agent can take work on this instance.{" "}
              <Link to="/settings/agents" className="text-brand underline-offset-4 hover:underline">Create an agent</Link>.
            </p>
          )}
          <ul className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {overview.projects.map((project) => <Tile key={project.key} project={project} now={overview.read} />)}
          </ul>
        </div>
      )}
    </>
  );
}

function Tile({ project, now }: { project: ProjectStanding; now: number }) {
  const drawn = look[project.standing];
  const Icon = drawn.icon;
  const to = drawn.view === null ? `/${project.key}` : `/${project.key}/${drawn.view}`;
  const said = counts(project);

  return (
    <li>
      <Link
        to={to}
        className="flex h-full flex-col gap-2 rounded-lg border bg-card p-3 transition-colors hover:bg-accent"
        style={{ borderLeftColor: `var(--${drawn.tone})`, borderLeftWidth: "3px" }}
      >
        <div className="flex items-start gap-2">
          <span className="font-mono text-xs font-medium tracking-wide text-brand">{project.key}</span>
          <span className="min-w-0 flex-1 truncate text-sm font-medium">{project.name}</span>
          {/* The icon is decoration: the word beside it says the same thing,
              so a reader who cannot see the colour or the glyph loses nothing. */}
          <span aria-hidden className="flex shrink-0 gap-0.5" style={{ color: `var(--${drawn.tone})` }}>
            {Array.from({ length: drawn.icons }, (_, at) => <Icon key={at} className="size-4" />)}
          </span>
        </div>
        <div>
          <p className="text-sm font-medium" style={{ color: `var(--${drawn.tone})` }}>{drawn.word}</p>
          <p className="text-xs text-muted-foreground">{reason(project, now)}</p>
        </div>
        {said !== null && (
          <p className={cn("mt-auto border-t pt-2 text-xs text-muted-foreground", "font-mono")}>{said}</p>
        )}
      </Link>
    </li>
  );
}
