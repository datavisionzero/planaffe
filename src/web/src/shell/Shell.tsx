import { CommandIcon } from "lucide-react";
import { lazy, Suspense, useEffect, useState } from "react";
import { matchPath, Navigate, Route, Routes, useLocation } from "react-router";
import { Button } from "@/components/ui/button";
import { Kbd } from "@/components/ui/kbd";
import { Separator } from "@/components/ui/separator";
import { SidebarInset, SidebarProvider, SidebarTrigger } from "@/components/ui/sidebar";
import { EpicsView } from "@/epics/EpicsView";
import { IssueListView } from "@/issues/IssueListView";
import { NewIssueView } from "@/issues/IssueEditor";
import { LabelsView } from "@/projects/LabelsView";
import { ProjectSwitcher } from "@/projects/ProjectSwitcher";
import { NewProjectView } from "@/projects/NewProjectView";
import { lastProject, rememberProject, useProjects } from "@/projects/useProjects";
import { ReleasesView } from "@/releases/ReleasesView";
import { SettingsView } from "@/settings/SettingsView";
import { AdminView } from "@/settings/AdminView";
import { ProjectSettingsView } from "@/settings/ProjectSettingsView";
import { AccountMenu } from "./AccountMenu";
import { AppSidebar } from "./AppSidebar";
import { Palette } from "./Palette";
import { views } from "./views";

// The Markdown pipeline of ADR 0007 weighs more than the shell; it arrives
// with the first issue opened, not with the frame.
const IssueView = lazy(() => import("@/issues/IssueView").then((module) => ({ default: module.IssueView })));

/**
 * The application shell of ADR 0006: the frame every screen sits in, rendered
 * before any data arrives and never remounted by navigation. The current
 * project is read from the URL — `/:project/…` — so that the frame and the
 * screen agree without either telling the other.
 */
export function Shell() {
  const location = useLocation();
  const projects = useProjects();
  const [paletteOpen, setPaletteOpen] = useState(false);

  const match = matchPath("/:project/*", location.pathname);
  const projectKey = match?.params.project;
  const viewPath = match?.params["*"] ?? "ready";

  const current =
    projects.at === "known" && projectKey !== undefined
      ? projects.projects.find((project) => project.key === projectKey)
      : undefined;

  useEffect(() => {
    if (current !== undefined) {
      rememberProject(current.key);
    }
  }, [current]);

  useEffect(() => {
    function onKeyDown(event: globalThis.KeyboardEvent) {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        setPaletteOpen((open) => !open);
      }
    }

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  const known = projects.at === "known" ? projects.projects : [];

  return (
    <SidebarProvider>
      <AppSidebar project={current} />
      <SidebarInset>
        <header className="flex h-12 shrink-0 items-center gap-2 border-b px-3">
          <SidebarTrigger className="md:hidden" />
          <Separator orientation="vertical" className="mr-1 h-4! md:hidden" />
          <ProjectSwitcher projects={known} current={current} viewPath={viewPath.split("/")[0] || "ready"} />
          <div className="flex-1" />
          <Button
            variant="outline"
            size="sm"
            className="hidden gap-2 text-muted-foreground sm:flex"
            onClick={() => setPaletteOpen(true)}
          >
            <CommandIcon className="size-3.5" />
            <span className="text-xs">Search or jump…</span>
            <Kbd>⌘K</Kbd>
          </Button>
          <Button
            variant="ghost"
            size="icon-sm"
            className="sm:hidden"
            aria-label="Command palette"
            onClick={() => setPaletteOpen(true)}
          >
            <CommandIcon />
          </Button>
          <AccountMenu />
        </header>

        <Routes>
          <Route path="/" element={<Landing projects={projects} />} />
          <Route path="/settings/*" element={<SettingsView />} />
          <Route path="/admin" element={<AdminView />} />
          <Route path="/projects/new" element={<NewProjectView />} />
          <Route path="/:project">
            <Route index element={<Navigate to="ready" replace />} />
            {views
              .filter((view) => view.filter !== undefined)
              .map((view) => (
                <Route key={view.id} path={view.path} element={<IssueListView view={view} />} />
              ))}
            <Route path="issues/new" element={<NewIssueView />} />
            <Route path="issues/:key" element={<Suspense fallback={null}><IssueView /></Suspense>} />
            <Route path="epics" element={<EpicsView />} />
            <Route path="epics/:key" element={<EpicsView />} />
            <Route path="releases" element={<ReleasesView />} />
            <Route path="labels" element={<LabelsView />} />
            <Route path="settings" element={<ProjectSettingsView />} />
          </Route>
        </Routes>
      </SidebarInset>

      <Palette open={paletteOpen} onOpenChange={setPaletteOpen} projects={known} current={current} />
    </SidebarProvider>
  );
}

/**
 * `/` is nowhere: it lands in the project the user was in last, or the first
 * one, in "Ready for agents" — the view the product is about.
 */
function Landing({ projects }: { projects: ReturnType<typeof useProjects> }) {
  if (projects.at === "asking") {
    return null;
  }

  if (projects.at === "failed") {
    return <Empty title="The projects could not be loaded." />;
  }

  const remembered = lastProject();
  const target = projects.projects.find((project) => project.key === remembered) ?? projects.projects[0];

  if (target === undefined) {
    return (
      <Empty title="No project yet.">
        <code className="font-mono">pa project create --key PLAN --name "…"</code> makes the first one.
      </Empty>
    );
  }

  return <Navigate to={`/${target.key}/ready`} replace />;
}

export function Empty({ title, children }: { title: string; children?: React.ReactNode }) {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-2 p-8 text-center">
      <p className="font-medium">{title}</p>
      {children !== undefined && <p className="text-sm text-muted-foreground">{children}</p>}
    </div>
  );
}
