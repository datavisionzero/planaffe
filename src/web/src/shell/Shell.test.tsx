import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SessionProvider } from "@/session/Session";
import { aProject, aUser, installInstance, renderAt } from "@/shared/testing";
import { Shell } from "./Shell";
import { drawn, shortcuts } from "./shortcuts";
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
    "GET /projects/PLAN/needs-you": {
      items: [{ issue: anIssue("PLAN-13", "The web shell", "review"), because: "review" }],
      total: 1,
      has_more: false,
      next_cursor: null,
    },
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

  // Needs you is the one view of the four that is not a filtered issue list:
  // it has an endpoint of its own that says why each issue is on it.
  it("gives Needs you its own screen rather than a filtered issue list", async () => {
    const { calls } = shell("/PLAN/needs-you");

    expect(await screen.findByRole("heading", { name: "In review · 1", level: 2 })).toBeInTheDocument();
    expect(calls.some((call) => new URL(call.url).pathname === "/projects/PLAN/needs-you")).toBe(true);
    expect(calls.some((call) => new URL(call.url).pathname === "/issues")).toBe(false);
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

  // The screen matrix gives /:project/issues "filters open as a dismissible
  // sheet" on a narrow screen; they were an inline bar on every width.
  it("opens the filters as a sheet on a narrow screen and hands the focus back", async () => {
    vi.stubGlobal("matchMedia", (media: string) => ({
      matches: true, media, onchange: null,
      addEventListener: () => undefined, removeEventListener: () => undefined,
      addListener: () => undefined, removeListener: () => undefined, dispatchEvent: () => false,
    }));
    shell("/PLAN/issues");
    const user = userEvent.setup();
    await screen.findByText("The web shell");

    const filters = screen.getByRole("button", { name: "Filters" });
    await user.click(filters);

    const sheet = await screen.findByRole("dialog", { name: "Filters" });
    expect(within(sheet).getByRole("group", { name: "Issue filters" })).toBeInTheDocument();

    await user.click(within(sheet).getByRole("button", { name: "Close" }));

    await waitFor(() => expect(screen.queryByRole("dialog", { name: "Filters" })).not.toBeInTheDocument());
    expect(filters).toHaveFocus();
  });

  it("keeps the filter bar in place on a wide screen, and Escape returns the focus", async () => {
    shell("/PLAN/issues");
    const user = userEvent.setup();
    await screen.findByText("The web shell");

    const filters = screen.getByRole("button", { name: "Filters" });
    await user.click(filters);

    expect(screen.getByRole("group", { name: "Issue filters" })).toBeInTheDocument();
    expect(screen.queryByRole("dialog", { name: "Filters" })).not.toBeInTheDocument();

    await user.keyboard("{Escape}");

    expect(screen.queryByRole("group", { name: "Issue filters" })).not.toBeInTheDocument();
    expect(filters).toHaveFocus();
  });

  // ADR 0013 calls the deleted list a real read; it was reachable only by
  // typing ?deleted=true into the address.
  it("offers the deleted list from the filters", async () => {
    const { calls } = shell("/PLAN/issues");
    const user = userEvent.setup();
    await screen.findByText("The web shell");

    await user.click(screen.getByRole("button", { name: "Filters" }));
    await user.selectOptions(screen.getByLabelText("Deleted"), "true");

    await waitFor(() =>
      expect(calls.some((call) => new URL(call.url).searchParams.get("deleted") === "true")).toBe(true),
    );
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

  // The frame used to flatten a failed list into an empty one, so the switcher
  // claimed there were no projects and the sidebar simply went dead.
  it("says the project list failed rather than that there are none", async () => {
    let answers = 0;
    installInstance({
      "GET /projects": () => (answers++ === 0 ? { status: 503, body: { detail: "no" } } : [aProject]),
      "GET /issues": { items: [], total: 0, has_more: false, next_cursor: null },
      "GET /projects/PLAN/labels": [],
    });
    renderAt(
      "/PLAN/ready",
      <SessionProvider value={{ me: aUser, signOut: vi.fn() }}>
        <Shell />
      </SessionProvider>,
    );
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Switch project" }));

    expect(await screen.findByText("The projects could not be loaded.")).toBeInTheDocument();
    expect(screen.queryByText("No project yet.")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Try again" }));

    expect(await screen.findByRole("menuitem", { name: /planaffe/ })).toBeInTheDocument();
  });

  it("offers project creation from the project switcher", async () => {
    shell("/PLAN/ready");
    const user = userEvent.setup();

    await screen.findByText("The web shell");
    await user.click(screen.getByRole("button", { name: "Switch project" }));
    await user.click(await screen.findByRole("menuitem", { name: "Create project" }));

    expect(await screen.findByRole("heading", { name: "Create project" })).toBeInTheDocument();
  });

  // `c` creates from a list, and nothing on the screen said so. The epic list
  // has had its button all along; the issue lists say it the same way now.
  it("offers issue creation from the header of every issue list", async () => {
    shell("/PLAN/ready");
    const user = userEvent.setup();
    await screen.findByText("The web shell");

    await user.click(screen.getByRole("link", { name: "New issue" }));

    expect(await screen.findByRole("heading", { name: "Create issue" })).toBeInTheDocument();
  });

  it("offers issue creation from Needs you, which is a list of issues too", async () => {
    shell("/PLAN/needs-you");
    const user = userEvent.setup();
    await screen.findByRole("heading", { name: "In review · 1", level: 2 });

    await user.click(screen.getByRole("link", { name: "New issue" }));

    expect(await screen.findByRole("heading", { name: "Create issue" })).toBeInTheDocument();
  });

  it("offers the three things that can be created from the palette", async () => {
    shell("/PLAN/ready");
    const user = userEvent.setup();
    await screen.findByText("The web shell");

    await user.keyboard("{Meta>}k{/Meta}");
    await user.type(await screen.findByRole("combobox", { name: /command/i }), "create");

    for (const what of ["Create issue", "Create epic", "Create project"]) {
      expect(await screen.findByRole("option", { name: new RegExp(what) })).toBeInTheDocument();
    }

    await user.click(screen.getByRole("option", { name: /Create epic/ }));

    expect(await screen.findByRole("heading", { name: "Create epic" })).toBeInTheDocument();
  });

  it("renders the sidebar controls as focusable links", async () => {
    shell("/PLAN/ready");

    const navigation = await screen.findByRole("navigation");
    for (const link of within(navigation).getAllByRole("link")) {
      expect(link.tagName).toBe("A");
      expect(link).toHaveAttribute("data-sidebar", "menu-button");
      expect(link.tabIndex).toBe(0);
    }
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

  it("offers what the instance finds for words, and the way to all of them", async () => {
    const { calls } = shell("/PLAN/ready");
    const user = userEvent.setup();
    await screen.findByText("The web shell");

    await user.keyboard("{Meta>}k{/Meta}");
    await user.type(await screen.findByRole("combobox", { name: /command/i }), "shell");

    const match = await screen.findByRole("option", { name: /The web shell/ });
    expect(within(match).getByText("PLAN-13")).toBeInTheDocument();
    await waitFor(() => {
      const search = calls.map((call) => new URL(call.url)).find((url) => url.searchParams.get("q") === "shell")!;
      expect(search.searchParams.get("project")).toBe("PLAN");
      expect(search.searchParams.get("limit")).toBe("5");
    });

    await user.click(match);

    expect(await screen.findByRole("heading", { name: /PLAN-13/ })).toBeInTheDocument();
  });

  it("lands on the filtered list from the last row of the matches", async () => {
    shell("/PLAN/ready");
    const user = userEvent.setup();
    await screen.findByText("The web shell");

    await user.keyboard("{Meta>}k{/Meta}");
    await user.type(await screen.findByRole("combobox", { name: /command/i }), "shell");
    await user.click(await screen.findByRole("option", { name: /All issues matching/ }));

    expect(await screen.findByRole("heading", { name: "All issues" })).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "Search issues" })).toHaveValue("shell");
  });

  // PLAN-51: the dropdown advertised ⌘P and nothing was bound to it. ⌘P is the
  // browser's print, so the shortcut that got bound is the bare key the lists
  // already speak.
  it("opens the project switcher on the key it advertises", async () => {
    shell("/PLAN/ready");
    const user = userEvent.setup();
    await screen.findByText("The web shell");

    await user.keyboard("p");

    const label = await screen.findByText("Projects");
    expect(within(label).getByText("P")).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: /logaffe/ })).toBeInTheDocument();
  });

  it("leaves the key alone while something is being typed", async () => {
    shell("/PLAN/ready");
    const user = userEvent.setup();
    await screen.findByText("The web shell");

    await user.type(screen.getByRole("textbox", { name: "Search issues" }), "print");

    expect(screen.queryByRole("menuitem", { name: /logaffe/ })).not.toBeInTheDocument();
  });

  // The shell is not remounted by navigation (ADR 0006), so the frame kept the
  // list it had asked for once: on arrival at the project just created there
  // was no current project, and every link in the frame was drawn disabled.
  it("has the project a screen just created when it navigates there", async () => {
    const created = { ...aProject, key: "NEW", name: "the new one" };
    let made = false;
    installInstance({
      "GET /projects": () => (made ? [aProject, created] : [aProject]),
      "POST /projects": () => { made = true; return { status: 201, body: created }; },
      "GET /issues": { items: [], total: 0, has_more: false, next_cursor: null },
      "GET /projects/NEW/labels": [],
    });
    renderAt(
      "/projects/new",
      <SessionProvider value={{ me: aUser, signOut: vi.fn() }}>
        <Shell />
      </SessionProvider>,
    );
    const user = userEvent.setup();

    await user.type(await screen.findByLabelText("Key"), "NEW");
    await user.type(screen.getByLabelText("Name"), "the new one");
    await user.click(screen.getByRole("button", { name: "Create project" }));

    const navigation = await screen.findByRole("navigation");
    await waitFor(() =>
      expect(within(navigation).getByRole("link", { name: views[0].label })).toHaveAttribute(
        "href",
        `/NEW/${views[0].path}`,
      ),
    );
    expect(screen.getByRole("button", { name: "Switch project" })).toHaveTextContent("the new one");
  });

  it("shows who is signed in, top right", async () => {
    shell("/PLAN/ready");
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Account: maintainer" }));

    expect(await screen.findByRole("menuitem", { name: "Sign out" })).toBeInTheDocument();
    expect(screen.getByText(/administrator/)).toBeInTheDocument();
  });
  // The application binds a dozen keys and used to explain exactly one of them,
  // on the palette button. The overview is the one place that says all of them,
  // and `shortcuts.ts` is the one place they are written down.
  it("opens the overview of the keys on ?, and draws every key it binds", async () => {
    shell("/PLAN/ready");
    const user = userEvent.setup();
    await screen.findByText("The web shell");

    await user.keyboard("?");

    const overview = await screen.findByRole("dialog", { name: "Keyboard shortcuts" });
    for (const shortcut of shortcuts) {
      const row = within(overview).getByText(shortcut.what).closest("div")!;
      for (const cap of drawn(shortcut.id)) {
        expect(within(row).getByText(cap)).toBeInTheDocument();
      }
    }
  });

  it("leaves ? alone while something is being typed", async () => {
    shell("/PLAN/ready");
    const user = userEvent.setup();
    await screen.findByText("The web shell");

    await user.type(screen.getByRole("textbox", { name: "Search issues" }), "why?");

    expect(screen.queryByRole("dialog", { name: "Keyboard shortcuts" })).not.toBeInTheDocument();
  });

  // A list of shortcuts reachable only by a shortcut helps nobody who has not
  // found one yet: the menu is the way in for a reader who never presses a key.
  it("offers the overview from the account menu", async () => {
    shell("/PLAN/ready");
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Account: maintainer" }));
    await user.click(await screen.findByRole("menuitem", { name: /Keyboard shortcuts/ }));

    expect(await screen.findByRole("dialog", { name: "Keyboard shortcuts" })).toBeInTheDocument();
  });

  it("offers the overview from the palette, and steps aside for it", async () => {
    shell("/PLAN/ready");
    const user = userEvent.setup();
    await screen.findByText("The web shell");

    await user.keyboard("{Meta>}k{/Meta}");
    await user.type(await screen.findByRole("combobox", { name: /command/i }), "keyboard");
    await user.click(await screen.findByRole("option", { name: /Keyboard shortcuts/ }));

    expect(await screen.findByRole("dialog", { name: "Keyboard shortcuts" })).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole("combobox", { name: /command/i })).not.toBeInTheDocument());
  });
});
