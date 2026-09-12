import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SessionProvider } from "@/session/Session";
import { aProject, aSpace, aSpacePage, aUser, installInstance, renderAt, type Route } from "@/shared/testing";
import { Shell } from "@/shell/Shell";

const tree = [
  aSpacePage("company", "The company"),
  aSpacePage("company/onboarding", "Onboarding"),
];

const page = {
  ...aSpacePage("company/onboarding", "Onboarding"),
  body: "# The first week\n\nWho hands over what, and when.",
  updated_at: "2026-09-05T12:00:00Z",
};

function page_(path: string, routes: Record<string, Route> = {}) {
  const instance = installInstance({
    "GET /projects": [aProject],
    "GET /issues": { items: [], total: 0, has_more: false, next_cursor: null },
    "GET /spaces": [aSpace],
    "GET /spaces/handbook/pages": tree,
    // Wrapped: an answer carrying a `body` is an envelope to `installInstance`,
    // and a page carries one of its own.
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

describe("a page in a space (VISION 18)", () => {
  it("renders the Markdown and the path from the root down", async () => {
    const instance = page_("/spaces/handbook/pages/company/onboarding");

    expect(await screen.findByRole("heading", { name: "The first week" })).toBeInTheDocument();

    const trail = screen.getByRole("navigation", { name: "The path to this page" });
    expect(within(trail).getByRole("link", { name: "The company handbook" })).toHaveAttribute("href", "/spaces/handbook");
    expect(within(trail).getByRole("link", { name: "The company" })).toHaveAttribute(
      "href",
      "/spaces/handbook/pages/company",
    );

    // The address carries the tree and the slashes are the address (ADR 0028):
    // a client that escapes them names nothing.
    expect(instance.calls.map((call) => new URL(call.url).pathname)).toContain(
      "/spaces/handbook/pages/company/onboarding",
    );
  });

  it("says what an empty page is for rather than showing nothing", async () => {
    page_("/spaces/handbook/pages/company/onboarding", {
      "GET /spaces/handbook/pages/company/onboarding": { body: { ...page, body: "" } },
    });

    expect(await screen.findByText(/This page is empty/)).toBeInTheDocument();
  });

  // The same field as everywhere else in the product, and the same guard:
  // `If-Match` against the `updated_at` last read.
  it("edits in the same Markdown field and guards the write", async () => {
    const instance = page_("/spaces/handbook/pages/company/onboarding", {
      "PATCH /spaces/handbook/pages/company/onboarding": {
        body: { ...page, body: "changed", updated_at: "2026-09-06T09:00:00Z" },
      },
    });

    await userEvent.click(await screen.findByRole("button", { name: "Edit" }));
    const field = await screen.findByRole("textbox", { name: "Body" });
    await userEvent.clear(field);
    await userEvent.type(field, "changed");
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));

    const written = instance.calls.find((call) => call.method === "PATCH")!;
    expect(written.headers.get("If-Match")).toBe("2026-09-05T12:00:00Z");
    expect(await screen.findByText("changed")).toBeInTheDocument();
  });

  // A conflict is shown and not lost: the typed text stays, the other version
  // is there to merge from, and saving again is a decision.
  it("shows the other version when the page changed underneath", async () => {
    page_("/spaces/handbook/pages/company/onboarding", {
      "PATCH /spaces/handbook/pages/company/onboarding": {
        status: 412,
        body: {
          type: "/problems/stale",
          title: "stale",
          status: 412,
          detail: "The object has changed since it was read",
          current: { ...page, body: "somebody else wrote this", updated_at: "2026-09-06T08:00:00Z" },
        },
      },
    });

    await userEvent.click(await screen.findByRole("button", { name: "Edit" }));
    const field = await screen.findByRole("textbox", { name: "Body" });
    await userEvent.clear(field);
    await userEvent.type(field, "mine");
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(/was changed while you were editing it/);
    expect(screen.getByText("somebody else wrote this")).toBeInTheDocument();
    // The typed text is still in the field.
    expect(screen.getByRole("textbox", { name: "Body" })).toHaveValue("mine");
  });

  it("hands the page over as the Markdown file it is", async () => {
    let handed: Blob | undefined;
    const clicked = vi.fn();
    const created = vi.fn((blob: Blob) => {
      handed = blob;
      return "blob:the-page";
    });
    vi.stubGlobal("URL", Object.assign(URL, { createObjectURL: created, revokeObjectURL: vi.fn() }));
    const anchor = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(clicked);

    page_("/spaces/handbook/pages/company/onboarding");
    await userEvent.click(await screen.findByRole("button", { name: "Download" }));

    expect(created).toHaveBeenCalledOnce();
    expect(await handed!.text()).toBe(page.body);
    expect(handed!.type).toContain("text/markdown");
    expect(clicked).toHaveBeenCalledOnce();
    anchor.mockRestore();
  });

  // Not forbidden, not there (ADR 0027): a space the caller is not named on
  // and a page that never existed get the same sentence.
  it("says the same thing about a page that is not there for this caller", async () => {
    page_("/spaces/handbook/pages/company/payroll", {
      "GET /spaces/handbook/pages/company/payroll": {
        status: 404,
        body: { type: "/problems/not-found", title: "not-found", status: 404, detail: "Nothing at that address" },
      },
    });

    expect(await screen.findByText(/It may have been renamed or moved/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Back to the space" })).toHaveAttribute("href", "/spaces/handbook");
  });

  it("says that a deleted page is deleted, and until when it can come back", async () => {
    page_("/spaces/handbook/pages/company/onboarding", {
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
    });

    expect(await screen.findByText("This page is deleted.")).toBeInTheDocument();
    expect(screen.getByText(/It can be brought back until/)).toBeInTheDocument();
  });
});
