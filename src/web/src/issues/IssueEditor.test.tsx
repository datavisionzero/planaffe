import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route, Routes } from "react-router";
import { afterEach, expect, it, vi } from "vitest";
import { installInstance, renderAt } from "@/shared/testing";
import { NewIssueView } from "./IssueEditor";

afterEach(() => vi.unstubAllGlobals());

it("creates one issue with its fields and opens it", async () => {
  const instance = installInstance({
    "POST /issues": { status: 201, body: { items: [{ key: "PLAN-10" }] } },
  });
  renderAt("/PLAN/issues/new", <Routes><Route path="/:project/issues/new" element={<NewIssueView />} /><Route path="/:project/issues/:key" element={<p>Created issue</p>} /></Routes>);
  const user = userEvent.setup();

  await user.type(screen.getByLabelText("Title"), "Human action");
  await user.type(screen.getByLabelText("Description"), "A **clear** description.");
  await user.click(screen.getByRole("button", { name: "Preview" }));
  expect(screen.getByText("clear").tagName).toBe("STRONG");
  await user.click(screen.getByRole("button", { name: "Edit" }));
  await user.type(screen.getByLabelText("Labels", { exact: false }), "web, cut-three");
  await user.click(screen.getByRole("button", { name: "Create issue" }));

  expect(await screen.findByText("Created issue")).toBeInTheDocument();
  const sent = await instance.calls[0].json();
  expect(sent).toMatchObject({ project: "PLAN", issues: [{ title: "Human action", labels: ["web", "cut-three"] }] });
});
