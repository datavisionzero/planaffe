import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route, Routes } from "react-router";
import { afterEach, expect, it, vi } from "vitest";
import { installInstance, renderAt } from "@/shared/testing";
import { ReleaseView } from "./ReleaseView";

afterEach(() => vi.unstubAllGlobals());

function anIssue(key: string, title: string, parent: string | null = null) {
  return {
    key, project: "PLAN", title, status: "done", ready: false, priority: 2, labels: [], epic: null, parent,
    release: null, assignee: null, claim: null, blocked_by: [], open_questions: 0, open_blockers: 0,
    open_sub_issues: 0, created_at: "2026-08-01T10:00:00Z", updated_at: "2026-08-02T10:00:00Z",
    closed_at: "2026-08-02T10:00:00Z", deleted_at: null, deleted_by: null,
  };
}

const open = {
  name: "unreleased",
  status: "open",
  description: "Everything closed since 0.3.0.",
  published_at: null,
  published_by: null,
  issues: [anIssue("PLAN-41", "The epic's own view"), anIssue("PLAN-42", "Its first half", "PLAN-41")],
};

// The route table takes `{ body }` for a release on purpose: a bare object
// carrying a `status` of its own reads as the envelope the helper accepts.
function renderRelease(name: string) {
  return renderAt(`/PLAN/releases/${name}`, <Routes><Route path="/:project/releases/:name" element={<ReleaseView />} /></Routes>);
}

it("shows the notes and the exact membership, sub-issues under their parent", async () => {
  installInstance({ "GET /projects/PLAN/releases/unreleased": { body: open } });
  renderRelease("unreleased");

  expect(await screen.findByText("Everything closed since 0.3.0.")).toBeInTheDocument();
  expect(screen.getByText("Issues · 2")).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "PLAN-41" })).toHaveAttribute("href", "/PLAN/issues/41");
  // The sub-issue stands where the instance put it, indented rather than moved.
  const sub = screen.getByText("Its first half").closest("li")!;
  expect(sub.className).toContain("pl-8");
});

it("copies the release as Markdown the way pa release notes prints it", async () => {
  installInstance({ "GET /projects/PLAN/releases/unreleased": { body: open } });
  renderRelease("unreleased");
  const user = userEvent.setup();

  await user.click(await screen.findByRole("button", { name: "Copy as Markdown" }));

  expect(await navigator.clipboard.readText()).toBe(
    "Everything closed since 0.3.0.\n\n- PLAN-41 The epic's own view\n  - PLAN-42 Its first half\n",
  );
  expect(await screen.findByRole("status")).toHaveTextContent("unreleased copied as Markdown.");
});

it("keeps the notes of a published release editable", async () => {
  const published = { ...open, name: "0.3.0", status: "published", published_at: "2026-08-30T09:00:00Z", published_by: { id: "0199a000-0000-7000-8000-000000000001", name: "maintainer" } };
  const instance = installInstance({
    "GET /projects/PLAN/releases/0.3.0": { body: published },
    "PATCH /projects/PLAN/releases/0.3.0": { body: { ...published, description: "The third cut." } },
  });
  renderRelease("0.3.0");
  const user = userEvent.setup();

  await user.click(await screen.findByRole("button", { name: "Edit notes" }));
  await user.clear(screen.getByLabelText("Notes"));
  await user.type(screen.getByLabelText("Notes"), "The third cut.");
  await user.click(screen.getByRole("button", { name: "Save notes" }));

  const request = await vi.waitFor(() => instance.calls.find((call) => call.method === "PATCH")!);
  expect(await request.json()).toEqual({ description: "The third cut." });
  expect(await screen.findByText("The third cut.")).toBeInTheDocument();
  // Publishing is over; only the annotation is still offered.
  expect(screen.queryByRole("button", { name: "Publish…" })).not.toBeInTheDocument();
});

it("shows the name, the notes and what ships before publishing, and lands on the published release", async () => {
  const publishedBody = { ...open, name: "0.4.0", status: "published", published_at: "2026-09-04T09:00:00Z", published_by: { id: "0199a000-0000-7000-8000-000000000001", name: "maintainer" } };
  const instance = installInstance({
    "GET /projects/PLAN/releases/unreleased": { body: open },
    "GET /projects/PLAN/releases/0.4.0": { body: publishedBody },
    "POST /projects/PLAN/releases/publish": { status: 201, body: publishedBody },
  });
  renderRelease("unreleased");
  const user = userEvent.setup();

  await user.click(await screen.findByRole("button", { name: "Publish…" }));
  const dialog = screen.getByRole("dialog");
  expect(within(dialog).getByText("PLAN-41")).toBeInTheDocument();
  expect(within(dialog).getByLabelText("Notes")).toHaveValue("Everything closed since 0.3.0.");
  await user.type(within(dialog).getByLabelText("Name"), "0.4.0");
  await user.click(within(dialog).getByRole("button", { name: "Publish release" }));

  const request = await vi.waitFor(() => instance.calls.find((call) => call.method === "POST")!);
  expect(await request.json()).toEqual({ name: "0.4.0", description: "Everything closed since 0.3.0." });
  // The open release it was published from is gone; the address follows the name.
  expect(await screen.findByText(/Published .* by maintainer/)).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "Publish…" })).not.toBeInTheDocument();
});

it("says why a refused publish did not happen and keeps what was typed", async () => {
  installInstance({
    "GET /projects/PLAN/releases/unreleased": { body: open },
    "POST /projects/PLAN/releases/publish": { status: 409, body: { detail: "Release 0.4.0 already exists." } },
  });
  renderRelease("unreleased");
  const user = userEvent.setup();

  await user.click(await screen.findByRole("button", { name: "Publish…" }));
  const dialog = screen.getByRole("dialog");
  await user.type(within(dialog).getByLabelText("Name"), "0.4.0");
  await user.click(within(dialog).getByRole("button", { name: "Publish release" }));

  expect(await within(dialog).findByRole("alert")).toHaveTextContent("Release 0.4.0 already exists.");
  expect(within(dialog).getByLabelText("Name")).toHaveValue("0.4.0");
});

it("says a release it could not read is not there, and offers the way back", async () => {
  installInstance({ "GET /projects/PLAN/releases/0.9.0": { status: 404, body: { detail: "No release 0.9.0 in PLAN." } } });
  renderRelease("0.9.0");

  expect(await screen.findByText("No release 0.9.0 in PLAN.")).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "All releases" })).toHaveAttribute("href", "/PLAN/releases");
});
