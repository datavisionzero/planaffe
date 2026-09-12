import { CommandIcon } from "lucide-react";
import { lazy, Suspense, useEffect, useState } from "react";
import { matchPath, Navigate, Route, Routes, useLocation, useNavigate } from "react-router";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { SidebarInset, SidebarProvider, SidebarTrigger } from "@/components/ui/sidebar";
import { EpicsView } from "@/epics/EpicsView";
import { IssueListView } from "@/issues/IssueListView";
import { NeedsYouView } from "@/issues/NeedsYouView";
import { PagesView } from "@/pages/PagesView";
import { LabelsView } from "@/projects/LabelsView";
import { ProjectSwitcher } from "@/projects/ProjectSwitcher";
import { NewProjectView } from "@/projects/NewProjectView";
import { OverviewView } from "@/projects/OverviewView";
import { ProjectsContext } from "@/projects/context";
import { useProjects, type Projects } from "@/projects/useProjects";
import { ReleasesView } from "@/releases/ReleasesView";
import { SettingsView } from "@/settings/SettingsView";
import { SpacesContext, TreeContext } from "@/spaces/context";
import { SpaceSwitcher } from "@/spaces/SpaceSwitcher";
import { SpacesView } from "@/spaces/SpacesView";
import { SpaceView } from "@/spaces/SpaceView";
import { useSpaceState, useTreeState } from "@/spaces/useSpaces";
import { AdminView } from "@/settings/AdminView";
import { ProjectSettingsView } from "@/settings/ProjectSettingsView";
import { AccountMenu } from "./AccountMenu";
import { AppSidebar, type Area } from "./AppSidebar";
import { AttentionContext } from "./attention";
import { useAttentionState } from "./useAttention";
import { Palette } from "./Palette";
import { Keys, ShortcutsDialog } from "./ShortcutsDialog";
import { is, overlaid, typing } from "./shortcuts";
import { views } from "./views";

// The Markdown pipeline of ADR 0007 weighs more than the shell; it arrives
// with the first issue, epic or release opened, not with the frame.
//
// `NewIssueView` belongs in this list and was the one screen that escaped it.
// It is imported statically nowhere any more: through `IssueEditor` it reaches
// `MarkdownField` and with it the whole pipeline, so a static import here put
// the pipeline in the frame's own graph — the build then had to fetch it
// before the shell rendered, on every screen, including the ones that never
// show Markdown. The paragraph above said otherwise, and so did
// `docs/human-interface.md`.
const IssueView = lazy(() => import("@/issues/IssueView").then((module) => ({ default: module.IssueView })));
const NewIssueView = lazy(() => import("@/issues/IssueEditor").then((module) => ({ default: module.NewIssueView })));
const EpicView = lazy(() => import("@/epics/EpicView").then((module) => ({ default: module.EpicView })));
const NewEpicView = lazy(() => import("@/epics/EpicView").then((module) => ({ default: module.NewEpicView })));
const ReleaseView = lazy(() => import("@/releases/ReleaseView").then((module) => ({ default: module.ReleaseView })));
const PageView = lazy(() => import("@/pages/PageView").then((module) => ({ default: module.PageView })));
const SpacePageView = lazy(() => import("@/spaces/SpacePageView").then((module) => ({ default: module.SpacePageView })));
const NewSpacePageView = lazy(() => import("@/spaces/NewSpacePageView").then((module) => ({ default: module.NewSpacePageView })));
const NewPageView = lazy(() => import("@/pages/PageView").then((module) => ({ default: module.NewPageView })));

/**
 * The application shell of ADR 0006: the frame every screen sits in, rendered
 * before any data arrives and never remounted by navigation. The current
 * project is read from the URL — `/:project/…` — so that the frame and the
 * screen agree without either telling the other.
 */
export function Shell() {
  const location = useLocation();
  const navigate = useNavigate();
  const list = useProjects();
  const projects = list.projects;
  const spaceList = useSpaceState();
  const spaces = spaceList.spaces;
  const [paletteOpen, setPaletteOpen] = useState(false);
  const [switcherOpen, setSwitcherOpen] = useState(false);
  const [spacesOpen, setSpacesOpen] = useState(false);
  const [shortcutsOpen, setShortcutsOpen] = useState(false);

  const match = matchPath("/:project/*", location.pathname);
  const projectKey = match?.params.project;
  const viewPath = match?.params["*"] ?? "ready";
  const currentView = viewPath.split("/")[0] || "ready";

  const current =
    projects.at === "known" && projectKey !== undefined
      ? projects.projects.find((project) => project.key === projectKey)
      : undefined;

  // Which of the two areas the frame is standing in (VISION 18). It is read
  // from the address like everything else about the frame: the knowledge base
  // begins at `/spaces`, and nothing else in the application starts there.
  const inSpace = matchPath("/spaces/:name/*", location.pathname) ?? matchPath("/spaces/:name", location.pathname);
  const knowledge = location.pathname === "/spaces" || inSpace !== null;
  const spaceName = inSpace?.params.name;
  const openPage = matchPath("/spaces/:name/pages/*", location.pathname)?.params["*"];
  const space =
    spaces.at === "known" && spaceName !== undefined
      ? spaces.spaces.find((one) => one.name === spaceName)
      : undefined;

  // The tree of the open space, read once for the frame and the screens
  // together. Outside the knowledge base nothing is read at all.
  const pageTree = useTreeState(spaceName);

  const area: Area = knowledge
    ? { at: "knowledge", space, name: spaceName, tree: pageTree.tree, path: openPage }
    : { at: "tracker", project: current };

  // The keys the frame itself owns, read from `shortcuts.ts` so that this
  // handler and the overview it feeds cannot come apart. `p`, `?` and `c` are
  // bare keys on purpose: ⌘P is the browser's print, and taking printing away
  // from an issue tracker costs more than the switcher gains. Bare keys are
  // what the lists already use — `j`, `k`, `/` — so they join that alphabet
  // instead of fighting the browser for a modifier.
  useEffect(() => {
    function onKeyDown(event: globalThis.KeyboardEvent) {
      if (is("global:palette", event)) {
        event.preventDefault();
        // One dialog at a time: the palette arrives over whatever the overview
        // was explaining, not behind it.
        setShortcutsOpen(false);
        setPaletteOpen((open) => !open);
        return;
      }

      // Not while something is being typed, and not while a menu or a dialog
      // has the focus — those close with Escape, as they always did.
      if (typing(event) || overlaid(event)) {
        return;
      }

      if (is("global:projects", event)) {
        event.preventDefault();
        setSwitcherOpen(true);
      } else if (is("global:spaces", event) && knowledge) {
        // The key of the area it belongs to, the way `c` is the key of a
        // project: in the tracker there is no space switcher to open, and a
        // shortcut that answers where its subject is not would be a surprise
        // rather than a shortcut.
        event.preventDefault();
        setSpacesOpen(true);
      } else if (is("global:shortcuts", event)) {
        event.preventDefault();
        setShortcutsOpen(true);
      } else if (is("global:create", event) && current !== undefined) {
        // The project the frame is standing in, not the one in the address:
        // `/settings` matches `/:project/*` too, and nothing is created there.
        event.preventDefault();
        void navigate(`/${current.key}/issues/new`);
      }
    }

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [current, knowledge, navigate]);

  // How much is waiting for a human, held once for the frame: the sidebar
  // draws the number and the "Needs you" screen shares the wake pulse that
  // keeps it current (epic "Attention").
  const attention = useAttentionState(current?.key);

  const known = projects.at === "known" ? projects.projects : [];

  return (
    // The list and the way back to it (ADR 0006: the shell is not remounted by
    // navigation): a screen that adds a project asks the frame to catch up
    // rather than leaving it on a list the new project is not in.
    <ProjectsContext.Provider value={list}>
    <SpacesContext.Provider value={spaceList}>
    <TreeContext.Provider value={pageTree}>
    <AttentionContext.Provider value={attention}>
    <SidebarProvider>
      <AppSidebar area={area} />
      <SidebarInset>
        <header className="flex h-12 shrink-0 items-center gap-2 border-b px-3">
          <SidebarTrigger className="md:hidden" />
          <Separator orientation="vertical" className="mr-1 h-4! md:hidden" />
          {/* The bracket of the area the reader is in, and only that one:
              there is no project in the knowledge base and no space in the
              tracker, so the header carries one switcher and never two. */}
          {knowledge ? (
            <SpaceSwitcher
              spaces={spaces}
              current={space}
              open={spacesOpen}
              onOpenChange={setSpacesOpen}
              reload={spaceList.reload}
            />
          ) : (
            <ProjectSwitcher
              projects={projects}
              current={current}
              viewPath={currentView}
              open={switcherOpen}
              onOpenChange={setSwitcherOpen}
              reload={list.reload}
            />
          )}
          <div className="flex-1" />
          <Button
            variant="outline"
            size="sm"
            className="hidden gap-2 text-muted-foreground sm:flex"
            onClick={() => setPaletteOpen(true)}
          >
            <CommandIcon className="size-3.5" />
            <span className="text-xs">Search or jump…</span>
            <Keys id="global:palette" />
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
          <AccountMenu onShortcuts={() => setShortcutsOpen(true)} />
        </header>

        <Routes>
          <Route path="/" element={<Landing projects={projects} />} />
          <Route path="/settings/*" element={<SettingsView />} />
          <Route path="/admin/*" element={<AdminView />} />
          <Route path="/projects" element={<OverviewView />} />
          <Route path="/spaces" element={<SpacesView />} />
          <Route path="/spaces/:name" element={<SpaceView />} />
          <Route path="/spaces/:name/new" element={<Suspense fallback={<Busy title="Loading the screen…" />}><NewSpacePageView /></Suspense>} />
          <Route path="/spaces/:name/pages/*" element={<Suspense fallback={<Busy title="Loading the screen…" />}><SpacePageView /></Suspense>} />
          <Route path="/projects/new" element={<NewProjectView />} />
          <Route path="/:project">
            <Route index element={<Navigate to="ready" replace />} />
            {views
              .filter((view) => view.filter !== undefined)
              .map((view) => (
                <Route key={view.id} path={view.path} element={<IssueListView view={view} />} />
              ))}
            <Route path="needs-you" element={<NeedsYouView />} />
            <Route path="issues/new" element={<Suspense fallback={<Busy title="Loading the screen…" />}><NewIssueView /></Suspense>} />
            <Route path="issues/:number" element={<Suspense fallback={<Busy title="Loading the screen…" />}><IssueView /></Suspense>} />
            <Route path="epics" element={<EpicsView />} />
            <Route path="epics/new" element={<Suspense fallback={<Busy title="Loading the screen…" />}><NewEpicView /></Suspense>} />
            <Route path="epics/:number" element={<Suspense fallback={<Busy title="Loading the screen…" />}><EpicView /></Suspense>} />
            <Route path="pages" element={<PagesView />} />
            <Route path="pages/new" element={<Suspense fallback={<Busy title="Loading the screen…" />}><NewPageView /></Suspense>} />
            <Route path="pages/:slug" element={<Suspense fallback={<Busy title="Loading the screen…" />}><PageView /></Suspense>} />
            <Route path="releases" element={<ReleasesView />} />
            <Route path="releases/:name" element={<Suspense fallback={<Busy title="Loading the screen…" />}><ReleaseView /></Suspense>} />
            <Route path="labels" element={<LabelsView />} />
            <Route path="settings/*" element={<ProjectSettingsView />} />
          </Route>
        </Routes>
      </SidebarInset>

      <Palette
        open={paletteOpen}
        onOpenChange={setPaletteOpen}
        projects={known}
        current={current}
        spaces={spaces.at === "known" ? spaces.spaces : []}
        space={space}
        pages={pageTree.tree.at === "known" ? pageTree.tree.pages : []}
        onShortcuts={() => setShortcutsOpen(true)}
      />
      <ShortcutsDialog open={shortcutsOpen} onOpenChange={setShortcutsOpen} />
    </SidebarProvider>
    </AttentionContext.Provider>
    </TreeContext.Provider>
    </SpacesContext.Provider>
    </ProjectsContext.Provider>
  );
}

/**
 * `/` is the overview: how every project stands, worst first
 * (`docs/human-interface.md`).
 *
 * With one project it is not. An overview of a single tile is decoration, and
 * the reader who has one project wants the same thing every time — so they are
 * taken straight into it, in "Ready for agents", the view the product is
 * about. The empty instance keeps the sentence it always had, because there is
 * nothing to be an overview of.
 */
function Landing({ projects }: { projects: Projects }) {
  if (projects.at === "asking") {
    return <Busy title="Looking for your projects…" />;
  }

  if (projects.at === "failed") {
    return <Empty title="The projects could not be loaded." />;
  }

  if (projects.projects.length === 0) {
    return (
      <Empty title="No project yet.">
        <code className="font-mono">pa project create --key PLAN --name "…"</code> makes the first one.
      </Empty>
    );
  }

  if (projects.projects.length === 1) {
    return <Navigate to={`/${projects.projects[0]!.key}/ready`} replace />;
  }

  return <Navigate to="/projects" replace />;
}

/**
 * What the frame shows while a screen or the list behind it is still on its
 * way. Never a blank page, which `docs/human-interface.md` asks for, and never
 * silent to a screen reader.
 */
export function Busy({ title }: { title: string }) {
  return (
    <div aria-busy className="flex flex-1 flex-col items-center justify-center gap-2 p-8 text-center">
      <span aria-hidden className="size-4.5 animate-pulse rounded-sm bg-brand" />
      <p role="status" className="text-sm text-muted-foreground">
        {title}
      </p>
    </div>
  );
}

export function Empty({ title, children }: { title: string; children?: React.ReactNode }) {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-2 p-8 text-center">
      <p className="font-medium">{title}</p>
      {children !== undefined && <p className="text-sm text-muted-foreground">{children}</p>}
    </div>
  );
}
