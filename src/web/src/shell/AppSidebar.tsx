import { NavLink } from "react-router";
import { SettingsIcon } from "lucide-react";
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
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from "@/components/ui/sidebar";
import { useSession } from "@/session/useSession";
import { viewPath, views } from "./views";

/**
 * The left navigation of ADR 0006: the views of the current project, in two
 * groups. On a phone the same component is the drawer the header button
 * opens — one application, not a reduced one.
 */
export function AppSidebar({ project }: { project: Project | undefined }) {
  const { me } = useSession();
  const { setOpenMobile } = useSidebar();

  const groups = [
    { id: "views", label: "Views" },
    { id: "structure", label: "Structure" },
  ] as const;

  return (
    <Sidebar collapsible="offcanvas">
      <SidebarHeader className="px-3 pt-3">
        <div className="flex items-center gap-2 px-1 text-sm font-semibold">
          <span aria-hidden className="size-4.5 rounded-sm bg-brand" />
          planaffe
        </div>
      </SidebarHeader>

      <SidebarContent>
        <nav aria-label="Views of the project">
        {groups.map((group) => (
          <SidebarGroup key={group.id}>
            <SidebarGroupLabel>{group.label}</SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                {views
                  .filter((view) => view.group === group.id)
                  .map((view) => (
                    <SidebarMenuItem key={view.id}>
                      {project === undefined ? (
                        <SidebarMenuButton disabled>
                          <view.icon />
                          <span>{view.label}</span>
                        </SidebarMenuButton>
                      ) : (
                        <NavLink
                          to={viewPath(project.key, view)}
                          onClick={() => setOpenMobile(false)}
                          className="contents"
                        >
                          {({ isActive }) => (
                            <SidebarMenuButton isActive={isActive} render={<span />}>
                              <view.icon />
                              <span>{view.label}</span>
                            </SidebarMenuButton>
                          )}
                        </NavLink>
                      )}
                    </SidebarMenuItem>
                  ))}
                {group.id === "structure" && project !== undefined && <SidebarMenuItem><NavLink to={`/${project.key}/settings`} onClick={() => setOpenMobile(false)} className="contents">{({ isActive }) => <SidebarMenuButton isActive={isActive} render={<span />}><SettingsIcon /><span>Project settings</span></SidebarMenuButton>}</NavLink></SidebarMenuItem>}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        ))}
        </nav>
      </SidebarContent>

      <SidebarFooter className="px-3 pb-3">
        <div className="truncate px-1 text-xs text-muted-foreground">
          {me.name} · {me.kind}
        </div>
      </SidebarFooter>
    </Sidebar>
  );
}
