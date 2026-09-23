import { screen, waitFor, within } from "@testing-library/react";
import { useState, type ReactNode } from "react";
import userEvent from "@testing-library/user-event";
import { Route, Routes } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import { aUser, installInstance, renderAt } from "@/shared/testing";
import { SessionProvider } from "@/session/Session";
import { AttentionContext } from "@/shell/attention";
import { IssueView } from "./IssueView";

// The screen lives under the shell, and it asks who is looking: only the
// author of a comment is offered the correction of it (ADR 0022).
const routedIssue = (
  <SessionProvider value={{ me: aUser, signOut: vi.fn() }}>
    <Routes><Route path="/:project/issues/:number" element={<IssueView />} /></Routes>
  </SessionProvider>
);

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
  workability: { workable: false, parent_gated: false },
  created_at: "2026-09-03T10:00:00Z", updated_at: "2026-09-04T10:00:00Z", closed_at: null,
};

/** The same issue, open and free: what the header offers there is Claim. */
const free = { ...issue, status: "todo", claim: null, result: null, questions: [], workability: { workable: true, parent_gated: false } };

function WithPulse({ children }: { children: ReactNode }) {
  const [issuesPulse, setIssuesPulse] = useState(0);
  return <AttentionContext.Provider value={{ needsYou: null, inProgress: null, pulse: 0, issuesPulse }}>
    <button onClick={() => setIssuesPulse((value) => value + 1)}>Remote change</button>{children}
  </AttentionContext.Provider>;
}

afterEach(() => vi.unstubAllGlobals());

describe("the human-first issue detail", () => {
  it("copies the issue key and a clean direct link from the header by keyboard", async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    installInstance({
      "GET /issues/PLAN-9": free,
      "GET /issues/PLAN-9/history": [],
      "GET /projects/PLAN/needs-you": { items: [{ issue: free, because: "question" }], total: 1, next_cursor: null, has_more: false, agents: 0 },
      "GET /projects/PLAN/users": [],
    });
    renderAt("/PLAN/issues/9?from=needs-you&tab=history", routedIssue);
    const user = userEvent.setup();
    vi.stubGlobal("navigator", Object.create(navigator, { clipboard: { value: { writeText }, configurable: true } }));

    const key = await screen.findByRole("button", { name: "Copy issue key PLAN-9" });
    key.focus();
    await user.keyboard("{Enter}");
    expect(writeText).toHaveBeenLastCalledWith("PLAN-9");
    expect(await screen.findByRole("status")).toHaveTextContent("Issue key copied.");

    const link = screen.getByRole("button", { name: "Copy link" });
    link.focus();
    await user.keyboard("{Enter}");
    const copied = new URL(writeText.mock.lastCall![0]);
    expect(copied.origin).toBe(window.location.origin);
    expect(copied.pathname).toBe("/PLAN/issues/9");
    expect(copied.search).toBe("");
    expect(copied.hash).toBe("");
    expect(copied.username).toBe("");
    expect(copied.password).toBe("");
    expect(await screen.findByRole("status")).toHaveTextContent("Issue link copied.");
  });

  it("reports unavailable and refused clipboard writes without claiming success", async () => {
    const clipboard = { writeText: vi.fn().mockRejectedValue(new Error("Denied")) };
    installInstance({ "GET /issues/PLAN-9": free, "GET /issues/PLAN-9/history": [], "GET /projects/PLAN/users": [] });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();
    vi.stubGlobal("navigator", Object.create(navigator, { clipboard: { value: clipboard, configurable: true } }));

    await user.click(await screen.findByRole("button", { name: "Copy link" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("did not allow copying");
    expect(screen.queryByText("Issue link copied.")).not.toBeInTheDocument();

    Object.defineProperty(navigator, "clipboard", { configurable: true, value: undefined });
    await user.click(screen.getByRole("button", { name: "Copy issue key PLAN-9" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Clipboard is unavailable");
    expect(clipboard.writeText).toHaveBeenCalledTimes(1);
  });

  it("changes priority, labels, and assignee with separate versioned patches", async () => {
    const initial = { ...free, project_context: { ...free.project_context, labels: [{ name: "feature", group: null, description: null }] } };
    const versions = [
      { ...initial, priority: 4, updated_at: "2026-09-05T11:00:00Z" },
      { ...initial, priority: 4, labels: [...initial.labels, initial.project_context.labels[0]], updated_at: "2026-09-05T12:00:00Z" },
      { ...initial, priority: 4, labels: [...initial.labels, initial.project_context.labels[0]], assignee: person, updated_at: "2026-09-05T13:00:00Z" },
      { ...initial, priority: 4, labels: [...initial.labels, initial.project_context.labels[0]], assignee: null, updated_at: "2026-09-05T14:00:00Z" },
    ];
    let writes = 0;
    const instance = installInstance({
      "GET /issues/PLAN-9": initial,
      "GET /issues/PLAN-9/history": [],
      "GET /projects/PLAN/users": [person],
      "PATCH /issues/PLAN-9": () => versions[writes++],
    });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();
    const details = await screen.findByLabelText("Issue details");

    await user.selectOptions(within(details).getByRole("combobox", { name: "Priority" }), "4");
    await waitFor(() => expect(instance.calls.filter((call) => call.method === "PATCH")).toHaveLength(1));
    await screen.findByText("Priority saved.");
    await user.click(within(details).getByRole("combobox", { name: "Labels" }));
    await user.click(screen.getByRole("option", { name: "feature" }));
    await screen.findByText("Labels saved.");
    await user.click(within(details).getByRole("combobox", { name: "Assignee" }));
    await user.click(screen.getByRole("option", { name: /maintainer/ }));
    await screen.findByText("Assignee saved.");
    await user.click(within(details).getByRole("button", { name: "Remove maintainer" }));
    await screen.findByText("Assignee saved.");

    const patches = instance.calls.filter((call) => call.method === "PATCH");
    expect(patches).toHaveLength(4);
    expect(await Promise.all(patches.map((call) => call.json()))).toEqual([
      { priority: 4 }, { labels: ["web", "feature"] }, { assignee: "maintainer" }, { assignee: null },
    ]);
    expect(patches.map((call) => call.headers.get("If-Match"))).toEqual([initial.updated_at, ...versions.slice(0, 3).map((next) => next.updated_at)]);
    expect(within(details).getByRole("combobox", { name: "Assignee" })).toHaveAttribute("placeholder", "Nobody");
  });

  it("adopts the current version after an inline edit conflicts", async () => {
    const current = { ...free, priority: 3, updated_at: "2026-09-05T12:00:00Z" };
    let attempts = 0;
    const instance = installInstance({
      "GET /issues/PLAN-9": free,
      "GET /issues/PLAN-9/history": [],
      "GET /projects/PLAN/users": [],
      "PATCH /issues/PLAN-9": () => ++attempts === 1
        ? { status: 412, body: { type: "/problems/stale", detail: "Changed elsewhere", current } }
        : { ...current, priority: 4 },
    });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();
    const details = await screen.findByLabelText("Issue details");
    const priority = within(details).getByRole("combobox", { name: "Priority" });

    await user.selectOptions(priority, "4");
    expect(await within(details).findByRole("alert")).toHaveTextContent("changed elsewhere");
    expect(priority).toHaveValue("3");
    await user.selectOptions(priority, "4");
    await screen.findByText("Priority saved.");
    const patches = instance.calls.filter((call) => call.method === "PATCH");
    expect(patches).toHaveLength(2);
    expect(patches[1].headers.get("If-Match")).toBe(current.updated_at);
    expect(await patches[1].json()).toEqual({ priority: 4 });
  });

  it("explains a refused inline change and keeps the saved value", async () => {
    installInstance({
      "GET /issues/PLAN-9": free,
      "GET /issues/PLAN-9/history": [],
      "GET /projects/PLAN/users": [],
      "PATCH /issues/PLAN-9": { status: 422, body: { type: "/problems/validation", detail: "Priority cannot be changed here." } },
    });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();
    const details = await screen.findByLabelText("Issue details");
    const priority = within(details).getByRole("combobox", { name: "Priority" });
    await user.selectOptions(priority, "4");
    expect(await within(details).findByRole("alert")).toHaveTextContent("Priority cannot be changed here.");
    expect(priority).toHaveValue("2");
  });
  it("finds cross-project blockers by key and keeps the choice after a cycle refusal", async () => {
    const other = { ...free, key: "OTHER-7", project: "OTHER", title: "External dependency" };
    const linked = { ...free, blocked_by: [...free.blocked_by, { key: "OTHER-7", title: other.title, status: "todo", open: true }], open_blockers: 2, workability: { workable: false, parent_gated: false } };
    let attempts = 0;
    const instance = installInstance({
      "GET /issues/PLAN-9": free,
      "GET /issues/PLAN-9/history": [],
      "GET /issues": { items: [free, other], total: 2, has_more: false, next_cursor: null },
      "GET /issues/OTHER-7": other,
      "POST /issues/PLAN-9/blocked-by/OTHER-7": () => ++attempts === 1
        ? { status: 422, body: { type: "/problems/cycle", title: "cycle", detail: "This edge would close a cycle." } }
        : linked,
    });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();
    await user.click(await screen.findByRole("tab", { name: /Relationships/ }));
    const picker = await screen.findByRole("combobox", { name: "Add blocker" });
    await user.type(picker, "OTHER-7");
    expect(within(screen.getByRole("listbox", { name: "Add blocker" })).queryByText("Human-first issue")).not.toBeInTheDocument();
    await user.click((await screen.findByText("External dependency")).closest<HTMLElement>("[role=option]")!);
    await user.click(screen.getByRole("button", { name: "Add" }));

    expect(await screen.findByText(/close a cycle/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Remove OTHER-7" })).toBeInTheDocument();
    const listCall = instance.calls.find((call) => new URL(call.url).pathname === "/issues")!;
    expect(new URL(listCall.url).searchParams.has("project")).toBe(false);

    await user.click(screen.getByRole("button", { name: "Add" }));
    await waitFor(() => expect(screen.getByRole("tab", { name: "Relationships 5" })).toBeInTheDocument());
    expect(screen.getAllByRole("link", { name: /^OTHER-7 ·/ }).some((link) => link.getAttribute("href") === "/OTHER/issues/7")).toBe(true);
  });

  it("separates Ready from Workable and explains simultaneous blockers", async () => {
    const gated = { ...issue, status: "todo", claim: null, ready: false, project_context: { ...issue.project_context, triage_required: true }, workability: { workable: false, parent_gated: true } };
    const instance = installInstance({
      "GET /issues/PLAN-9": gated,
      "GET /issues/PLAN-9/history": [],
      "PATCH /issues/PLAN-9": { ...gated, ready: true },
    });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();

    const workability = await screen.findByLabelText("Workability");
    expect(within(workability).getByText("Workable: no")).toBeInTheDocument();
    expect(workability).toHaveTextContent("1 open question needs an answer.");
    expect(workability).toHaveTextContent("1 open blocker must close.");
    expect(workability).toHaveTextContent("This project requires Ready before next can take it.");
    expect(workability).toHaveTextContent("Parent");
    await user.click(within(workability).getByRole("button", { name: "Set ready" }));
    const patch = instance.calls.find((call) => call.method === "PATCH")!;
    expect(await patch.json()).toEqual({ ready: true });
  });

  it("shows a positive selection result even when Ready is unset in a project without triage", async () => {
    const workable = { ...free, ready: false, open_questions: 0, open_blockers: 0, open_sub_issues: 0, blocked_by: [], sub_issues: [] };
    installInstance({ "GET /issues/PLAN-9": workable, "GET /issues/PLAN-9/history": [] });
    renderAt("/PLAN/issues/9", routedIssue);
    const workability = await screen.findByLabelText("Workability");
    expect(workability).toHaveTextContent("Workable: yes");
    expect(workability).toHaveTextContent("Ready: not set. Not required by this project.");
  });
  it("continues from Needs you only after the current issue is clear", async () => {
    const waiting = { ...free, questions: issue.questions, open_questions: 1 };
    const next = { ...free, key: "PLAN-10", title: "Next decision", questions: issue.questions, open_questions: 1 };
    const answered = { ...issue.questions[0], answer: "Use the browser path.", answered_by: person, answered_at: "2026-09-04T11:00:00Z" };
    let items = [{ issue: waiting, because: "question" }, { issue: next, because: "question" }];
    installInstance({
      "GET /issues/PLAN-9": waiting,
      "GET /issues/PLAN-9/history": [],
      "GET /issues/PLAN-10": next,
      "GET /issues/PLAN-10/history": [],
      "GET /projects/PLAN/needs-you": () => ({ items, total: items.length, next_cursor: null, has_more: false, agents: 1 }),
      "POST /questions/0199a000-0000-7000-8000-000000000004/answer": () => {
        items = items.slice(1);
        return { body: answered };
      },
    });
    renderAt("/PLAN/issues/9?from=needs-you", routedIssue);
    const user = userEvent.setup();

    expect(await screen.findByRole("link", { name: /Back to Needs you/ })).toHaveAttribute("href", "/PLAN/needs-you");
    expect(screen.queryByRole("button", { name: "Next waiting issue" })).not.toBeInTheDocument();
    await user.type(await screen.findByLabelText("Answer"), "Use the browser path.");
    await user.click(screen.getByRole("button", { name: "Answer" }));
    await user.click(await screen.findByRole("button", { name: "Next waiting issue" }));
    expect(await screen.findByRole("heading", { name: /Next decision/ })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Back to Needs you/ })).toBeInTheDocument();
  });

  it("leaves a direct issue link outside the Needs you workflow", async () => {
    installInstance({ "GET /issues/PLAN-9": free, "GET /issues/PLAN-9/history": [] });
    renderAt("/PLAN/issues/9", routedIssue);
    await screen.findByRole("heading", { name: /Human-first issue/ });
    expect(screen.queryByRole("link", { name: /Back to Needs you/ })).not.toBeInTheDocument();
  });

  it("announces remote edits without replacing text or focus in the editor", async () => {
    let current = free;
    installInstance({ "GET /issues/PLAN-9": () => current, "GET /issues/PLAN-9/history": [] });
    renderAt("/PLAN/issues/9", <WithPulse>{routedIssue}</WithPulse>);
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Edit" }));
    const title = screen.getByRole("textbox", { name: "Title" });
    await user.type(title, " locally");
    current = { ...free, title: "Changed elsewhere", updated_at: "2026-09-05T10:00:00Z" };
    await user.click(screen.getByRole("button", { name: "Remote change" }));

    expect(await screen.findByText(/Your draft is kept/)).toBeInTheDocument();
    expect(title).toHaveValue("Human-first issue locally");
    await user.click(title);
    expect(title).toHaveFocus();
    await user.click(screen.getByRole("button", { name: "Discard draft and load latest" }));
    await user.click(screen.getByRole("button", { name: "Discard" }));
    expect(await screen.findByRole("heading", { name: /Changed elsewhere/ })).toBeInTheDocument();
  });
  it("puts current attention before context and never folds the description away", async () => {
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
    // Long, so it is capped and offers the rest — but it is on the page either way.
    const description = screen.getByText("Description").closest("section")!;
    expect(within(description).getByText(/Long context\./)).toBeInTheDocument();
    expect(within(description).getByRole("button", { name: "Show more" })).toBeInTheDocument();
  });

  // The three long lists used to stack, so acting on an issue meant scrolling
  // past all of them. Conversation is what an open issue is opened for.
  it("shares one tabbed area between conversation, relationships and history", async () => {
    installInstance({
      "GET /issues/PLAN-9": issue,
      "GET /issues/PLAN-9/history": [{ id: 1, actor: person, at: "2026-09-03T10:00:00Z", field: "created", old_value: null, new_value: null, note: null }],
    });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();

    expect(await screen.findByRole("tab", { name: "Conversation 2" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByText("A comment.")).toBeInTheDocument();

    await user.click(screen.getByRole("tab", { name: "Relationships 4" }));
    expect(await screen.findByRole("link", { name: /^PLAN-1 ·/ })).toHaveAttribute("href", "/PLAN/issues/1");
    expect(screen.getByRole("link", { name: /^PLAN-10 ·/ })).toBeInTheDocument();
    expect(screen.getByText("Issue outside your project access")).toBeInTheDocument();

    await user.click(screen.getByRole("tab", { name: "History 1" }));
    expect(await screen.findByText((_, element) => element?.tagName === "LI" && element.textContent?.includes("maintainer created the issue") === true)).toBeInTheDocument();
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
    const user = userEvent.setup();

    await user.click(await screen.findByRole("tab", { name: /Relationships/ }));
    for (const link of await screen.findAllByRole("link", { name: /^OTHER-7 ·/ })) {
      expect(link).toHaveAttribute("href", "/OTHER/issues/7");
    }
    // The chip line and the metadata column both name the epic.
    for (const link of screen.getAllByRole("link", { name: "OTHER-E2" })) {
      expect(link).toHaveAttribute("href", "/OTHER/epics/E2");
    }
  });

  it("answers the action that needs attention without leaving the issue", async () => {
    const answered = { ...issue.questions[0], answer: "Use the browser path.", answered_by: person, answered_at: "2026-09-04T11:00:00Z" };
    let current: Record<string, unknown> = issue;
    const instance = installInstance({
      "GET /issues/PLAN-9": () => current,
      "GET /issues/PLAN-9/history": [],
      "POST /questions/0199a000-0000-7000-8000-000000000004/answer": () => {
        current = { ...issue, questions: [answered], open_questions: 0, updated_at: "2026-09-04T11:00:00Z" };
        return { body: answered };
      },
    });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();

    await user.type(await screen.findByLabelText("Answer"), "Use the browser path.");
    await user.click(screen.getByRole("button", { name: "Answer" }));

    expect(await screen.findByText("Use the browser path.")).toBeInTheDocument();
    expect(await instance.calls.find((call) => call.url.endsWith("/answer"))!.json()).toEqual({ answer: "Use the browser path." });
  });

  // The action used to sit below description, relationships and conversation:
  // one had to scroll past everything the issue says in order to act on it.
  it("offers the one action the status calls for in the header", async () => {
    const instance = installInstance({ "GET /issues/PLAN-9": issue, "GET /issues/PLAN-9/history": [], "POST /issues/PLAN-9/close": { ...issue, status: "done" } });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();

    const header = (await screen.findByRole("heading", { name: /Human-first issue/ })).parentElement!.parentElement!;
    await user.click(within(header).getByRole("button", { name: "Accept as done" }));

    // The close, by name rather than as the last call: closing an issue that
    // was open asks the overview once afterwards, in case that was the last
    // one open in the project.
    const closing = instance.calls.find((call) => call.url.endsWith("/issues/PLAN-9/close"))!;
    expect(await closing.json()).toEqual({ status: "done", result: issue.result });
    await waitFor(() => expect(instance.calls.some((call) => call.url.endsWith("/standing"))).toBe(true));
  });

  it("offers Claim on a free issue and keeps the rest of the verbs in the overflow", async () => {
    const instance = installInstance({ "GET /issues/PLAN-9": free, "GET /issues/PLAN-9/history": [], "POST /issues/PLAN-9/claim": { ...free, claim: { holder: person, since: "2026-09-05T08:00:00Z", expires_at: null } } });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Claim" }));
    expect(await instance.calls.find((call) => call.url.endsWith("/claim"))!.json()).toEqual({ force: false });

    await user.click(screen.getByRole("button", { name: "More actions" }));
    const menu = await screen.findByRole("menu");
    expect(within(menu).getByRole("menuitem", { name: "Release claim" })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: "Clear ready" })).toBeInTheDocument();
    expect(within(menu).getByRole("menuitem", { name: "Delete issue" })).toBeInTheDocument();
  });

  // Nothing is typed at this button, so a stale refusal has nothing to merge —
  // but the version it carries is still what makes the next press possible.
  it("shows the version a stale refusal carried and lets the same press work", async () => {
    const current = { ...free, ready: false, priority: 0, updated_at: "2026-09-05T12:00:00Z" };
    let patched = 0;
    const instance = installInstance({
      "GET /issues/PLAN-9": free,
      "GET /issues/PLAN-9/history": [],
      "PATCH /issues/PLAN-9": () =>
        ++patched === 1
          ? { status: 412, body: { type: "/problems/stale", detail: "PLAN-9 changed at …", current } }
          : { body: { ...current, ready: true } },
    });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "More actions" }));
    await user.click(await screen.findByRole("menuitem", { name: "Clear ready" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("PLAN-9 changed while it was open.");
    // The screen took the version the refusal carried, so the same press is a
    // press that can work rather than one that is refused for ever.
    await user.click(screen.getByRole("button", { name: "More actions" }));
    await user.click(await screen.findByRole("menuitem", { name: "Set ready" }));

    const second = await vi.waitFor(() => {
      const calls = instance.calls.filter((call) => call.method === "PATCH");
      if (calls.length < 2) throw new Error("no second PATCH yet");
      return calls[1]!;
    });
    expect(second.headers.get("If-Match")).toBe("2026-09-05T12:00:00Z");
  });

  // ADR 0022: the author corrects their own comment in the field they wrote it
  // in, and the correction is visible. What the caller may not do is not
  // offered — the check itself is the instance's.
  it("corrects its author's own comment in place and says that it was edited", async () => {
    const corrected = { ...issue.comments[0], body: "A corrected comment.", edited_at: "2026-09-05T11:00:00Z" };
    let current: Record<string, unknown> = free;
    const instance = installInstance({
      "GET /issues/PLAN-9": () => current,
      "GET /issues/PLAN-9/history": [],
      "PATCH /comments/0199a000-0000-7000-8000-000000000003": () => {
        current = { ...free, comments: [corrected], updated_at: "2026-09-05T11:00:00Z" };
        return { body: corrected };
      },
    });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();

    expect(await screen.findByText("A comment.")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Actions on the comment by maintainer" }));
    await user.click(await screen.findByRole("menuitem", { name: "Edit comment" }));

    // The field opens in the text it is correcting rather than empty.
    const field = await screen.findByLabelText("Save comment");
    expect(field).toHaveValue("A comment.");
    await user.clear(field);
    await user.type(field, "A corrected comment.");
    await user.click(screen.getByRole("button", { name: "Save comment" }));

    expect(await screen.findByText("A corrected comment.")).toBeInTheDocument();
    expect(await instance.calls.find((call) => call.method === "PATCH")!.json()).toEqual({ body: "A corrected comment." });
    expect(screen.getByText("edited")).toBeInTheDocument();
  });

  it("takes a comment away for good, once it has been confirmed", async () => {
    let current: Record<string, unknown> = free;
    const instance = installInstance({
      "GET /issues/PLAN-9": () => current,
      "GET /issues/PLAN-9/history": [],
      "DELETE /comments/0199a000-0000-7000-8000-000000000003": () => {
        current = { ...free, comments: [], updated_at: "2026-09-05T11:00:00Z" };
        return { status: 204 };
      },
    });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();

    expect(await screen.findByText("A comment.")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Actions on the comment by maintainer" }));
    await user.click(await screen.findByRole("menuitem", { name: "Delete comment" }));

    // No grace period, so the dialog says so before it happens.
    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent("gone for good");
    await user.click(within(dialog).getByRole("button", { name: "Delete comment" }));

    await waitFor(() => expect(screen.queryByText("A comment.")).toBeNull());
    expect(instance.calls.some((call) => call.method === "DELETE")).toBe(true);
    // The menu that opened the dialog went with the comment; the focus lands on
    // what the conversation offers next rather than on the page.
    await waitFor(() => expect(screen.getByRole("button", { name: "Add comment" })).toHaveFocus());
  });

  // A comment somebody else wrote: a user may clear it up, and nobody but its
  // author may rewrite it.
  it("offers only the delete on somebody else's comment", async () => {
    installInstance({
      "GET /issues/PLAN-9": { ...free, comments: [{ ...issue.comments[0], author: agent }] },
      "GET /issues/PLAN-9/history": [],
    });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Actions on the comment by codex" }));
    const menu = await screen.findByRole("menu");
    expect(within(menu).getByRole("menuitem", { name: "Delete comment" })).toBeInTheDocument();
    expect(within(menu).queryByRole("menuitem", { name: "Edit comment" })).not.toBeInTheDocument();
  });

  // An always-open comment box invites the comment nobody needed, and it sat
  // under the actions rather than under the thread it belongs to.
  it("opens the comment field on a button, inside the conversation", async () => {
    const comment = { id: "0199a000-0000-7000-8000-000000000005", author: person, body: "Looked at it.", created_at: "2026-09-05T10:00:00Z" };
    let current: Record<string, unknown> = free;
    const instance = installInstance({
      "GET /issues/PLAN-9": () => current,
      "GET /issues/PLAN-9/history": [],
      "POST /issues/PLAN-9/comments": () => {
        current = { ...free, comments: [...free.comments, comment], updated_at: "2026-09-05T10:00:00Z" };
        return { body: comment };
      },
    });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();

    expect(await screen.findByRole("button", { name: "Add comment" })).toBeInTheDocument();
    expect(screen.queryByLabelText("Add comment")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Add comment" }));
    await user.type(await screen.findByLabelText("Add comment"), "Looked at it.");
    await user.click(screen.getByRole("button", { name: "Add comment" }));

    expect(await screen.findByText("Looked at it.")).toBeInTheDocument();
    expect(await instance.calls.find((call) => call.url.endsWith("/comments"))!.json()).toEqual({ body: "Looked at it." });
  });

  // A comment moves the issue's `updated_at` (ADR 0022). The screen used to
  // patch the comment into the issue it held and keep the old version, so the
  // next guarded write was refused for the writer's own comment.
  it("writes against the version its own comment left behind", async () => {
    const comment = { id: "0199a000-0000-7000-8000-000000000005", author: person, body: "Looked at it.", created_at: "2026-09-05T10:00:00Z" };
    let current: Record<string, unknown> = free;
    const instance = installInstance({
      "GET /issues/PLAN-9": () => current,
      "GET /issues/PLAN-9/history": [],
      "GET /projects/PLAN/users": [],
      "POST /issues/PLAN-9/comments": () => {
        current = { ...free, comments: [...free.comments, comment], updated_at: "2026-09-05T10:00:00Z" };
        return { body: comment };
      },
      "PATCH /issues/PLAN-9": () => ({ ...current, priority: 4, updated_at: "2026-09-05T10:05:00Z" }),
    });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Add comment" }));
    await user.type(await screen.findByLabelText("Add comment"), "Looked at it.");
    await user.click(screen.getByRole("button", { name: "Add comment" }));
    expect(await screen.findByText("Looked at it.")).toBeInTheDocument();

    await user.selectOptions(within(screen.getByLabelText("Issue details")).getByRole("combobox", { name: "Priority" }), "4");
    await screen.findByText("Priority saved.");

    expect(instance.calls.find((call) => call.method === "PATCH")!.headers.get("If-Match")).toBe("2026-09-05T10:00:00Z");
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
    // The focus is put there by an effect, which runs after the button is in
    // the document: waited for, not read once, or the assertion is a race that
    // only loses on a busy machine.
    await waitFor(() => expect(restore).toHaveFocus());

    await userEvent.setup().click(restore);

    expect(await screen.findByText("Human-first issue")).toBeInTheDocument();
  });

  it("puts the focus on Restore after a delete removed the control that started it", async () => {
    installInstance({ "GET /issues/PLAN-9": issue, "GET /issues/PLAN-9/history": [], "DELETE /issues/PLAN-9": { status: 204 } });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "More actions" }));
    await user.click(within(await screen.findByRole("menu")).getByRole("menuitem", { name: "Delete issue" }));
    await user.click(within(await screen.findByRole("dialog")).getByRole("button", { name: "Delete issue" }));

    const restore = await screen.findByRole("button", { name: "Restore issue" });
    await waitFor(() => expect(restore).toHaveFocus());
  });

  // VISION 7 promises it in as many words: a ticket that has not shipped yet
  // simply does not belong, and moving one by hand still works.
  it("puts an issue into the open release by hand and takes it out again", async () => {
    let put = false;
    const instance = installInstance({
      "GET /issues/PLAN-9": () => (put ? { ...free, release: "unreleased" } : free),
      "GET /issues/PLAN-9/history": [],
      "PUT /projects/PLAN/releases/unreleased/issues/PLAN-9": () => { put = true; return { body: { name: "unreleased", status: "open", description: "", published_at: null, published_by: null, issues: [] } }; },
    });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "More actions" }));
    await user.click(await screen.findByRole("menuitem", { name: "Put into the open release" }));

    expect(await vi.waitFor(() => instance.calls.some((call) => call.method === "PUT"))).toBe(true);
    expect(await screen.findByText("unreleased")).toBeInTheDocument();
  });

  it("offers no release move on an issue that has shipped", async () => {
    installInstance({ "GET /issues/PLAN-9": { ...free, release: "0.4.0" }, "GET /issues/PLAN-9/history": [] });
    renderAt("/PLAN/issues/9", routedIssue);
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "More actions" }));

    expect(screen.queryByRole("menuitem", { name: /open release/ })).not.toBeInTheDocument();
  });
});
