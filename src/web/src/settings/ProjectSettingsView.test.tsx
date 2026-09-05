import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route, Routes, useLocation } from "react-router";
import { afterEach, expect, it, vi } from "vitest";
import { SessionProvider } from "@/session/Session";
import { aProject, aUser, installInstance, renderAt } from "@/shared/testing";
import { ProjectSettingsView } from "./ProjectSettingsView";

afterEach(() => vi.unstubAllGlobals());

/** Where the click left the reader. */
function At() {
  return <span data-testid="at">{useLocation().pathname}</span>;
}

it("submits the project name and workflow switches", async () => {
  const changed = { ...aProject, name: "Planning", triage_required: true, review_required: true };
  const instance = installInstance({
    "GET /projects/PLAN": aProject,
    "GET /projects/PLAN/users": [],
    "PATCH /projects/PLAN": changed,
  });
  renderAt("/PLAN/settings/general", <SessionProvider value={{ me: aUser, signOut: vi.fn() }}><Routes><Route path="/:project/settings/*" element={<ProjectSettingsView />} /></Routes></SessionProvider>);
  const user = userEvent.setup();

  const name = await screen.findByLabelText("Name");
  await user.clear(name);
  await user.type(name, "Planning");
  await user.click(screen.getByLabelText("Require triage before agents take issues"));
  await user.click(screen.getByLabelText("Require review before issues are done"));
  await user.click(screen.getByRole("button", { name: "Save project" }));

  expect(await screen.findByRole("status")).toHaveTextContent("Saved.");
  const request = instance.calls.find((call) => call.method === "PATCH")!;
  expect(await request.json()).toEqual({ name: "Planning", triage_required: true, review_required: true });
});

// The screen sits under the project, so its own address is not a constant:
// the nav entries have to lead to `/PLAN/settings/…` and not to whatever the
// area currently is, plus an area.
it("moves between areas without growing the address", async () => {
  installInstance({ "GET /projects/PLAN": aProject, "GET /projects/PLAN/users": [] });
  renderAt(
    "/PLAN/settings/general",
    <SessionProvider value={{ me: aUser, signOut: vi.fn() }}>
      <Routes><Route path="/:project/settings/*" element={<ProjectSettingsView />} /></Routes>
      <At />
    </SessionProvider>,
  );
  const user = userEvent.setup();

  await user.click(await screen.findByRole("link", { name: "Members" }));

  expect(screen.getByTestId("at")).toHaveTextContent("/PLAN/settings/members");
  expect(await screen.findByRole("heading", { name: "Members" })).toBeInTheDocument();
});
