import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route, Routes } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import { installInstance, renderAt } from "@/shared/testing";
import { IssueView } from "./IssueView";

const routedIssue = <Routes><Route path="/:project/issues/:number" element={<IssueView />} /></Routes>;

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
    renderAt("/PLAN/issues/9", routedIssue);

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
    renderAt("/PLAN/issues/9", routedIssue);

    expect(await screen.findByRole("link", { name: /^PLAN-1 ·/ })).toHaveAttribute("href", "/PLAN/issues/1");
    expect(screen.getByRole("link", { name: /^PLAN-10 ·/ })).toBeInTheDocument();
    expect(screen.getByText("Issue outside your project access")).toBeInTheDocument();
    expect(screen.getByText("A comment.")).toBeInTheDocument();
  });

  // The address carries the number alone, so a link may not take the project
  // from the page it sits on: PLAN-9 blocked by OTHER-7 led to PLAN-7.
  it("sends a link to the project its key names, not to the project in the address", async () => {
    const across = {
      ...issue,
      epic: { key: "OTHER-E2", title: "Elsewhere", description: "", status: "open" },
      blocked_by: [{ key: "OTHER-7", title: "Elsewhere too", status: "todo", open: true }],
    };
    installInstance({ "GET /issues/PLAN-9": across, "GET /issues/PLAN-9/history": [] });
    renderAt("/PLAN/issues/9", routedIssue);

    for (const link of await screen.findAllByRole("link", { name: /^OTHER-7 ·/ })) {
      expect(link).toHaveAttribute("href", "/OTHER/issues/7");
    }
    expect(screen.getByRole("link", { name: "OTHER-E2" })).toHaveAttribute("href", "/OTHER/epics/E2");
  });

  it("answers the action that needs attention without leaving the issue", async () => {
    const answered = { ...issue.questions[0], answer: "Use the browser path.", answered_by: person, answered_at: "2026-09-04T11:00:00Z" };
    const instance = installInstance({ "GET /issues/PLAN-9": issue, "GET /issues/PLAN-9/history": [], "POST /questions/0199a000-0000-7000-8000-000000000004/answer": { body: answered } });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();

    await user.type(await screen.findByLabelText("Answer"), "Use the browser path.");
    await user.click(screen.getByRole("button", { name: "Answer" }));

    expect(await screen.findByText("Use the browser path.")).toBeInTheDocument();
    expect(await instance.calls.at(-1)!.json()).toEqual({ answer: "Use the browser path." });
  });

  // `GET /issues/{key}` answers 404 `deleted` in the grace period and the view
  // used to print that as a red sentence: the way back existed for the few
  // seconds after a delete and nowhere else.
  it("offers the way back when the key names a deleted issue", async () => {
    const until = new Date(Date.now() + 84 * 3_600_000).toISOString();
    let restored = false;
    installInstance({
      "GET /issues/PLAN-9": () => restored
        ? issue
        : {
            status: 404,
            body: { type: "/problems/deleted", title: "deleted", status: 404, detail: "Issue PLAN-9 is deleted.", restorable_until: until },
          },
      "GET /issues/PLAN-9/history": [],
      "POST /issues/PLAN-9/restore": () => { restored = true; return issue; },
    });
    renderAt("/PLAN/issues/9", routedIssue);

    const restore = await screen.findByRole("button", { name: "Restore issue" });
    expect(screen.getByRole("status")).toHaveTextContent("deleted and hidden from the project");
    expect(screen.getByText(/It can be restored until .* — 3 days left\./)).toBeInTheDocument();
    expect(restore).toHaveFocus();

    await userEvent.setup().click(restore);

    expect(await screen.findByText("Human-first issue")).toBeInTheDocument();
  });

  it("puts the focus on Restore after a delete removed the control that started it", async () => {
    installInstance({ "GET /issues/PLAN-9": issue, "GET /issues/PLAN-9/history": [], "DELETE /issues/PLAN-9": { status: 204 } });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Delete issue" }));
    await user.click(within(await screen.findByRole("dialog")).getByRole("button", { name: "Delete issue" }));

    expect(await screen.findByRole("button", { name: "Restore issue" })).toHaveFocus();
  });
});
