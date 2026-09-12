import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SessionProvider } from "@/session/Session";
import { aHit, aProject, aSpace, aSpacePage, aUser, installInstance, renderAt, type Route } from "@/shared/testing";
import { Shell } from "@/shell/Shell";

const other = { ...aSpace, name: "personal", title: "Personal" };

const tree = [aSpacePage("company", "The company"), aSpacePage("company/onboarding", "Onboarding")];

const hits = [
  aHit(
    "company/onboarding",
    "Onboarding",
    [
      { text: "Am ", hit: false },
      { text: "ersten Tag", hit: true },
      { text: " bekommt jede neue Person eine Karte.", hit: false },
    ],
    aSpace,
    [{ path: "company", title: "The company" }],
  ),
  aHit("vertrag", "Vertrag", [{ text: "Vertraulich, und deshalb hier.", hit: false }], other),
];

function knowledge(path: string, routes: Record<string, Route> = {}) {
  const instance = installInstance({
    "GET /projects": [aProject],
    "GET /issues": { items: [], total: 0, has_more: false, next_cursor: null },
    "GET /projects/PLAN/labels": [],
    "GET /projects/PLAN/needs-you": { items: [], total: 0, has_more: false, next_cursor: null },
    "GET /spaces": [aSpace, other],
    "GET /spaces/handbook/pages": tree,
    "GET /pages": hits,
    ...routes,
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

describe("the search across the knowledge base (VISION 18)", () => {
  it("shows where a hit stands and marks what matched", async () => {
    knowledge("/spaces?q=onboarding");

    const list = await screen.findByRole("list", { name: "Hits" });
    const rows = within(list).getAllByRole("link");

    expect(rows[0]).toHaveAttribute("href", "/spaces/handbook/pages/company/onboarding");
    // The way down to it, so that a hit from a space the reader is not in says
    // where it is.
    expect(within(rows[0]!).getByText("The company handbook / The company")).toBeInTheDocument();
    expect(within(rows[0]!).getByText("ersten Tag").tagName).toBe("MARK");
    expect(rows[1]).toHaveAttribute("href", "/spaces/personal/pages/vertrag");
  });

  it("writes what is typed into the address, and searches the space it is standing in", async () => {
    const instance = knowledge("/spaces/handbook");

    await userEvent.type(await screen.findByRole("searchbox", { name: "Search handbook" }), "onboarding");

    expect(await screen.findByRole("list", { name: "Hits" })).toBeInTheDocument();

    const asked = instance.calls.map((call) => new URL(call.url)).filter((url) => url.pathname === "/pages");
    expect(asked.at(-1)?.searchParams.get("q")).toBe("onboarding");
    expect(asked.at(-1)?.searchParams.get("space")).toBe("handbook");
    // The address is what the screen read, so the screen says which space it
    // stands in and carries the way out of it.
    expect(screen.getByText("Hits in this space.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Search the whole knowledge base" })).toHaveAttribute(
      "href",
      "/spaces?q=onboarding",
    );
  });

  it("searches the whole knowledge base from its door", async () => {
    const instance = knowledge("/spaces");

    await userEvent.type(await screen.findByRole("searchbox", { name: "Search the knowledge base" }), "vertrag");

    expect(await screen.findByRole("list", { name: "Hits" })).toBeInTheDocument();

    const asked = instance.calls.map((call) => new URL(call.url)).filter((url) => url.pathname === "/pages");
    expect(asked.at(-1)?.searchParams.has("space")).toBe(false);
  });

  it("says that nothing matched, and offers the way out of one space", async () => {
    knowledge("/spaces/handbook?q=nichts", { "GET /pages": [] });

    expect(await screen.findByText(/Nothing matched “nichts”/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Search the whole knowledge base" })).toHaveAttribute(
      "href",
      "/spaces?q=nichts",
    );
  });

  it("offers no search where there is no space", async () => {
    knowledge("/spaces", { "GET /spaces": [] });

    expect(await screen.findByText("No space yet.")).toBeInTheDocument();
    expect(screen.queryByRole("searchbox")).not.toBeInTheDocument();
  });

  it("asks the same search from the palette and leads to all of it", async () => {
    knowledge("/spaces/handbook");

    await screen.findByRole("navigation", { name: "The knowledge base" });
    await userEvent.keyboard("{Meta>}k{/Meta}");

    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByRole("combobox"), "onboarding");

    // A hit of the search, wherever it stands — not a page of the tree the
    // frame happens to hold.
    expect(await within(dialog).findByText("Vertrag")).toBeInTheDocument();
    expect(within(dialog).getByText("All pages matching “onboarding”")).toBeInTheDocument();
  });
});
