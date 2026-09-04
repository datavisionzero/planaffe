import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route, Routes } from "react-router";
import { afterEach, expect, it, vi } from "vitest";
import { installInstance, renderAt } from "@/shared/testing";
import { LabelsView } from "./LabelsView";

afterEach(() => vi.unstubAllGlobals());

it("creates a label from the form", async () => {
  const instance = installInstance({
    "GET /projects/PLAN/labels": [],
    "POST /projects/PLAN/labels": {
      status: 201,
      body: { name: "web", group: "area", description: "Browser application" },
    },
  });
  renderAt("/PLAN/labels", <Routes><Route path="/:project/labels" element={<LabelsView />} /></Routes>);
  const user = userEvent.setup();

  await screen.findByRole("button", { name: "Create" });
  await user.type(screen.getByLabelText("Label name"), "web");
  await user.type(screen.getByLabelText("Label group"), "area");
  await user.type(screen.getByLabelText("Label description"), "Browser application");
  await user.click(screen.getByRole("button", { name: "Create" }));

  expect(await vi.waitFor(() => instance.calls.some((call) => call.method === "POST"))).toBe(true);
  const request = instance.calls.find((call) => call.method === "POST")!;
  expect(await request.json()).toEqual({ name: "web", group: "area", description: "Browser application" });
  expect(screen.getByLabelText("Label name")).toHaveValue("");
});
