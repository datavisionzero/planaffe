import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, expect, it, vi } from "vitest";
import { SessionProvider } from "@/session/Session";
import { aUser, installInstance, renderAt } from "@/shared/testing";
import { SettingsView } from "./SettingsView";

afterEach(() => vi.unstubAllGlobals());

const anAgent = {
  id: "0199a000-0000-7000-8000-000000000009",
  name: "one",
  token: { prefix: "pa_wxyz", created_at: "2026-09-02T10:00:00Z", revoked_at: null },
};

function settings(routes: Parameters<typeof installInstance>[0] = {}) {
  const instance = installInstance({
    "GET /sessions": [],
    "GET /tokens": [],
    "GET /agents": [],
    ...routes,
  });

  renderAt("/settings", <SessionProvider value={{ me: aUser, signOut: vi.fn() }}><SettingsView /></SessionProvider>);
  return instance;
}

// The form is read off the event, and React empties `currentTarget` once the
// event has been dispatched: reading it back after the await threw, and the
// `TypeError` was reported where "Saved." belonged.
it("reports a changed password as saved and clears the fields", async () => {
  settings({ "POST /me/password": { status: 204 } });
  const user = userEvent.setup();

  await user.type(await screen.findByLabelText("Current password"), "a long first password");
  await user.type(screen.getByLabelText("New password"), "a long second password");
  await user.click(screen.getByRole("button", { name: "Change password" }));

  expect(await screen.findByRole("status")).toHaveTextContent("Saved.");
  expect(screen.getByLabelText("Current password")).toHaveValue("");
  expect(screen.getByLabelText("New password")).toHaveValue("");
});

it("reports a created agent as saved, shows its secret once and clears the name", async () => {
  settings({ "POST /agents": { status: 201, body: { ...anAgent, token: { ...anAgent.token, secret: "pa_thesecret" } } } });
  const user = userEvent.setup();

  await user.type(await screen.findByLabelText("Agent name"), "one");
  await user.click(screen.getByRole("button", { name: "Create agent" }));

  expect(await screen.findByText("pa_thesecret")).toBeInTheDocument();
  expect(screen.getByLabelText("Agent name")).toHaveValue("");
  expect(screen.getAllByRole("status").map((x) => x.textContent)).toContain("Saved.");
});
