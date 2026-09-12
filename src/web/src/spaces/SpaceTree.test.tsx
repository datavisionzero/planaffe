import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SessionProvider } from "@/session/Session";
import { aProject, aSpace, aSpacePage, aUser, installInstance, renderAt, type Route } from "@/shared/testing";
import { Shell } from "@/shell/Shell";

const other = { ...aSpace, name: "customers", title: "Customers" };

const tree = [
  aSpacePage("company", "The company"),
  aSpacePage("company/onboarding", "Onboarding"),
  aSpacePage("company/onboarding/day-one", "The first day"),
  aSpacePage("product", "The product"),
];

const page = { ...aSpacePage("company/onboarding", "Onboarding"), body: "Who hands over what." };

function tree_(path: string, routes: Record<string, Route> = {}) {
  const instance = installInstance({
    "GET /projects": [aProject],
    "GET /issues": { items: [], total: 0, has_more: false, next_cursor: null },
    "GET /spaces": [aSpace, other],
    "GET /spaces/handbook/pages": tree,
    "GET /spaces/customers/pages": [],
    "GET /spaces/handbook/pages/company/onboarding": { body: page },
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

describe("the tree in the hand (ADR 0028)", () => {
  it("creates a page under the parent the address names, with a given slug", async () => {
    const instance = tree_("/spaces/handbook/new?parent=company", {
      "POST /spaces/handbook/pages": { status: 201, body: aSpacePage("company/values", "Our values") },
      "GET /spaces/handbook/pages/company/values": { body: { ...aSpacePage("company/values", "Our values"), body: "" } },
    });

    const under = await screen.findByRole("combobox", { name: "Under" });
    expect(under).toHaveValue("company");

    await userEvent.type(screen.getByRole("textbox", { name: "Slug" }), "values");
    await userEvent.type(screen.getByRole("textbox", { name: "Title" }), "Our values");
    await userEvent.click(screen.getByRole("button", { name: "Create page" }));

    const written = instance.calls.find((call) => call.method === "POST")!;
    expect(await written.json()).toMatchObject({ parent: "company", slug: "values", title: "Our values" });
  });

  // There is no fourth level (VISION 18), so the third is never offered as a
  // parent — the instance refuses it too, and that refusal is shown where it
  // arrives.
  it("offers no parent that has no room below it", async () => {
    tree_("/spaces/handbook/new");

    const under = await screen.findByRole("combobox", { name: "Under" });
    const offered = within(under).getAllByRole("option").map((option) => option.textContent);

    expect(offered).toContain("The company");
    expect(offered).toContain("— Onboarding");
    expect(offered).not.toContain("— — The first day");
  });

  it("says what a taken slug is, in the instance's words", async () => {
    tree_("/spaces/handbook/new", {
      "POST /spaces/handbook/pages": {
        status: 400,
        body: { type: "/problems/validation", title: "validation", status: 400, detail: "slug: onboarding is taken under company" },
      },
    });

    await userEvent.type(await screen.findByRole("textbox", { name: "Slug" }), "onboarding");
    await userEvent.type(screen.getByRole("textbox", { name: "Title" }), "Onboarding");
    await userEvent.click(screen.getByRole("button", { name: "Create page" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("is taken under company");
  });

  it("renames a page and says how many pages hang below it", async () => {
    const instance = tree_("/spaces/handbook/pages/company/onboarding", {
      "PATCH /spaces/handbook/pages/company/onboarding": { body: aSpacePage("company/arrival", "Onboarding") },
      "GET /spaces/handbook/pages/company/arrival": { body: { ...aSpacePage("company/arrival", "Onboarding"), body: "x" } },
    });

    await userEvent.click(await screen.findByRole("button", { name: "Rename page" }));
    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent("1 page hangs below it and goes along.");

    const field = within(dialog).getByRole("textbox", { name: "New slug" });
    await userEvent.clear(field);
    await userEvent.type(field, "arrival");
    await userEvent.click(within(dialog).getByRole("button", { name: "Rename page" }));

    const written = instance.calls.find((call) => call.method === "PATCH")!;
    expect(await written.json()).toEqual({ slug: "arrival" });
  });

  it("moves a page into another space, with everything below it", async () => {
    const instance = tree_("/spaces/handbook/pages/company/onboarding", {
      "POST /spaces/handbook/pages/move": { body: aSpacePage("onboarding", "Onboarding", "customers") },
      "GET /spaces/customers/pages/onboarding": { body: { ...aSpacePage("onboarding", "Onboarding", "customers"), body: "x" } },
    });

    await userEvent.click(await screen.findByRole("button", { name: "Move page" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.selectOptions(within(dialog).getByRole("combobox", { name: "Space" }), "customers");
    await userEvent.click(within(dialog).getByRole("button", { name: "Move page" }));

    const written = instance.calls.find((call) => new URL(call.url).pathname === "/spaces/handbook/pages/move")!;
    expect(await written.json()).toEqual({ path: "company/onboarding", space: "customers", parent: null });
  });

  // The four refusals of a move each say their own thing, and the dialog shows
  // what came rather than one sentence for all of them.
  it("shows a refused move in the instance's own words", async () => {
    tree_("/spaces/handbook/pages/company/onboarding", {
      "POST /spaces/handbook/pages/move": {
        status: 422,
        body: {
          type: "/problems/too-deep",
          title: "too-deep",
          status: 422,
          detail: "handbook/company/onboarding is 2 levels tall and there is room for 1 where it would land.",
          depth: 1,
        },
      },
    });

    await userEvent.click(await screen.findByRole("button", { name: "Move page" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Move page" }));

    expect(await within(dialog).findByRole("alert")).toHaveTextContent("2 levels tall");
  });

  // Deleting takes the subtree, and that has to stand there before it is
  // confirmed as well as after.
  it("says how many pages a delete takes, before and after", async () => {
    tree_("/spaces/handbook/pages/company/onboarding", {
      "DELETE /spaces/handbook/pages/company/onboarding": { body: { deleted: 2 } },
    });

    await userEvent.click(await screen.findByRole("button", { name: "Delete page" }));
    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent("1 page hangs below it and goes along.");

    await userEvent.click(within(dialog).getByRole("button", { name: "Delete page" }));

    expect(await screen.findByText("This page and the 1 below it are deleted.")).toBeInTheDocument();
  });

  it("brings a deleted page back from its own address", async () => {
    const instance = tree_("/spaces/handbook/pages/company/onboarding", {
      "GET /spaces/handbook/pages/company/onboarding": {
        status: 404,
        body: {
          type: "/problems/deleted",
          title: "deleted",
          status: 404,
          detail: "The page is deleted and can still be restored",
          restorable_until: "2026-09-19T12:00:00Z",
        },
      },
      "POST /spaces/handbook/pages/restore": { body: page },
    });

    await userEvent.click(await screen.findByRole("button", { name: "Restore page" }));

    const written = instance.calls.find((call) => new URL(call.url).pathname === "/spaces/handbook/pages/restore")!;
    expect(await written.json()).toEqual({ path: "company/onboarding" });
    expect(await screen.findByText("Who hands over what.")).toBeInTheDocument();
  });

  // A live page under a deleted one is the state the rule exists to prevent,
  // so the way on is the page above — which the address already names.
  it("points at the page above where that one is deleted too", async () => {
    tree_("/spaces/handbook/pages/company/onboarding", {
      "GET /spaces/handbook/pages/company/onboarding": {
        status: 404,
        body: { type: "/problems/deleted", title: "deleted", status: 404, detail: "The page is deleted and can still be restored" },
      },
      "POST /spaces/handbook/pages/restore": {
        status: 422,
        body: {
          type: "/problems/transition",
          title: "transition",
          status: 422,
          detail: "The page above handbook/company/onboarding is deleted; restore handbook/company first.",
        },
      },
    });

    await userEvent.click(await screen.findByRole("button", { name: "Restore page" }));

    expect(await screen.findByRole("link", { name: "Restore the page above first" })).toHaveAttribute(
      "href",
      "/spaces/handbook/pages/company",
    );
  });
});
