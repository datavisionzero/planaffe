import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route, Routes, useLocation } from "react-router";
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

  renderAt(at, <SessionProvider value={{ me: aUser, signOut: vi.fn() }}><Routes><Route path="/admin/*" element={<AdminView />} /></Routes><At /></SessionProvider>);
  return instance;
}

/** Where the click left the reader. */
function At() {
  return <span data-testid="at">{useLocation().pathname}</span>;
}

/** The row's acts live in its menu; opening it is the first half of clicking one. */
async function act(user: ReturnType<typeof userEvent.setup>, of: string, what: string) {
  await user.click(await screen.findByRole("button", { name: `Actions for ${of}` }));
  await user.click(await screen.findByRole("menuitem", { name: what }));
}

/** The two acts that ask first: the menu entry, then the dialog's own button. */
async function confirm(user: ReturnType<typeof userEvent.setup>, of: string, what: string) {
  await act(user, of, what);
  await user.click(within(await screen.findByRole("dialog")).getByRole("button", { name: what }));
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

  await confirm(user, "maintainer", "Demote");

  expect(await screen.findByRole("status")).toHaveTextContent("Deactivation or demotion would leave no active administrator.");
});

it("says why the last administrator cannot be deactivated", async () => {
  admin({
    "GET /users": [maintainer],
    [`POST /users/${maintainer.id}/deactivate`]: refusal(422, "Deactivation or demotion would leave no active administrator."),
  });
  const user = userEvent.setup();

  await confirm(user, "maintainer", "Deactivate");

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

// The widest of the three areas: `projects/*` matches two segments, so the
// address of the screen is two segments up rather than one. The nav entries
// were relative, and a relative link inside a splat route resolves against
// everything the splat matched: from here "Users" led to
// `/admin/projects/PLAN/users`, which is no area at all, and every further
// click added another segment.
it("leaves a project behind when another area is picked", async () => {
  admin({ "GET /users": [maintainer], "GET /projects/PLAN/users": [maintainer] }, "/admin/projects/PLAN");
  const user = userEvent.setup();

  await user.click(await screen.findByRole("link", { name: "Users" }));

  expect(screen.getByTestId("at")).toHaveTextContent("/admin/users");
  expect(await screen.findByRole("heading", { name: "Users" })).toBeInTheDocument();
});

// An entry that led somewhere nobody stands never read as the current one
// either, not even while its own area was open.
it("marks the area a project is read in as the current one", async () => {
  admin({ "GET /users": [maintainer], "GET /projects/PLAN/users": [maintainer] }, "/admin/projects/PLAN");

  expect(await screen.findByRole("link", { name: "Projects" })).toHaveAttribute("aria-current", "page");
  expect(screen.getByRole("link", { name: "Users" })).not.toHaveAttribute("aria-current");
});

// Deactivating ends every session of that person and stops their tokens and
// agents in the same minute, and "Deactivate" sits directly under "Make admin".
// It hung on the click of the menu entry and fired with no question at all.
it("asks before it deactivates, and sends nothing when the question is answered with no", async () => {
  const instance = admin({
    "GET /users": [maintainer, invited],
    [`POST /users/${invited.id}/deactivate`]: { status: 204 },
  });
  const user = userEvent.setup();

  await act(user, "newcomer", "Deactivate");

  const dialog = await screen.findByRole("dialog");
  expect(dialog).toHaveTextContent("Every browser session of newcomer ends");
  expect(instance.calls.some((call) => call.url.includes("/deactivate"))).toBe(false);

  await user.click(within(dialog).getByRole("button", { name: "Cancel" }));
  expect(instance.calls.some((call) => call.url.includes("/deactivate"))).toBe(false);

  await confirm(user, "newcomer", "Deactivate");
  expect(instance.calls.some((call) => call.url.includes("/deactivate"))).toBe(true);
});

// A question in front of every act is no longer a warning, only a second click.
// Neither of these takes anything away, and neither asks.
it("reactivates and promotes without a question", async () => {
  const deactivated = { ...invited, state: "deactivated" };
  const instance = admin({
    "GET /users": [maintainer, deactivated],
    [`POST /users/${invited.id}/reactivate`]: { status: 204 },
    [`PATCH /users/${invited.id}`]: { ...deactivated, state: "active" },
  });
  const user = userEvent.setup();

  await act(user, "newcomer", "Make admin");
  expect(await screen.findByRole("status")).toHaveTextContent("newcomer is now an administrator.");
  expect(screen.queryByRole("dialog")).not.toBeInTheDocument();

  await act(user, "newcomer", "Reactivate");
  expect(await screen.findByRole("status")).toHaveTextContent("newcomer is active again.");
  expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  expect(instance.calls.some((call) => call.url.includes("/reactivate"))).toBe(true);
});

// The address used to answer a reader without the role with a redirect to the
// issue list: he learned neither that it exists nor that it is not his.
it("says that the administration is not this account's instead of redirecting", async () => {
  installInstance({});
  renderAt(
    "/admin/users",
    <SessionProvider value={{ me: { ...aUser, administrator: false }, signOut: vi.fn() }}>
      <Routes><Route path="/admin/*" element={<AdminView />} /></Routes>
      <At />
    </SessionProvider>,
  );

  expect(await screen.findByRole("status")).toHaveTextContent("Instance administration belongs to administrators");
  expect(screen.getByTestId("at")).toHaveTextContent("/admin/users");
  expect(screen.getByRole("link", { name: "Back to your projects" })).toHaveFocus();
});

// There is no second way into a browser account: a password is recovered
// through a link in an email, and email is optional. Whoever forgot theirs in
// an instance without SMTP was locked out for good.
it("hands over a password link rather than setting a password", async () => {
  const instance = admin({
    "GET /users": [maintainer],
    [`POST /users/${maintainer.id}/recovery-link`]: { link: "/recover?secret=abc", expires_at: "2026-09-06T11:00:00Z" },
  });
  const user = userEvent.setup();

  await act(user, "maintainer", "Password link");

  // The instance answered a path, because it has not been told its public
  // address. The browser is standing at that address and completes it.
  expect(await screen.findByText(new URL("/recover?secret=abc", window.location.origin).toString())).toBeInTheDocument();
  expect(instance.calls.some((call) => call.method === "PATCH")).toBe(false);
});

// An invitation has the same hole without SMTP, and it is closed the same way.
it("offers the invitation as a link an administrator carries over", async () => {
  admin({
    "GET /users": [maintainer, invited],
    [`POST /users/${invited.id}/invitation-link`]: { link: "https://plan.example.test/activate?secret=xyz", expires_at: "2026-09-13T10:00:00Z" },
  });
  const user = userEvent.setup();

  await act(user, "newcomer", "Invitation link");

  expect(await screen.findByText("https://plan.example.test/activate?secret=xyz")).toBeInTheDocument();
});

// A link is a state and not a favour: an invited user has no password to
// recover, and an active one has no invitation left to hand over.
it("offers each user only the link their state has", async () => {
  admin({ "GET /users": [maintainer, invited] });
  const user = userEvent.setup();

  await user.click(await screen.findByRole("button", { name: "Actions for newcomer" }));
  expect(await screen.findByRole("menuitem", { name: "Invitation link" })).toBeInTheDocument();
  expect(screen.queryByRole("menuitem", { name: "Password link" })).not.toBeInTheDocument();
});
