import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route, Routes } from "react-router";
import { afterEach, expect, it, vi } from "vitest";
import { installInstance, renderAt } from "@/shared/testing";
import { EpicView, NewEpicView } from "./EpicView";

afterEach(() => vi.unstubAllGlobals());

const epic = {
  key: "PLAN-E4",
  project: "PLAN",
  title: "The web application",
  description: "React on its own toolchain.",
  status: "open",
  author: { id: "0199a000-0000-7000-8000-000000000001", name: "maintainer" },
  labels: [{ name: "cut-3", group: null, description: null }],
  progress: { total: 3, closed: 1, done: 1, canceled: 0 },
  created_at: "2026-08-01T10:00:00Z",
  updated_at: "2026-08-02T10:00:00Z",
  closed_at: null,
};

function anIssue(key: string, title: string, status = "todo") {
  return {
    key, project: "PLAN", title, status, ready: true, priority: 2, labels: [], epic: "PLAN-E4", parent: null,
    release: null, assignee: null, claim: null, blocked_by: [], open_questions: 0, open_blockers: 0,
    open_sub_issues: 0, created_at: "2026-08-01T10:00:00Z", updated_at: "2026-08-02T10:00:00Z",
    closed_at: null, deleted_at: null, deleted_by: null,
  };
}

const issues = { items: [anIssue("PLAN-43", "The release screens"), anIssue("PLAN-45", "The epic's own view")], total: 2, has_more: false, next_cursor: null };

function renderEpic(routes: Parameters<typeof installInstance>[0] = {}) {
  const instance = installInstance({ "GET /epics/PLAN-E4": { body: epic }, "GET /issues": issues, ...routes });
  renderAt("/PLAN/epics/E4", <Routes><Route path="/:project/epics/:number" element={<EpicView />} /></Routes>);
  return instance;
}

it("opens the epic itself: the living document, the progress and its issues", async () => {
  const instance = renderEpic();

  expect(await screen.findByText("React on its own toolchain.")).toBeInTheDocument();
  expect(screen.getByText("1 of 3 closed")).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "PLAN-43" })).toHaveAttribute("href", "/PLAN/issues/43");
  expect(screen.getByRole("link", { name: "In the issue list" })).toHaveAttribute("href", "/PLAN/issues?epic=PLAN-E4");

  const list = instance.calls.find((call) => new URL(call.url).pathname === "/issues")!;
  expect(new URL(list.url).searchParams.get("epic")).toBe("PLAN-E4");
});

// The matrix: closing an epic with open issues warns but succeeds.
it("warns that the open issues stay workable before closing the epic", async () => {
  const instance = renderEpic({ "POST /epics/PLAN-E4/close": { body: { ...epic, status: "closed", closed_at: "2026-09-04T10:00:00Z" } } });
  const user = userEvent.setup();

  await user.click(await screen.findByRole("button", { name: "Close epic" }));
  const dialog = screen.getByRole("dialog", { name: "Close PLAN-E4?" });
  expect(dialog).toHaveTextContent("2 issues are still open. They stay workable — an epic is a bracket and gates nothing.");

  await user.click(within(dialog).getByRole("button", { name: "Close epic" }));

  expect(await vi.waitFor(() => instance.calls.some((call) => call.method === "POST"))).toBe(true);
  expect(await screen.findByRole("button", { name: "Reopen epic" })).toBeInTheDocument();
  expect(screen.getByText("closed")).toBeInTheDocument();
});

it("edits the living document under If-Match", async () => {
  const instance = renderEpic({ "PATCH /epics/PLAN-E4": { body: { ...epic, description: "Rewritten." } } });
  const user = userEvent.setup();

  await user.click(await screen.findByRole("button", { name: "Edit" }));
  await user.clear(screen.getByLabelText("Description"));
  await user.type(screen.getByLabelText("Description"), "Rewritten.");
  await user.click(screen.getByRole("button", { name: "Save changes" }));

  const request = await vi.waitFor(() => {
    const call = instance.calls.find((entry) => entry.method === "PATCH");
    if (call === undefined) throw new Error("no PATCH yet");
    return call;
  });
  expect(request.headers.get("If-Match")).toBe("2026-08-02T10:00:00Z");
  expect(await request.json()).toEqual({ title: "The web application", description: "Rewritten.", labels: ["cut-3"] });
  expect(await screen.findByText("Rewritten.")).toBeInTheDocument();
});

it("says why an epic its issues still reference cannot be deleted", async () => {
  renderEpic({ "DELETE /epics/PLAN-E4": { status: 422, body: { detail: "2 issues still reference PLAN-E4." } } });
  const user = userEvent.setup();

  await user.click(await screen.findByRole("button", { name: "Delete epic" }));
  const dialog = screen.getByRole("dialog", { name: "Delete PLAN-E4?" });
  await user.click(within(dialog).getByRole("button", { name: "Delete epic" }));

  expect(await within(dialog).findByRole("alert")).toHaveTextContent("2 issues still reference PLAN-E4.");
});

it("offers the way back after a delete", async () => {
  renderEpic({ "DELETE /epics/PLAN-E4": { status: 204 }, "POST /epics/PLAN-E4/restore": { body: epic } });
  const user = userEvent.setup();

  await user.click(await screen.findByRole("button", { name: "Delete epic" }));
  await user.click(within(screen.getByRole("dialog", { name: "Delete PLAN-E4?" })).getByRole("button", { name: "Delete epic" }));

  await user.click(await screen.findByRole("button", { name: "Restore PLAN-E4" }));
  expect(await screen.findByText("React on its own toolchain.")).toBeInTheDocument();
});

it("says what it could not read, and offers the way back to the list", async () => {
  installInstance({ "GET /epics/PLAN-E4": { status: 404, body: { detail: "No epic PLAN-E4." } }, "GET /issues": issues });
  renderAt("/PLAN/epics/E4", <Routes><Route path="/:project/epics/:number" element={<EpicView />} /></Routes>);

  expect(await screen.findByText("No epic PLAN-E4.")).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "All epics" })).toHaveAttribute("href", "/PLAN/epics");
});

it("creates an epic and opens it", async () => {
  const instance = installInstance({ "POST /epics": { status: 201, body: epic } });
  renderAt(
    "/PLAN/epics/new",
    <Routes>
      <Route path="/:project/epics/new" element={<NewEpicView />} />
      <Route path="/:project/epics/:number" element={<p>The epic itself</p>} />
    </Routes>,
  );
  const user = userEvent.setup();

  await user.type(screen.getByLabelText("Title"), "The web application");
  await user.type(screen.getByLabelText("Description"), "React on its own toolchain.");
  await user.type(screen.getByLabelText("Labels", { exact: false }), "cut-3");
  await user.click(screen.getByRole("button", { name: "Create epic" }));

  expect(await screen.findByText("The epic itself")).toBeInTheDocument();
  const request = instance.calls.find((call) => call.method === "POST")!;
  expect(await request.json()).toEqual({
    project: "PLAN",
    title: "The web application",
    description: "React on its own toolchain.",
    labels: ["cut-3"],
  });
});
