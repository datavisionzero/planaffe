import { lazy, Suspense } from "react";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Link, Route, Routes, useLocation } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import { aProject, installInstance, renderAt } from "@/shared/testing";
import { ScreenErrorBoundary } from "./ScreenErrorBoundary";
import { Shell } from "./Shell";
import { recoverFromStaleChunk } from "./staleChunk";

// The labels screen throws while it renders, as any screen might; everything
// else is the real shell.
vi.mock("@/projects/LabelsView", () => ({
  LabelsView: () => {
    throw new Error("The labels screen broke");
  },
}));

const FailedChunk = lazy(() => Promise.reject(new Error("Release chunk did not load")));

function Routed() {
  const location = useLocation();

  return (
    <>
      <nav>
        <Link to="/PLAN/releases">All releases</Link>
      </nav>
      <ScreenErrorBoundary at={location.pathname}>
        <Routes>
          <Route path="/:project/releases" element={<p>Release list</p>} />
          <Route path="/:project/releases/:name" element={<Suspense fallback={<p>Loading…</p>}><FailedChunk /></Suspense>} />
        </Routes>
      </ScreenErrorBoundary>
    </>
  );
}

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  window.sessionStorage.clear();
});

describe("a screen that fails", () => {
  it("fails alone when its chunk does not load, and the next address tries again", async () => {
    // React reports what a boundary caught; that is the case under test.
    vi.spyOn(console, "error").mockImplementation(() => undefined);
    renderAt("/PLAN/releases/0.11.0", <Routed />);

    expect(await screen.findByText(/This screen could not load/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reload page" })).toBeInTheDocument();

    await userEvent.setup().click(screen.getByRole("link", { name: "All releases" }));

    expect(await screen.findByText("Release list")).toBeInTheDocument();
    expect(screen.queryByText(/This screen could not load/)).toBeNull();
  });

  it("leaves the shell standing when a screen throws, and is gone after navigating", async () => {
    vi.spyOn(console, "error").mockImplementation(() => undefined);
    installInstance({
      "GET /projects": [aProject],
      "GET /epics": { items: [], total: 0, has_more: false, next_cursor: null },
    });
    renderAt("/PLAN/labels", <Shell />);

    expect(await screen.findByRole("heading", { name: "Screen unavailable" })).toBeInTheDocument();

    const navigation = screen.getByRole("navigation");
    await userEvent.setup().click(within(navigation).getByRole("link", { name: /^Epics/ }));

    expect(await screen.findByRole("heading", { name: "Epics" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Screen unavailable" })).toBeNull();
  });
});

describe("a chunk a redeploy took away", () => {
  it("reloads once and not again while the reload is fresh", () => {
    const reload = vi.fn();
    const first = new Event("vite:preloadError", { cancelable: true });

    recoverFromStaleChunk(first, reload, 1_000_000);

    expect(reload).toHaveBeenCalledTimes(1);
    expect(first.defaultPrevented).toBe(true);

    // The page came back and the chunk is still missing: that is broken, not
    // stale, and the error goes on to the screen's boundary.
    const second = new Event("vite:preloadError", { cancelable: true });
    recoverFromStaleChunk(second, reload, 1_003_000);

    expect(reload).toHaveBeenCalledTimes(1);
    expect(second.defaultPrevented).toBe(false);

    // Long after, a new redeploy is a new stale chunk.
    recoverFromStaleChunk(new Event("vite:preloadError", { cancelable: true }), reload, 1_060_000);
    expect(reload).toHaveBeenCalledTimes(2);
  });

  it("does not reload where it cannot remember having done so", () => {
    const reload = vi.fn();
    vi.spyOn(window, "sessionStorage", "get").mockImplementation(() => {
      throw new DOMException("blocked", "SecurityError");
    });

    recoverFromStaleChunk(new Event("vite:preloadError", { cancelable: true }), reload, 1_000_000);

    expect(reload).not.toHaveBeenCalled();
  });
});
