import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route, Routes } from "react-router";
import { afterEach, expect, it, vi } from "vitest";
import { SessionProvider } from "@/session/Session";
import { aProject, aUser, installInstance, renderAt } from "@/shared/testing";
import { AdminView } from "./AdminView";

afterEach(() => vi.unstubAllGlobals());

const maintainer = { id: aUser.id, name: "maintainer", email: "maintainer@example.test", state: "active", administrator: true };
const invited = { id: "0199a000-0000-7000-8000-000000000002", name: "newcomer", email: "newcomer@example.test", state: "invited", administrator: false };

function admin(routes: Parameters<typeof installInstance>[0], at = "/admin/users") {
  const instance = installInstance({
    "GET /admin/projects": [{ ...aProject, deleted_at: null }],
    "GET /admin/smtp": { configured: false, host: null, port: null, security: null, sender: null },
    ...routes,
  });

  renderAt(at, <SessionProvider value={{ me: aUser, signOut: vi.fn() }}><Routes><Route path="/admin/*" element={<AdminView />} /></Routes></SessionProvider>);
  return instance;
}

/** The row's acts live in its menu; opening it is the first half of clicking one. */
async function act(user: ReturnType<typeof userEvent.setup>, of: string, what: string) {
  await user.click(await screen.findByRole("button", { name: `Actions for ${of}` }));
  await user.click(await screen.findByRole("menuitem", { name: what }));
}

// The reload after a successful invite used to be unreachable: the form was
// read back off the event after the await, which threw before `load()` ran.
it("shows an invited user without a reload of the page", async () => {
  let sent = false;
  admin({
    "GET /users": () => (sent ? [maintainer, invited] : [maintainer]),
    "POST /users": () => { sent = true; return { status: 201, body: invited }; },
  });
  const user = userEvent.setup();

  await screen.findByText("maintainer@example.test · active · administrator");
  await user.type(screen.getByLabelText("Name"), "newcomer");
  await user.type(screen.getByLabelText("Email"), "newcomer@example.test");
  await user.click(screen.getByRole("button", { name: "Invite" }));

  expect(await screen.findByText("newcomer@example.test · invited")).toBeInTheDocument();
  expect(screen.getByLabelText("Name")).toHaveValue("");
});

// An empty select contributes no form entry, and the id read back out of it
// was the string "null", which went out as `PUT /projects/PLAN/users/null`.
it("offers no access to grant when everybody already has it", async () => {
  const instance = admin({
    "GET /users": [maintainer],
    "GET /projects/PLAN/users": [maintainer],
  }, "/admin/projects/PLAN");
  const user = userEvent.setup();

  const grant = await screen.findByRole("button", { name: "Grant access" });
  expect(grant).toBeDisabled();
  expect(within(screen.getByLabelText("User for PLAN")).queryAllByRole("option")).toHaveLength(0);

  await user.click(grant);
  expect(instance.calls.some((call) => call.method === "PUT")).toBe(false);
});

// A refusal is a sentence the screen owes the reader. Both of these used to
// be `.then(load)`: the list reloaded unchanged and nothing said why.
const refusal = (status: number, detail: string) => ({ status, body: { type: "about:blank", title: "refused", status, detail } });

it("says why the last administrator cannot be demoted", async () => {
  admin({
    "GET /users": [maintainer],
    [`PATCH /users/${maintainer.id}`]: refusal(422, "Deactivation or demotion would leave no active administrator."),
  });
  const user = userEvent.setup();

  await act(user, "maintainer", "Demote");

  expect(await screen.findByRole("status")).toHaveTextContent("Deactivation or demotion would leave no active administrator.");
});

it("says why the last administrator cannot be deactivated", async () => {
  admin({
    "GET /users": [maintainer],
    [`POST /users/${maintainer.id}/deactivate`]: refusal(422, "Deactivation or demotion would leave no active administrator."),
  });
  const user = userEvent.setup();

  await act(user, "maintainer", "Deactivate");

  expect(await screen.findByRole("status")).toHaveTextContent("Deactivation or demotion would leave no active administrator.");
});

// "Invitation resent." was set from a `.then()` that never looked, so a resend
// that failed reported the opposite of what happened.
it("does not report a resent invitation that did not go out", async () => {
  admin({
    "GET /users": [maintainer, invited],
    [`POST /users/${invited.id}/invitation`]: refusal(503, "Transactional email is not configured."),
  });
  const user = userEvent.setup();

  await act(user, "newcomer", "Resend invitation");

  const notice = await screen.findByRole("status");
  expect(notice).toHaveTextContent("Transactional email is not configured.");
  expect(notice).not.toHaveTextContent("Invitation resent.");
});

it("lands on the users area when no area is named", async () => {
  admin({ "GET /users": [maintainer] }, "/admin");

  expect(await screen.findByRole("heading", { name: "Users" })).toBeInTheDocument();
});

// The single box asked every project for its access list on opening, to show
// one of them. The list asks for none, and the detail asks for its own.
it("asks for a project's access only on the project's own address", async () => {
  const second = { ...aProject, key: "LOG", name: "logaffe", deleted_at: null };
  const instance = admin({
    "GET /users": [maintainer],
    "GET /admin/projects": [{ ...aProject, deleted_at: null }, second],
    "GET /projects/PLAN/users": [maintainer],
  }, "/admin/projects");
  const user = userEvent.setup();

  await screen.findByRole("link", { name: "PLAN · planaffe" });
  expect(instance.calls.some((call) => call.url.includes("/users") && call.url.includes("/projects/"))).toBe(false);

  await user.click(screen.getByRole("link", { name: "PLAN · planaffe" }));
  expect(await screen.findByRole("button", { name: "Grant access" })).toBeInTheDocument();
  expect(instance.calls.filter((call) => new URL(call.url).pathname.endsWith("/users") && new URL(call.url).pathname.startsWith("/projects/"))).toHaveLength(1);
});
