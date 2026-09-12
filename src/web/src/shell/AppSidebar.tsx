import { NavLink, useLocation } from "react-router";
import { LibraryIcon, SettingsIcon, SquareKanbanIcon } from "lucide-react";
import type { Project } from "@/api/client";
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuBadge,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from "@/components/ui/sidebar";
import { useSession } from "@/session/useSession";
import type { Space, Tree } from "@/spaces/context";
import { SpaceNav } from "@/spaces/SpaceNav";
import type { Attention } from "./attention";
import { useAttention } from "./useAttention";
import { spacePath, viewPath, views } from "./views";

/**
 * Which of the two areas the frame is standing in (VISION 18). One application
 * and one shell, and the navigation is what tells them apart: the tracker's
 * views on one side, a space's page tree on the other, and never both at once.
 */
export type Area =
  | { at: "tracker"; project: Project | undefined }
  | { at: "knowledge"; space: Space | undefined; name: string | undefined; tree: Tree; path: string | undefined };

/**
 * The left navigation of ADR 0006. On a phone the same component is the drawer
 * the header button opens — one application, not a reduced one.
 */
export function AppSidebar({ area }: { area: Area }) {
  const { me } = useSession();
  const { setOpenMobile } = useSidebar();
  const { pathname } = useLocation();
  const close = () => setOpenMobile(false);

  return (
    <Sidebar collapsible="offcanvas">
      <SidebarHeader className="px-3 pt-3">
        <div className="flex items-center gap-2 px-1 text-sm font-semibold">
          <span aria-hidden className="size-4.5 rounded-sm bg-brand" />
          planaffe
        </div>
      </SidebarHeader>

      <SidebarContent>
        {area.at === "tracker" ? (
          <ProjectNav project={area.project} pathname={pathname} onWalk={close} />
        ) : (
          <KnowledgeNav area={area} pathname={pathname} onWalk={close} />
        )}
      </SidebarContent>

      <SidebarFooter className="px-3 pb-3">
        {/* The one way across the border, in the one place it can be reached
            from every screen of either area. The tracker has no space in it
            and the knowledge base no project, so this is a door and never a
            view of the other side. */}
        <SidebarMenu>
          <SidebarMenuItem>
            {area.at === "tracker" ? (
              <SidebarMenuButton render={<NavLink to="/spaces" onClick={close} />}>
                <LibraryIcon />
                <span>Knowledge base</span>
              </SidebarMenuButton>
            ) : (
              // Back to the landing rather than to a remembered project: it
              // takes a reader with one project into it and everybody else to
              // the overview, which is the same answer `/` always gives.
              <SidebarMenuButton render={<NavLink to="/" onClick={close} />}>
                <SquareKanbanIcon />
                <span>Tracker</span>
              </SidebarMenuButton>
            )}
          </SidebarMenuItem>
        </SidebarMenu>
        <div className="truncate px-1 text-xs text-muted-foreground">
          {me.name} · {me.kind}
        </div>
      </SidebarFooter>
    </Sidebar>
  );
}

/** The views of the current project, in two groups. */
function ProjectNav({ project, pathname, onWalk }: { project: Project | undefined; pathname: string; onWalk: () => void }) {
  const attention = useAttention();

  const groups = [
    { id: "views", label: "Views" },
    { id: "structure", label: "Structure" },
  ] as const;

  return (
    <nav aria-label="Views of the project">
      {groups.map((group) => (
        <SidebarGroup key={group.id}>
          <SidebarGroupLabel>{group.label}</SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>
              {views
                .filter((view) => view.group === group.id)
                .map((view) => {
                  const path = project === undefined ? "" : viewPath(project.key, view);
                  const count = drawn(counted(view.id, attention));
                  return <SidebarMenuItem key={view.id}>
                    {project === undefined ? (
                      <SidebarMenuButton disabled>
                        <view.icon />
                        <span>{view.label}</span>
                      </SidebarMenuButton>
                    ) : (
                      <SidebarMenuButton
                        isActive={pathname === path || pathname.startsWith(`${path}/`)}
                        // The count belongs to the name of the link, not
                        // beside it: a screen reader says "Needs you, 3"
                        // rather than reading two fragments in a row.
                        aria-label={count === null ? undefined : `${view.label}, ${count}`}
                        render={<NavLink to={path} onClick={onWalk} />}
                      >
                        <view.icon />
                        <span>{view.label}</span>
                      </SidebarMenuButton>
                    )}
                    {count !== null && project !== undefined && <SidebarMenuBadge aria-hidden>{count}</SidebarMenuBadge>}
                  </SidebarMenuItem>;
                })}
              {group.id === "structure" && project !== undefined && <SidebarMenuItem><SidebarMenuButton isActive={pathname === `/${project.key}/settings`} render={<NavLink to={`/${project.key}/settings`} onClick={onWalk} />}><SettingsIcon /><span>Project settings</span></SidebarMenuButton></SidebarMenuItem>}
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      ))}
    </nav>
  );
}

/**
 * The knowledge base's navigation: the way back to the spaces, and — inside a
 * space — its page tree, which is the navigation of the area rather than a
 * list of links beside one (VISION 18). The counts of the tracker are not
 * here, and neither is the connection behind them: they are about work that is
 * waiting, and nothing here is waiting for anybody.
 */
function KnowledgeNav({
  area,
  pathname,
  onWalk,
}: {
  area: Extract<Area, { at: "knowledge" }>;
  pathname: string;
  onWalk: () => void;
}) {
  return (
    <nav aria-label="The knowledge base">
      <SidebarGroup>
        <SidebarGroupLabel>Knowledge base</SidebarGroupLabel>
        <SidebarGroupContent>
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton isActive={pathname === "/spaces"} render={<NavLink to="/spaces" onClick={onWalk} />}>
                <LibraryIcon />
                <span>Spaces</span>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarGroupContent>
      </SidebarGroup>

      {area.name !== undefined && (
        <SidebarGroup>
          <SidebarGroupLabel>{area.space?.title ?? area.name}</SidebarGroupLabel>
          <SidebarGroupContent>
            <SpaceNav space={area.name} tree={area.tree} path={area.path} />
            <SidebarMenu>
              <SidebarMenuItem>
                <SidebarMenuButton
                  isActive={pathname.startsWith(`${spacePath(area.name)}/settings`)}
                  render={<NavLink to={`${spacePath(area.name)}/settings`} onClick={onWalk} />}
                >
                  <SettingsIcon />
                  <span>Space settings</span>
                </SidebarMenuButton>
              </SidebarMenuItem>
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      )}
    </nav>
  );
}

/**
 * Which of the frame's numbers a link carries, if any. "Needs you" is the
 * request and "In progress" the observation; the other links carry none —
 * a number that is always there and barely moves is decoration, and it would
 * take the attention away from the two that mean something.
 */
function counted(id: string, attention: Attention): number | null {
  if (id === "needs-you") {
    return attention.needsYou;
  }

  return id === "in-progress" ? attention.inProgress : null;
}

/**
 * What the badge says, or nothing at all. Zero is not a signal, and an unknown
 * number is not a zero — in both cases the link carries no badge, and it
 * carries no placeholder while the first answer is on its way either. Past a
 * hundred the exact number stops mattering and the width starts to.
 */
function drawn(count: number | null): string | null {
  if (count === null || count <= 0) {
    return null;
  }

  return count > 99 ? "99+" : String(count);
}
