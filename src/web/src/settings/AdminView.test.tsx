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
const colleague = { id: "0199a000-0000-7000-8000-000000000003", name: "colleague", email: "colleague@example.test", state: "active", administrator: true };

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

// The row acted on is somebody else's: an administrator is no longer offered
// either act on their own row. Whatever the instance refuses, and for whatever
// reason, the screen owes the reader the sentence.
it("says why an administrator cannot be demoted", async () => {
  admin({
    "GET /users": [maintainer, colleague],
    [`PATCH /users/${colleague.id}`]: refusal(422, "Deactivation or demotion would leave no active administrator."),
  });
  const user = userEvent.setup();

  await confirm(user, "colleague", "Demote");

  expect(await screen.findByRole("status")).toHaveTextContent("Deactivation or demotion would leave no active administrator.");
});

it("says why an administrator cannot be deactivated", async () => {
  admin({
    "GET /users": [maintainer, colleague],
    [`POST /users/${colleague.id}/deactivate`]: refusal(422, "Deactivation or demotion would leave no active administrator."),
  });
  const user = userEvent.setup();

  await confirm(user, "colleague", "Deactivate");

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

  // A deactivated user is not what the list shows without being asked.
  await user.selectOptions(await screen.findByLabelText("State"), "Deactivated");

  // The result count is a live region of its own, so the notice is found by
  // what it says and then held to being announced.
  await act(user, "newcomer", "Make admin");
  expect(await screen.findByText("newcomer is now an administrator.")).toHaveAttribute("role", "status");
  expect(screen.queryByRole("dialog")).not.toBeInTheDocument();

  await act(user, "newcomer", "Reactivate");
  expect(await screen.findByText("newcomer is active again.")).toHaveAttribute("role", "status");
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

// Both lists said everything at once. The default of each is now the live
// half, and the rest is fetched or shown on request.
it("shows only live projects until the deleted are asked for", async () => {
  const gone = { ...aProject, key: "OLD", name: "retired", deleted_at: "2026-09-01T10:00:00Z" };
  const instance = admin({
    "GET /users": [maintainer],
    "GET /admin/projects": (request: Request) =>
      new URL(request.url).searchParams.get("deleted") === "all"
        ? [{ ...aProject, deleted_at: null }, gone]
        : [{ ...aProject, deleted_at: null }],
  }, "/admin/projects");
  const user = userEvent.setup();

  await screen.findByRole("link", { name: "PLAN · planaffe" });
  expect(screen.queryByRole("link", { name: "OLD · retired" })).not.toBeInTheDocument();

  // Not hidden in the client: the parameter exists and the server answers it.
  const asked = instance.calls.map((call) => new URL(call.url)).filter((url) => url.pathname === "/admin/projects");
  expect(asked.every((url) => url.searchParams.get("deleted") === "false")).toBe(true);

  await user.selectOptions(screen.getByLabelText("Deleted"), "Shown");

  expect(await screen.findByRole("link", { name: "OLD · retired" })).toBeInTheDocument();
  expect(instance.calls.some((call) => new URL(call.url).searchParams.get("deleted") === "all")).toBe(true);
});

// A deleted project's row is the row that should be able to undo it, and the
// deletion that was only in the project's own settings is offered here too.
it("restores and deletes a project from its own row", async () => {
  let deleted = true;
  const instance = admin({
    "GET /users": [maintainer],
    "GET /admin/projects": () => [{ ...aProject, deleted_at: deleted ? "2026-09-01T10:00:00Z" : null }],
    "POST /projects/PLAN/restore": () => { deleted = false; return { status: 204 }; },
    "DELETE /projects/PLAN": { status: 204 },
  }, "/admin/projects");
  const user = userEvent.setup();

  await user.selectOptions(await screen.findByLabelText("Deleted"), "Shown");
  await screen.findByText(/Created .* · Deleted /);
  await act(user, "PLAN", "Restore");

  expect(await screen.findByText("PLAN restored.")).toHaveAttribute("role", "status");

  // Deleting takes everything in the project with it, so it asks first and
  // says what goes.
  await act(user, "PLAN", "Delete");
  const dialog = await screen.findByRole("dialog");
  expect(dialog).toHaveTextContent("issues, epics, pages and releases");
  expect(instance.calls.some((call) => call.method === "DELETE")).toBe(false);

  await user.click(within(dialog).getByRole("button", { name: "Delete" }));
  expect(instance.calls.some((call) => call.method === "DELETE")).toBe(true);
});

it("finds a project by key or by name, and says what a search left", async () => {
  admin({
    "GET /users": [maintainer],
    "GET /admin/projects": [{ ...aProject, deleted_at: null }, { ...aProject, key: "LOG", name: "logaffe", deleted_at: null }],
  }, "/admin/projects");
  const user = userEvent.setup();

  await screen.findByRole("link", { name: "PLAN · planaffe" });
  await user.type(screen.getByLabelText("Search"), "log");

  expect(screen.queryByRole("link", { name: "PLAN · planaffe" })).not.toBeInTheDocument();
  expect(screen.getByRole("link", { name: "LOG · logaffe" })).toBeInTheDocument();
  expect(screen.getByText("1 of 2 projects")).toBeInTheDocument();

  // An instance with no projects and a search that matched none of two are
  // not the same sentence.
  await user.clear(screen.getByLabelText("Search"));
  await user.type(screen.getByLabelText("Search"), "nothing");
  expect(screen.getByText("Nothing matches “nothing”.")).toBeInTheDocument();
  expect(screen.queryByText("No projects.")).not.toBeInTheDocument();
});

// Deactivated users stood among the active ones, and the row somebody was
// looking for had to be found by eye.
it("hides deactivated users until the filter asks for them, and searches by name and email", async () => {
  const retired = { ...invited, id: "0199a000-0000-7000-8000-000000000004", name: "retired", email: "retired@example.test", state: "deactivated" };
  admin({ "GET /users": [maintainer, invited, retired] });
  const user = userEvent.setup();

  await screen.findByText("maintainer@example.test · active · administrator");
  expect(screen.getByText("newcomer@example.test · invited")).toBeInTheDocument();
  expect(screen.queryByText("retired@example.test · deactivated")).not.toBeInTheDocument();

  await user.selectOptions(screen.getByLabelText("State"), "Every state");
  expect(screen.getByText("retired@example.test · deactivated")).toBeInTheDocument();
  expect(screen.getByText("3 of 3 users")).toBeInTheDocument();

  await user.type(screen.getByLabelText("Search"), "newcomer@example");
  expect(screen.getByText("newcomer@example.test · invited")).toBeInTheDocument();
  expect(screen.queryByText("maintainer@example.test · active · administrator")).not.toBeInTheDocument();
  expect(screen.getByText("1 of 3 users")).toBeInTheDocument();

  await user.clear(screen.getByLabelText("Search"));
  await user.type(screen.getByLabelText("Search"), "nobody");
  expect(screen.getByText("Nothing matches “nobody”.")).toBeInTheDocument();
});

// The server refuses to leave the instance without an active administrator,
// but that catches only the last one. With two of them either can lock
// themselves out, and the interface does not offer the way in.
it("offers the reader neither deactivation nor demotion on their own row", async () => {
  admin({ "GET /users": [maintainer, colleague] });
  const user = userEvent.setup();

  await user.click(await screen.findByRole("button", { name: "Actions for maintainer" }));
  expect(await screen.findByRole("menuitem", { name: "Password link" })).toBeInTheDocument();
  expect(screen.queryByRole("menuitem", { name: "Deactivate" })).not.toBeInTheDocument();
  expect(screen.queryByRole("menuitem", { name: "Demote" })).not.toBeInTheDocument();

  // Somebody else's row keeps both.
  await user.keyboard("{Escape}");
  await user.click(await screen.findByRole("button", { name: "Actions for colleague" }));
  expect(await screen.findByRole("menuitem", { name: "Deactivate" })).toBeInTheDocument();
  expect(screen.getByRole("menuitem", { name: "Demote" })).toBeInTheDocument();
});
