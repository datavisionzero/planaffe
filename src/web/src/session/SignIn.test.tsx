import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { aUser, installInstance, renderAt } from "@/shared/testing";
import { SignIn } from "./SignIn";

afterEach(() => {
  vi.unstubAllGlobals();
  window.localStorage.clear();
});

describe("signing in with a token", () => {
  it("keeps a token the instance recognises", async () => {
    const { calls } = installInstance({
      "GET /me": (request) =>
        request.headers.get("Authorization") === "Bearer pa_right" ? aUser : { status: 401, body: {} },
    });
    const signedIn = vi.fn();
    renderAt("/", <SignIn onSignedIn={signedIn} />);
    const user = userEvent.setup();

    await user.type(screen.getByLabelText("Your token"), "pa_right");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await vi.waitFor(() => signedIn.mock.calls[0]?.[0])).toEqual(aUser);
    expect(window.localStorage.getItem("planaffe.token")).toBe("pa_right");
    expect(calls).toHaveLength(1);
  });

  it("refuses a token the instance does not know, and keeps nothing", async () => {
    installInstance({ "GET /me": { status: 401, body: {} } });
    renderAt("/", <SignIn onSignedIn={vi.fn()} />);
    const user = userEvent.setup();

    await user.type(screen.getByLabelText("Your token"), "pa_wrong");
    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("does not know this token");
    expect(window.localStorage.getItem("planaffe.token")).toBeNull();
  });
});
