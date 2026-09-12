import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SessionProvider } from "@/session/Session";
import { aProject, aSpace, aUser, installInstance, renderAt, type Route } from "@/shared/testing";
import { Shell } from "@/shell/Shell";

const member = { ...aUser, administrator: false };

const other = {
  id: "0199a000-0000-7000-8000-000000000009",
  kind: "user" as const,
  name: "colleague",
  email: "colleague@example.test",
  state: "active" as const,
  administrator: false,
  created_at: "2026-09-02T10:00:00Z",
};

const named = {
  id: aUser.id,
  kind: "user" as const,
  name: aUser.name,
  email: aUser.email,
  state: "active" as const,
  administrator: true,
  created_at: "2026-09-02T10:00:00Z",
};

function admin(path: string, routes: Record<string, Route> = {}, me = aUser) {
  const instance = installInstance({
    "GET /projects": [aProject],
    "GET /issues": { items: [], total: 0, has_more: false, next_cursor: null },
    "GET /spaces": [aSpace],
    "GET /spaces/handbook/pages": [],
    "GET /spaces/handbook/users": [named],
    "GET /users": [named, other],
    ...routes,
  });

  renderAt(
    path,
    <SessionProvider value={{ me, signOut: vi.fn() }}>
      <Shell />
    </SessionProvider>,
  );

  return instance;
}

afterEach(() => {
  vi.unstubAllGlobals();
  window.localStorage.clear();
});

describe("managing a space (ADR 0027)", () => {
  it("creates a space from the list, with the name given and not derived", async () => {
    const instance = admin("/spaces", {
      "POST /spaces": { status: 201, body: { ...aSpace, name: "customers", title: "Customers" } },
      "GET /spaces/customers/pages": [],
    });

    await userEvent.click(await screen.findByRole("button", { name: "New space" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByRole("textbox", { name: /Name/ }), "customers");
    await userEvent.type(within(dialog).getByRole("textbox", { name: "Title" }), "Customers");
    await userEvent.click(within(dialog).getByRole("checkbox", { name: "Closed to agents" }));
    await userEvent.click(within(dialog).getByRole("button", { name: "Create space" }));

    const written = instance.calls.find((call) => call.method === "POST")!;
    expect(await written.json()).toEqual({ name: "customers", title: "Customers", closed_to_agents: true });
  });

  it("says what a taken name is, in the instance's words", async () => {
    admin("/spaces", {
      "POST /spaces": {
        status: 400,
        body: { type: "/problems/validation", title: "validation", status: 400, detail: "name: handbook belongs to a deleted space, restorable until 2026-09-19" },
      },
    });

    await userEvent.click(await screen.findByRole("button", { name: "New space" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByRole("textbox", { name: /Name/ }), "handbook");
    await userEvent.type(within(dialog).getByRole("textbox", { name: "Title" }), "The handbook");
    await userEvent.click(within(dialog).getByRole("button", { name: "Create space" }));

    expect(await within(dialog).findByRole("alert")).toHaveTextContent("belongs to a deleted space");
  });

  // The sentence beside the switch is not decoration: whoever writes something
  // sensitive into a page is relying on it, so it says the thing that is true.
  it("closes a space to agents and says what that means", async () => {
    const instance = admin("/spaces/handbook/settings/general", {
      "PATCH /spaces/handbook": { body: { ...aSpace, closed_to_agents: true } },
    });

    expect(await screen.findByText(/it is absent/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole("checkbox", { name: "Closed to agents" }));
    await userEvent.click(screen.getByRole("button", { name: "Save space" }));

    const written = instance.calls.find((call) => call.method === "PATCH")!;
    expect(await written.json()).toMatchObject({ closed_to_agents: true, title: aSpace.title });
  });

  it("renames a space as an act with a warning", async () => {
    const instance = admin("/spaces/handbook/settings/general", {
      "PATCH /spaces/handbook": { body: { ...aSpace, name: "company-handbook" } },
      "GET /spaces/company-handbook/pages": [],
    });

    await userEvent.click(await screen.findByRole("button", { name: "Rename space" }));
    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent("Nothing forwards");

    const field = within(dialog).getByRole("textbox", { name: "New name" });
    await userEvent.clear(field);
    await userEvent.type(field, "company-handbook");
    await userEvent.click(within(dialog).getByRole("button", { name: "Rename space" }));

    const written = instance.calls.find((call) => call.method === "PATCH")!;
    expect(await written.json()).toMatchObject({ name: "company-handbook" });
  });

  it("names a user on a space and takes them off it again", async () => {
    const instance = admin("/spaces/handbook/settings/members", {
      "PUT /spaces/handbook/users/0199a000-0000-7000-8000-000000000009": { status: 204 },
      "DELETE /spaces/handbook/users/0199a000-0000-7000-8000-000000000001": { status: 204 },
    });

    expect(await screen.findByText("maintainer")).toBeInTheDocument();

    await userEvent.selectOptions(screen.getByRole("combobox", { name: "User for handbook" }), other.id);
    await userEvent.click(screen.getByRole("button", { name: "Grant access" }));

    expect(instance.calls.some((call) => call.method === "PUT" && call.url.endsWith(other.id))).toBe(true);

    await userEvent.click(screen.getByRole("button", { name: "Actions for maintainer" }));
    await userEvent.click(await screen.findByRole("menuitem", { name: "Remove access" }));

    expect(instance.calls.some((call) => call.method === "DELETE" && call.url.endsWith(aUser.id))).toBe(true);
  });

  // The interface offers nothing it may not do, and never relies on that: the
  // check stays in the server (ADR 0015).
  it("offers a non-administrator neither the grant nor the delete", async () => {
    admin("/spaces/handbook/settings/members", {}, member);

    expect(await screen.findByText(/is an administrator's act/)).toBeInTheDocument();
    expect(screen.queryByRole("combobox", { name: "User for handbook" })).not.toBeInTheDocument();
  });

  it("finds a deleted space under administration and brings it back", async () => {
    const gone = { ...aSpace, name: "board", title: "The board", deleted_at: "2026-09-10T10:00:00Z" };
    const instance = admin("/admin/spaces", {
      "GET /admin/spaces": [aSpace, gone],
      "POST /spaces/board/restore": { body: { ...gone, deleted_at: null } },
    });

    // Hidden until asked for: a space deleted months ago is not what somebody
    // opening the list came to see.
    expect(await screen.findByText(/The company handbook/)).toBeInTheDocument();
    expect(screen.queryByText("The board")).not.toBeInTheDocument();

    await userEvent.selectOptions(screen.getByRole("combobox", { name: "Deleted" }), "all");
    expect(await screen.findByText("The board")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Actions for board" }));
    await userEvent.click(await screen.findByRole("menuitem", { name: "Restore" }));

    expect(instance.calls.some((call) => new URL(call.url).pathname === "/spaces/board/restore")).toBe(true);
  });
});
