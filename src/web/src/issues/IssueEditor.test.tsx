import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route, Routes } from "react-router";
import { afterEach, expect, it, vi } from "vitest";
import { installInstance, renderAt } from "@/shared/testing";
import type { Issue } from "@/api/client";
import { EditIssueForm, NewIssueView } from "./IssueEditor";

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

it("does not send an unchanged parking status while editing fields", async () => {
  const person = { id: "0199a000-0000-7000-8000-000000000001", kind: "user" as const, name: "maintainer" };
  const issue: Issue = {
    key: "PLAN-10", project: "PLAN", title: "Before", description: "Context", result: null,
    status: "todo", ready: true, priority: 1, labels: [{ name: "web", group: null, description: null }],
    epic: null, parent: null, release: null, sub_issues: [], assignee: null, claim: null, author: person,
    blocked_by: [], blocks: [], open_questions: 0, open_blockers: 0, open_sub_issues: 0, comments: [], questions: [],
    project_context: { key: "PLAN", name: "planaffe", triage_required: false, review_required: false, labels: [] },
    created_at: "2026-09-02T10:00:00Z", updated_at: "2026-09-02T10:00:00Z", closed_at: null,
  };
  const instance = installInstance({ "PATCH /issues/PLAN-10": { body: { ...issue, title: "After", priority: 3 } } });
  renderAt("/", <EditIssueForm issue={issue} onSaved={vi.fn()} onCancel={vi.fn()} />);
  const user = userEvent.setup();

  await user.clear(screen.getByLabelText("Title"));
  await user.type(screen.getByLabelText("Title"), "After");
  await user.selectOptions(screen.getByLabelText("Priority"), "3");
  await user.click(screen.getByRole("button", { name: "Save changes" }));

  await vi.waitFor(() => expect(instance.calls.some((call) => call.method === "PATCH")).toBe(true));
  const request = instance.calls.find((call) => call.method === "PATCH")!;
  const sent = await request.json();
  expect(sent).toMatchObject({ title: "After", priority: 3 });
  expect(sent).not.toHaveProperty("status");
});
