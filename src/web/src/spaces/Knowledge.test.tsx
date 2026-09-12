import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SessionProvider } from "@/session/Session";
import { aProject, aSpace, aSpacePage, aUser, installInstance, renderAt, type Route } from "@/shared/testing";
import { Shell } from "@/shell/Shell";

const closed = { ...aSpace, name: "board", title: "The board", closed_to_agents: true };

const tree = [
  aSpacePage("company", "The company"),
  aSpacePage("company/onboarding", "Onboarding"),
  aSpacePage("company/onboarding/day-one", "The first day"),
  aSpacePage("product", "The product"),
];

function knowledge(path: string, routes: Record<string, Route> = {}) {
  const instance = installInstance({
    "GET /projects": [aProject],
    "GET /issues": { items: [], total: 0, has_more: false, next_cursor: null },
    "GET /projects/PLAN/labels": [],
    "GET /projects/PLAN/needs-you": { items: [], total: 0, has_more: false, next_cursor: null },
    "GET /spaces": [aSpace, closed],
    "GET /spaces/handbook/pages": tree,
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

describe("the knowledge base in the frame (VISION 18)", () => {
  it("lists the spaces the caller may see, and says which is closed to agents", async () => {
    knowledge("/spaces");

    expect(await screen.findByRole("link", { name: /The company handbook/ })).toHaveAttribute("href", "/spaces/handbook");
    const board = await screen.findByRole("link", { name: /The board/ });

    expect(within(board).getByText("Closed to agents")).toBeInTheDocument();
  });

  it("says what a space is where there is none", async () => {
    knowledge("/spaces", { "GET /spaces": [] });

    expect(await screen.findByText("No space yet.")).toBeInTheDocument();
  });

  // The border of VISION 18: one shell, and what it carries says which area
  // the reader is in. No project switcher here, and none of the tracker's
  // counts — they are about work that is waiting, and nothing here waits.
  it("carries the space and not the project, and leads back to the tracker", async () => {
    knowledge("/spaces/handbook");

    expect(await screen.findByRole("button", { name: "Switch space" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Switch project" })).not.toBeInTheDocument();

    const navigation = await screen.findByRole("navigation", { name: "The knowledge base" });
    expect(within(navigation).queryByRole("link", { name: /^Needs you/ })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Tracker" })).toHaveAttribute("href", "/");
  });

  it("leads into the knowledge base from the tracker", async () => {
    knowledge("/PLAN/ready");

    expect(await screen.findByRole("link", { name: "Knowledge base" })).toHaveAttribute("href", "/spaces");
    expect(screen.getByRole("button", { name: "Switch project" })).toBeInTheDocument();
  });

  it("draws the page tree as the navigation of the space, and unfolds a branch on request", async () => {
    knowledge("/spaces/handbook");

    const navigation = await screen.findByRole("navigation", { name: "The knowledge base" });
    expect(await within(navigation).findByRole("link", { name: "The company" })).toHaveAttribute(
      "href",
      "/spaces/handbook/pages/company",
    );
    expect(within(navigation).getByRole("link", { name: "The product" })).toBeInTheDocument();
    // A branch nobody is standing in is folded: the page below is not there
    // until it is asked for.
    expect(within(navigation).queryByRole("link", { name: "Onboarding" })).not.toBeInTheDocument();

    await userEvent.click(within(navigation).getByRole("button", { name: "Unfold The company" }));

    expect(within(navigation).getByRole("link", { name: "Onboarding" })).toHaveAttribute(
      "href",
      "/spaces/handbook/pages/company/onboarding",
    );
  });

  it("stands the branch of the open page open, three levels down", async () => {
    knowledge("/spaces/handbook/pages/company/onboarding/day-one");

    const navigation = await screen.findByRole("navigation", { name: "The knowledge base" });

    expect(await within(navigation).findByRole("link", { name: "The first day" })).toHaveAttribute(
      "href",
      "/spaces/handbook/pages/company/onboarding/day-one",
    );
    expect(within(navigation).getByRole("link", { name: "Onboarding" })).toBeInTheDocument();
  });

  it("says what stands directly under a space", async () => {
    knowledge("/spaces/handbook");

    const contents = await screen.findByRole("list", { name: "Directly under this space" });

    expect(within(contents).getByRole("link", { name: /The company/ })).toHaveAttribute(
      "href",
      "/spaces/handbook/pages/company",
    );
    // The table of contents is the pages under the space and not the tree:
    // what hangs below them is the navigation's to draw.
    expect(within(contents).queryByRole("link", { name: "Onboarding" })).not.toBeInTheDocument();
  });

  it("says nothing about a space that is not there for this caller", async () => {
    knowledge("/spaces/board-minutes", { "GET /spaces/board-minutes/pages": { status: 404, body: { detail: "no" } } });

    expect(await screen.findByText("There is no space under this address.")).toBeInTheDocument();
  });

  it("reaches a space and a page of the open one from the command palette", async () => {
    knowledge("/spaces/handbook");

    await screen.findByRole("navigation", { name: "The knowledge base" });
    await userEvent.keyboard("{Meta>}k{/Meta}");

    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByRole("combobox"), "onboarding");

    expect(await within(dialog).findByText("Onboarding")).toBeInTheDocument();
  });
});
