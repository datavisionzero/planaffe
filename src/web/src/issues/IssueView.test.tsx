import { screen, within } from "@testing-library/react";
import { Route, Routes } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import { installInstance, renderAt } from "@/shared/testing";
import { IssueView } from "./IssueView";

const routedIssue = <Routes><Route path="/:project/issues/:key" element={<IssueView />} /></Routes>;

const person = { id: "0199a000-0000-7000-8000-000000000001", kind: "user", name: "maintainer" };
const agent = { ...person, id: "0199a000-0000-7000-8000-000000000002", kind: "agent", name: "codex" };
const issue = {
  key: "PLAN-9", project: "PLAN", title: "Human-first issue", description: "Long context.\n".repeat(10),
  result: "The delivered **result**.", status: "review", ready: true, priority: 2,
  labels: [{ name: "web", group: null, description: null }], epic: null, parent: { key: "PLAN-1", title: "Parent" }, release: null,
  sub_issues: [{ key: "PLAN-10", title: "Child" }], assignee: null,
  claim: { holder: agent, since: "2026-09-04T08:00:00Z", expires_at: "2026-09-04T09:00:00Z" }, author: person,
  blocked_by: [{ key: "PLAN-2", title: "Database", status: "todo", open: true }], blocks: [{ key: null, title: null, status: null, open: true }],
  open_questions: 1, open_blockers: 1, open_sub_issues: 1,
  comments: [{ id: "0199a000-0000-7000-8000-000000000003", author: person, body: "A comment.", created_at: "2026-09-04T10:00:00Z" }],
  questions: [{ id: "0199a000-0000-7000-8000-000000000004", question: "Which way?", asked_by: agent, asked_at: "2026-09-04T09:00:00Z", answer: null, answered_by: null, answered_at: null }],
  project_context: { key: "PLAN", name: "planaffe", triage_required: false, review_required: true, labels: [] },
  created_at: "2026-09-03T10:00:00Z", updated_at: "2026-09-04T10:00:00Z", closed_at: null,
};

afterEach(() => vi.unstubAllGlobals());

describe("the human-first issue detail", () => {
  it("puts current attention before context and loads the full history", async () => {
    installInstance({
      "GET /issues/PLAN-9": issue,
      "GET /issues/PLAN-9/history": [{ id: 1, actor: person, at: "2026-09-03T10:00:00Z", field: "created", old_value: null, new_value: null, note: null }],
    });
    renderAt("/PLAN/issues/PLAN-9", routedIssue);

    const attention = await screen.findByLabelText("Needs attention");
    expect(within(attention).getByText("Answer needed")).toBeInTheDocument();
    expect(within(attention).getByText("Review needed")).toBeInTheDocument();
    expect(within(attention).getByText("Blocked")).toBeInTheDocument();
    expect(within(attention).getByText("In progress")).toBeInTheDocument();
    expect(attention.compareDocumentPosition(screen.getByText("Description")) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(screen.getByText("Show full description").closest("details")).not.toHaveAttribute("open");
    expect(screen.getByText((_, element) => element?.tagName === "LI" && element.textContent?.includes("maintainer created the issue") === true)).toBeInTheDocument();
  });

  it("keeps relationships, comments, and inaccessible blockers readable", async () => {
    installInstance({ "GET /issues/PLAN-9": issue, "GET /issues/PLAN-9/history": [] });
    renderAt("/PLAN/issues/PLAN-9", routedIssue);

    expect(await screen.findByRole("link", { name: /^PLAN-1 ·/ })).toHaveAttribute("href", "/PLAN/issues/PLAN-1");
    expect(screen.getByRole("link", { name: /^PLAN-10 ·/ })).toBeInTheDocument();
    expect(screen.getByText("Issue outside your project access")).toBeInTheDocument();
    expect(screen.getByText("A comment.")).toBeInTheDocument();
  });
});
