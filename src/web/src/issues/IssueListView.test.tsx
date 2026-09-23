import { screen, waitFor, within } from "@testing-library/react";
import { useState, type ReactNode } from "react";
import userEvent from "@testing-library/user-event";
import { Route, Routes, useParams } from "react-router";
import { afterEach, expect, it, vi } from "vitest";
import { installInstance, renderAt } from "@/shared/testing";
import { views } from "@/shell/views";
import { AttentionContext } from "@/shell/attention";
import { IssueListView } from "./IssueListView";

afterEach(() => vi.unstubAllGlobals());

const all = views.find((view) => view.id === "all")!;

function WithPulse({ children }: { children: ReactNode }) {
  const [issuesPulse, setIssuesPulse] = useState(0);
  return <AttentionContext.Provider value={{ needsYou: null, inProgress: null, pulse: 0, issuesPulse }}>
    <button onClick={() => setIssuesPulse((value) => value + 1)}>Remote change</button>{children}
  </AttentionContext.Provider>;
}

function anIssue(key: string, title: string, extra: Record<string, unknown> = {}) {
  return {
    key, project: "PLAN", title, status: "todo", ready: false, priority: 2, labels: [], epic: null, parent: null,
    release: null, assignee: null, claim: null, blocked_by: [], open_questions: 0, open_blockers: 0,
    open_sub_issues: 0, created_at: "2026-08-01T10:00:00Z", updated_at: "2026-08-02T10:00:00Z",
    closed_at: null, deleted_at: null, deleted_by: null, ...extra,
  };
}

const onePage = { items: [anIssue("PLAN-1", "The first one")], total: 1, has_more: false, next_cursor: null };

it("refreshes a filtered list and its count after a remote change", async () => {
  let answer = onePage;
  installInstance({
    "GET /issues": () => answer,
    "GET /projects/PLAN/labels": [],
    "GET /epics": { items: [], total: 0, has_more: false, next_cursor: null },
  });
  renderAt("/PLAN/issues?status=todo", <WithPulse><Routes><Route path="/:project/:view" element={<IssueListView view={all} />} /></Routes></WithPulse>);
  const user = userEvent.setup();

  await screen.findByText("The first one");
  expect(screen.getByText("1 issue")).toBeInTheDocument();
  answer = { ...onePage, items: [], total: 0 };
  await user.click(screen.getByRole("button", { name: "Remote change" }));
  expect(await screen.findByText("No issues match these filters.")).toBeInTheDocument();
  expect(screen.getByText("0 issues")).toBeInTheDocument();
});

const everyStep = {
  items: [0, 1, 2, 3, 4].map((priority) => anIssue(`PLAN-${priority + 1}`, `Step ${priority}`, { priority })),
  total: 5, has_more: false, next_cursor: null,
};

const anAgent = {
  id: "0199a000-0000-7000-8000-000000000009",
  kind: "agent", name: "builder", owner: { id: "u", kind: "user", name: "maintainer" },
  created_at: "2026-09-02T10:00:00Z", token: { prefix: "pa_wxyz", created_at: "2026-09-02T10:00:00Z" },
  metadata: null, metadata_reported_at: null,
};

function renderList(routes: Record<string, unknown> = {}, path = `/PLAN/${all.path}`) {
  const instance = installInstance({
    "GET /issues": onePage,
    "GET /projects/PLAN/labels": [],
    "GET /epics": { items: [{ key: "PLAN-E1", project: "PLAN", title: "The shell", status: "open" }], total: 1, has_more: false, next_cursor: null },
    "GET /projects/PLAN/users": [{ id: "u", kind: "user", name: "maintainer", administrator: true, email: "maintainer@example.test" }],
    "GET /agents": [anAgent],
    ...routes,
  });

  renderAt(path, <Routes><Route path="/:project/:view" element={<IssueListView view={all} />} /></Routes>);

  return instance;
}

it("shows explicit filters and removes one label while preserving the rest and sort", async () => {
  const instance = renderList({}, "/PLAN/issues?label=bug&label=chore&priority=3&sort=created");
  const user = userEvent.setup();
  await screen.findByText("The first one");

  const filters = screen.getByLabelText("Active filters");
  expect(within(filters).getByRole("button", { name: "Remove Label: bug" })).toBeInTheDocument();
  expect(within(filters).getByRole("button", { name: "Remove Label: chore" })).toBeInTheDocument();
  expect(within(filters).getByRole("button", { name: "Remove Priority: high" })).toBeInTheDocument();
  await user.click(within(filters).getByRole("button", { name: "Remove Label: bug" }));

  expect(within(filters).queryByRole("button", { name: "Remove Label: bug" })).not.toBeInTheDocument();
  expect(within(filters).getByRole("button", { name: "Remove Label: chore" })).toBeInTheDocument();
  expect(screen.getByRole("combobox", { name: "Sort issues" })).toHaveValue("created");
  await waitFor(() => {
    const calls = instance.calls.filter((call) => new URL(call.url).pathname === "/issues");
    const query = new URL(calls.at(-1)!.url).searchParams;
    expect(query.getAll("label")).toEqual(["chore"]);
    expect(query.get("priority_min")).toBe("3");
    expect(query.get("sort")).toBe("created");
  });
});

it("keeps view defaults separate and clears added filters from an empty result", async () => {
  const ready = views.find((view) => view.id === "ready")!;
  const empty = { items: [], total: 0, has_more: false, next_cursor: null };
  const instance = installInstance({ "GET /issues": empty, "GET /projects/PLAN/labels": [], "GET /epics": { items: [], total: 0, has_more: false, next_cursor: null } });
  renderAt("/PLAN/ready?label=bug&sort=priority", <Routes><Route path="/:project/:view" element={<IssueListView view={ready} />} /></Routes>);
  const user = userEvent.setup();

  expect(await screen.findByText("No issues match these filters.")).toBeInTheDocument();
  expect(screen.getByText("View defaults: todo · ready")).toBeInTheDocument();
  await user.click(screen.getByRole("button", { name: "Clear added filters" }));
  expect(await screen.findByText("No issues in this view.")).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "Remove Label: bug" })).not.toBeInTheDocument();
  await waitFor(() => {
    const calls = instance.calls.filter((call) => new URL(call.url).pathname === "/issues");
    const query = new URL(calls.at(-1)!.url).searchParams;
    expect(query.get("label")).toBeNull();
    expect(query.get("sort")).toBe("priority");
    expect(query.get("status")).toBe("todo");
    expect(query.get("ready")).toBe("true");
  });
});

/** The filter bar, open, with the answers of the instance already in it. */
async function openFilters() {
  const user = userEvent.setup();
  renderList();

  await screen.findByText("The first one");
  await user.click(screen.getByRole("button", { name: "Filters" }));

  return { user, filters: screen.getByRole("group", { name: "Issue filters" }) };
}

/**
 * The three filters that were text fields: what exists already is chosen, and
 * the values the contract knows beside a name — Any, `me`, `none` — are rows of
 * the list rather than words to guess at.
 */
it("chooses the epic instead of typing it, and offers no epic as a row", async () => {
  const { user, filters } = await openFilters();

  for (const name of ["Epic", "Assignee", "Author"]) {
    expect(within(filters).getByRole("combobox", { name })).toBeInTheDocument();
  }

  await user.click(within(filters).getByRole("combobox", { name: "Epic" }));

  const epics = await screen.findByRole("listbox", { name: "Epic" });
  expect(within(epics).getByText("PLAN-E1")).toBeInTheDocument();
  expect(within(epics).getByText("No epic")).toBeInTheDocument();

  await user.click(within(epics).getByText("PLAN-E1"));
  expect(within(filters).getByText("PLAN-E1")).toBeInTheDocument();
});

/** The assignee filter narrows; it does not assign. Hence Any, not Nobody. */
it("offers Any, me and none where the contract does", async () => {
  const { user, filters } = await openFilters();

  await user.click(within(filters).getByRole("combobox", { name: "Assignee" }));

  const assignees = await screen.findByRole("listbox", { name: "Assignee" });
  expect(within(assignees).getByText("Any")).toBeInTheDocument();
  expect(within(assignees).getByText("Me")).toBeInTheDocument();
  expect(within(assignees).getByText("Nobody")).toBeInTheDocument();
  expect(within(assignees).getByText("maintainer")).toBeInTheDocument();
});

/** An author can be an agent, so the agents of the instance are offered too. */
it("offers users and agents as authors, and no nobody", async () => {
  const { user, filters } = await openFilters();

  await user.click(within(filters).getByRole("combobox", { name: "Author" }));

  const authors = await screen.findByRole("listbox", { name: "Author" });
  expect(within(authors).getByText("maintainer")).toBeInTheDocument();
  expect(within(authors).getByText("builder")).toBeInTheDocument();
  expect(within(authors).queryByText("Nobody")).not.toBeInTheDocument();
});

/**
 * The row says the step twice, as the status dot beside it does: a mark that is
 * read at a glance, and the word that is read out. `P0` to `P4` did neither —
 * one character apart and equally loud, whichever step it was.
 */
it("marks the priority of a row with its word, not with a number", async () => {
  renderList({ "GET /issues": everyStep });

  await screen.findByText("Step 0");

  for (const [priority, word] of [[0, "none"], [1, "low"], [2, "medium"], [3, "high"], [4, "urgent"]] as const) {
    const row = screen.getByText(`Step ${priority}`).closest<HTMLElement>("[role=listitem]")!;
    expect(within(row).getByTitle(`Priority: ${word}`)).toBeInTheDocument();
    expect(row).toHaveTextContent(`Priority: ${word}`);
    expect(row).not.toHaveTextContent(`P${priority}`);
  }
});

/** Colour is spent once, and `none` is the step that must not draw the eye. */
it("lights as many bars as the step is high, and colours only urgent", async () => {
  renderList({ "GET /issues": everyStep });

  await screen.findByText("Step 0");

  for (const priority of [0, 1, 2, 3, 4]) {
    const row = screen.getByText(`Step ${priority}`).closest<HTMLElement>("[role=listitem]")!;
    const bars = Array.from(row.querySelectorAll("[title^='Priority'] span[aria-hidden] > span"));

    expect(bars).toHaveLength(4);
    expect(bars.filter((bar) => !bar.className.includes("bg-foreground/15"))).toHaveLength(priority);
    expect(bars.some((bar) => bar.className.includes("bg-destructive"))).toBe(priority === 4);
  }
});

/**
 * `sort=epic` makes the epic the first sort key, so a group is one unbroken run
 * of the answer and stays one across page boundaries (PLAN-19). The screen
 * draws the heads and does not re-sort: what comes back in that order is shown
 * in it.
 */
it("groups by epic when the epic is the sort key, and names the group without one", async () => {
  const grouped = {
    items: [
      anIssue("PLAN-3", "Under the shell", { epic: "PLAN-E1", priority: 4 }),
      anIssue("PLAN-1", "Also under the shell", { epic: "PLAN-E1", priority: 1 }),
      anIssue("PLAN-2", "Under nothing"),
    ],
    total: 3, has_more: false, next_cursor: null,
  };
  const instance = installInstance({
    "GET /issues": grouped,
    "GET /projects/PLAN/labels": [],
    "GET /epics": { items: [{ key: "PLAN-E1", project: "PLAN", title: "The shell", status: "open" }], total: 1, has_more: false, next_cursor: null },
  });
  renderAt("/PLAN/issues?sort=epic", <Routes><Route path="/:project/:view" element={<IssueListView view={all} />} /></Routes>);

  await screen.findByText("Under the shell");

  expect(new URL(instance.calls.find((call) => new URL(call.url).pathname === "/issues")!.url).searchParams.get("sort")).toBe("epic");
  // The head carries the key and the epic's own title, and the run that hangs
  // under no epic says so rather than trailing off the end of the list. The
  // key is asked of the head itself, because the rows under it name it too.
  const heads = Array.from(document.querySelectorAll("[data-group-head]"));
  expect(heads.some((head) => head.textContent?.includes("PLAN-E1") && head.textContent?.includes("The shell"))).toBe(true);
  expect(screen.getByText("No epic")).toBeInTheDocument();
  // Heads are not items: the list still holds only the three issues.
  expect(within(screen.getByRole("list", { name: "All issues issues" })).getAllByRole("listitem")).toHaveLength(3);
});

it("draws no group heads under any other sort, and every row still names its epic", async () => {
  installInstance({
    "GET /issues": {
      items: [anIssue("PLAN-1", "Under the shell", { epic: "PLAN-E1" }), anIssue("PLAN-2", "Under nothing")],
      total: 2, has_more: false, next_cursor: null,
    },
    "GET /projects/PLAN/labels": [],
    "GET /epics": { items: [], total: 0, has_more: false, next_cursor: null },
  });
  renderAt("/PLAN/issues", <Routes><Route path="/:project/:view" element={<IssueListView view={all} />} /></Routes>);

  await screen.findByText("Under the shell");

  // No head, because nothing is grouped — but the row says what it is part
  // of, which is the one thing a list that hides the epic makes you open a
  // ticket for.
  expect(document.querySelectorAll("[data-group-head]")).toHaveLength(0);
  expect(screen.queryByText("No epic")).not.toBeInTheDocument();

  const rows = within(screen.getByRole("list", { name: "All issues issues" })).getAllByRole("listitem");
  expect(within(rows[0]).getAllByText("PLAN-E1").length).toBeGreaterThan(0);
  expect(within(rows[1]).queryByText("PLAN-E1")).not.toBeInTheDocument();
});

/** The list, with the issue screen behind its rows, so that a jump shows. */
function renderRoutedList(routes: Record<string, unknown> = {}, path = `/PLAN/${all.path}`) {
  const instance = installInstance({
    "GET /issues": everyStep,
    "GET /projects/PLAN/labels": [],
    "GET /epics": { items: [], total: 0, has_more: false, next_cursor: null },
    ...routes,
  });

  renderAt(path, <Routes>
    <Route path="/:project/issues/:number" element={<IssueScreen />} />
    <Route path="/:project/:view" element={<IssueListView view={all} />} />
  </Routes>);

  return instance;
}

function IssueScreen() {
  const { number } = useParams();
  return <p>Issue screen {number}</p>;
}

function issueCalls(instance: { calls: Request[] }) {
  return instance.calls.filter((call) => new URL(call.url).pathname === "/issues");
}

it("leaves Enter to the control that has the focus", async () => {
  renderRoutedList();
  const user = userEvent.setup();
  await screen.findByText("Step 0");

  screen.getByRole("button", { name: "Filters" }).focus();
  await user.keyboard("{Enter}");

  // The button opened the filters, and the list did not jump to row 0 as well.
  expect(await screen.findByRole("group", { name: "Issue filters" })).toBeInTheDocument();
  expect(screen.queryByText(/Issue screen/)).not.toBeInTheDocument();
});

it("moves the focus from row to row on j and k, and Enter opens the one it is on", async () => {
  renderRoutedList();
  const user = userEvent.setup();
  await screen.findByText("Step 0");

  const links = () => within(screen.getByRole("list", { name: "All issues issues" })).getAllByRole("link");
  // One tab stop for the whole list, on the active row.
  expect(links().filter((link) => link.tabIndex === 0)).toHaveLength(1);

  await user.keyboard("j");
  await waitFor(() => expect(document.activeElement).toBe(links()[0]));
  await user.keyboard("j");
  await waitFor(() => expect(document.activeElement).toBe(links()[1]));
  await user.keyboard("j");
  await user.keyboard("k");
  await waitFor(() => expect(document.activeElement).toBe(links()[1]));
  expect(links()[1]).toHaveAttribute("tabindex", "0");
  expect(links()[0]).toHaveAttribute("tabindex", "-1");

  await user.keyboard("{Enter}");
  expect(await screen.findByText("Issue screen 2")).toBeInTheDocument();
});

it("opens the active row on Enter where the focus is on nothing else", async () => {
  renderRoutedList();
  const user = userEvent.setup();
  await screen.findByText("Step 0");

  (document.activeElement as HTMLElement | null)?.blur();
  await user.keyboard("{Enter}");

  expect(await screen.findByText("Issue screen 1")).toBeInTheDocument();
});

it("jumps into the search on /", async () => {
  renderRoutedList();
  const user = userEvent.setup();
  await screen.findByText("Step 0");

  await user.keyboard("/");

  expect(screen.getByRole("textbox", { name: "Search issues" })).toHaveFocus();
});

it("writes the search into the address once the typing pauses, and keeps the rows meanwhile", async () => {
  const instance = renderRoutedList();
  const user = userEvent.setup();
  await screen.findByText("Step 0");
  const before = issueCalls(instance).length;

  await user.type(screen.getByRole("textbox", { name: "Search issues" }), "shell");

  // Typed faster than the pause: nothing asked yet, and the rows still there.
  expect(issueCalls(instance)).toHaveLength(before);

  await waitFor(() => expect(issueCalls(instance)).toHaveLength(before + 1));
  expect(new URL(issueCalls(instance).at(-1)!.url).searchParams.get("q")).toBe("shell");
  expect(screen.getByText("Step 0")).toBeInTheDocument();
  expect(screen.getByRole("textbox", { name: "Search issues" })).toHaveValue("shell");
  expect(screen.getByRole("button", { name: "Remove Search: shell" })).toBeInTheDocument();
});

it("empties the field when the search is taken off as a chip", async () => {
  renderRoutedList({}, "/PLAN/issues?q=shell");
  const user = userEvent.setup();
  await screen.findByText("Step 0");

  expect(screen.getByRole("textbox", { name: "Search issues" })).toHaveValue("shell");
  await user.click(screen.getByRole("button", { name: "Remove Search: shell" }));

  expect(screen.getByRole("textbox", { name: "Search issues" })).toHaveValue("");
});

it("comes back to where the list was left, loading the rows it had", async () => {
  const pages = Array.from({ length: 60 }, (_, index) => anIssue(`PLAN-${index + 1}`, `Issue ${index + 1}`));
  window.sessionStorage.setItem("planaffe.issue-list:/PLAN/issues", JSON.stringify({ top: 1800, count: 60 }));
  const scrollTo = vi.spyOn(Element.prototype, "scrollTo");

  const instance = renderRoutedList({
    "GET /issues": (request: Request) => {
      const cursor = new URL(request.url).searchParams.get("cursor");
      return cursor === null
        ? { items: pages.slice(0, 50), total: 60, has_more: true, next_cursor: "c2" }
        : { items: pages.slice(50), total: 60, has_more: false, next_cursor: null };
    },
  });

  await screen.findByText("Issue 1");

  // Both pages are read before the offset is restored, because the second
  // one is where the offset lies.
  await waitFor(() => expect(scrollTo).toHaveBeenCalledWith({ top: 1800 }));
  expect(issueCalls(instance).map((call) => new URL(call.url).searchParams.get("cursor"))).toEqual([null, "c2"]);

  scrollTo.mockRestore();
  window.sessionStorage.clear();
});

it("survives a browser that refuses storage", async () => {
  vi.spyOn(window, "sessionStorage", "get").mockImplementation(() => {
    throw new DOMException("blocked", "SecurityError");
  });

  renderRoutedList();

  expect(await screen.findByText("Step 0")).toBeInTheDocument();
  vi.restoreAllMocks();
});
