import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SessionProvider } from "@/session/Session";
import { aProject, aUser, installInstance, renderAt } from "@/shared/testing";
import { Shell } from "./Shell";
import { views } from "./views";

const other = { ...aProject, key: "LOG", name: "logaffe" };

function anIssue(key: string, title: string, status = "todo") {
  return {
    key,
    project: "PLAN",
    title,
    status,
    ready: true,
    priority: 3,
    labels: ["feature"],
    epic: null,
    assignee: null,
    claim: null,
    blocked_by: [],
    open_questions: 0,
    open_blockers: 0,
    open_sub_issues: 0,
    created_at: "2026-09-02T10:00:00Z",
    updated_at: "2026-09-02T10:00:00Z",
    closed_at: null,
    deleted_at: null,
    deleted_by: null,
  };
}

function shell(path: string) {
  const instance = installInstance({
    "GET /projects": [aProject, other],
    "GET /issues": (request) => {
      const url = new URL(request.url);
      const items = url.searchParams.get("project") === "PLAN" ? [anIssue("PLAN-13", "The web shell")] : [];
      return { items, total: items.length, has_more: false, next_cursor: null };
    },
    "GET /epics": { items: [], total: 0, has_more: false, next_cursor: null },
    "GET /projects/PLAN/labels": [{ name: "feature", group: "kind", description: null }],
  });

  renderAt(
    path,
    <SessionProvider value={{ me: aUser, signOut: vi.fn() }}>
      <Shell />
    </SessionProvider>,
  );

  return instance;
}

afterEach(() => {
  vi.unstubAllGlobals();
  window.localStorage.clear();
});

describe("the shell (ADR 0006)", () => {
  it("shows the seven views of the project in the navigation", async () => {
    shell("/PLAN/ready");

    const navigation = await screen.findByRole("navigation");

    for (const view of views) {
      expect(within(navigation).getByRole("link", { name: view.label })).toHaveAttribute(
        "href",
        `/PLAN/${view.path}`,
      );
    }
  });

  it("frames the list of the view it was opened on, filtered by the view", async () => {
    const { calls } = shell("/PLAN/ready");

    expect(await screen.findByText("The web shell")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Ready for agents" })).toBeInTheDocument();

    const list = calls.find((call) => new URL(call.url).pathname === "/issues")!;
    const query = new URL(list.url).searchParams;
    expect(query.get("project")).toBe("PLAN");
    expect(query.getAll("status")).toEqual(["todo"]);
    expect(query.get("ready")).toBe("true");
  });

  it("lets the URL carry the filter", async () => {
    const { calls } = shell("/PLAN/issues?label=cut-3&status=backlog&status=todo");

    await screen.findByText("The web shell");

    const list = calls.find((call) => new URL(call.url).pathname === "/issues")!;
    const query = new URL(list.url).searchParams;
    expect(query.getAll("label")).toEqual(["cut-3"]);
    expect(query.getAll("status")).toEqual(["backlog", "todo"]);
  });

  it("switches the project and keeps the view", async () => {
    shell("/PLAN/in-progress");
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Switch project" }));
    await user.click(await screen.findByRole("menuitem", { name: /logaffe/ }));

    await waitFor(() =>
      expect(screen.getByRole("navigation").querySelector('a[aria-current="page"]')).toHaveAttribute(
        "href",
        "/LOG/in-progress",
      ),
    );
    expect(window.localStorage.getItem("planaffe.project")).toBe("LOG");
  });

  it("lands on the remembered project from /", async () => {
    window.localStorage.setItem("planaffe.project", "LOG");
    shell("/");

    await waitFor(() =>
      expect(screen.getByRole("navigation").querySelector('a[aria-current="page"]')).toHaveAttribute(
        "href",
        "/LOG/ready",
      ),
    );
  });

  it("opens the palette on ⌘K and jumps to a key typed into it", async () => {
    shell("/PLAN/ready");
    const user = userEvent.setup();
    await screen.findByText("The web shell");

    await user.keyboard("{Meta>}k{/Meta}");
    const box = await screen.findByRole("combobox", { name: /command/i });
    await user.type(box, "plan-13{Enter}");

    expect(await screen.findByRole("heading", { name: /PLAN-13/ })).toBeInTheDocument();
  });

  it("shows who is signed in, top right", async () => {
    shell("/PLAN/ready");
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Account: maintainer" }));

    expect(await screen.findByRole("menuitem", { name: "Sign out" })).toBeInTheDocument();
    expect(screen.getByText(/administrator/)).toBeInTheDocument();
  });
});
