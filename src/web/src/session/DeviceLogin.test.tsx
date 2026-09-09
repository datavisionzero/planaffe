import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { aUser, installInstance, renderAt } from "@/shared/testing";
import { DeviceLogin } from "./DeviceLogin";

afterEach(() => vi.unstubAllGlobals());

const waiting = { user_code: "BCDF-GHJK", expires_at: "2026-09-09T10:10:00Z" };

describe("the confirmation page a console is sent to", () => {
  it("looks up a code that arrived in the address, and says what confirming grants", async () => {
    installInstance({ "GET /device-logins/BCDFGHJK": waiting });

    renderAt("/device?code=BCDF-GHJK", <DeviceLogin me={aUser} />);

    expect(await screen.findByRole("heading", { level: 1, name: "Sign in a console?" })).toBeInTheDocument();
    expect(screen.getByText(/BCDF-GHJK/)).toBeInTheDocument();
    // The sentence before the button: whose key it is, and that it does not
    // expire on its own.
    expect(screen.getByText(/key to planaffe as/)).toHaveTextContent("maintainer");
    expect(screen.getByText(/does not expire/)).toBeInTheDocument();
  });

  it("forgives case, spaces and dashes in what a person types", async () => {
    const { calls } = installInstance({ "GET /device-logins/BCDFGHJK": waiting });

    renderAt("/device", <DeviceLogin me={aUser} />);
    const user = userEvent.setup();
    await user.type(screen.getByLabelText("Code"), "bcdf ghjk");

    // Shown as it was printed, whatever was typed.
    expect(screen.getByLabelText("Code")).toHaveValue("BCDF-GHJK");

    await user.click(screen.getByRole("button", { name: "Continue" }));
    expect(await screen.findByRole("heading", { level: 1, name: "Sign in a console?" })).toBeInTheDocument();
    expect(new URL(calls[0].url).pathname).toBe("/device-logins/BCDFGHJK");
  });

  it("confirms, and then says the terminal is where it continues", async () => {
    const { calls } = installInstance({
      "GET /device-logins/BCDFGHJK": waiting,
      "POST /device-logins/BCDFGHJK/decide": waiting,
    });

    renderAt("/device?code=BCDF-GHJK", <DeviceLogin me={aUser} />);
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Sign in this console" }));

    expect(await screen.findByRole("heading", { level: 1, name: "That console is signed in." })).toBeInTheDocument();
    expect(await calls[1].json()).toEqual({ approve: true });
    expect(calls[1].headers.get("X-Planaffe-CSRF")).toBe("1");
  });

  it("refuses, and grants nothing", async () => {
    const { calls } = installInstance({
      "GET /device-logins/BCDFGHJK": waiting,
      "POST /device-logins/BCDFGHJK/decide": waiting,
    });

    renderAt("/device?code=BCDF-GHJK", <DeviceLogin me={aUser} />);
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "I did not start this" }));

    expect(await screen.findByRole("heading", { level: 1, name: "That login was refused." })).toBeInTheDocument();
    expect(await calls[1].json()).toEqual({ approve: false });
  });

  it("says so when nothing is waiting for that code", async () => {
    installInstance({
      "GET /device-logins/BCDFGHJK": { status: 404, body: { detail: "No login is waiting for that code." } },
    });

    renderAt("/device?code=BCDF-GHJK", <DeviceLogin me={aUser} />);

    expect(await screen.findByRole("alert")).toHaveTextContent("No login is waiting for that code.");
    expect(screen.getByRole("heading", { level: 1, name: "Sign in a console" })).toBeInTheDocument();
  });

  it("says so when the code ran out before anybody confirmed it", async () => {
    installInstance({
      "GET /device-logins/BCDFGHJK": {
        status: 410,
        body: { detail: "That login expired before it was confirmed." },
      },
    });

    renderAt("/device?code=BCDF-GHJK", <DeviceLogin me={aUser} />);

    expect(await screen.findByRole("alert")).toHaveTextContent("expired");
  });

  it("says so when somebody already decided it", async () => {
    installInstance({
      "GET /device-logins/BCDFGHJK": waiting,
      "POST /device-logins/BCDFGHJK/decide": {
        status: 403,
        body: { detail: "That login was already refused." },
      },
    });

    renderAt("/device?code=BCDF-GHJK", <DeviceLogin me={aUser} />);
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: "Sign in this console" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("already refused");
  });

  it("waits for eight characters before it will ask", async () => {
    installInstance({});

    renderAt("/device", <DeviceLogin me={aUser} />);
    const user = userEvent.setup();

    expect(screen.getByRole("button", { name: "Continue" })).toBeDisabled();
    await user.type(screen.getByLabelText("Code"), "bcdfghj");
    expect(screen.getByRole("button", { name: "Continue" })).toBeDisabled();
  });
});
