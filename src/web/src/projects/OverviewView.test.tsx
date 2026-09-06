import { screen, within } from "@testing-library/react";
import { expect, it } from "vitest";
import { installInstance, renderAt } from "@/shared/testing";
import { age, reason } from "./standing";
import { OverviewView } from "./OverviewView";

const now = new Date("2026-09-06T12:00:00Z").getTime();

function project(key: string, standing: string, because: string, over: Record<string, unknown> = {}) {
  return {
    key,
    name: key.toLowerCase(),
    standing,
    because,
    needs_you: { question: 0, review: 0, unready: 0, stuck: 0, oldest: null },
    work: { in_progress: 0, ready: 0, open: 0 },
    ...over,
  };
}

it("names the step, the reason and the counts, and leads at the reason", async () => {
  installInstance({
    "GET /standing": {
      agents: 1,
      projects: [
        project("PLAN", "neglected", "question", {
          needs_you: { question: 2, review: 0, unready: 0, stuck: 0, oldest: "2026-08-31T12:00:00Z" },
          work: { in_progress: 3, ready: 12, open: 41 },
        }),
        project("HOST", "idle", "no_agent", { work: { in_progress: 0, ready: 0, open: 7 } }),
        project("LOG", "clear", "nothing"),
      ],
    },
  });

  renderAt("/projects", <OverviewView />);

  const worst = await screen.findByRole("link", { name: /PLAN/ });
  expect(within(worst).getByText("Neglected")).toBeInTheDocument();
  expect(within(worst).getByText("2 questions, oldest 6 days")).toBeInTheDocument();
  expect(within(worst).getByText("3 in progress · 12 ready · 41 open")).toBeInTheDocument();
  expect(worst).toHaveAttribute("href", "/PLAN/needs-you");

  // The order is the instance's and is drawn as it came: the screen does not
  // sort a second time and cannot disagree with the CLI about it.
  const keys = screen.getAllByRole("link").map((link) => link.textContent);
  expect(keys[0]).toContain("PLAN");
  expect(keys[1]).toContain("HOST");
  expect(keys[2]).toContain("LOG");

  expect(screen.getByRole("link", { name: /HOST/ })).toHaveAttribute("href", "/HOST/ready");
  expect(screen.getByRole("link", { name: /LOG/ })).toHaveAttribute("href", "/LOG");

  // At `clear` the tile says so rather than printing three zeros.
  expect(within(screen.getByRole("link", { name: /LOG/ })).getByText("Nothing open")).toBeInTheDocument();
});

it("says once, not per project, that no agent can take anything", async () => {
  installInstance({
    "GET /standing": { agents: 0, projects: [project("PLAN", "idle", "no_agent", { work: { in_progress: 0, ready: 4, open: 4 } })] },
  });

  renderAt("/projects", <OverviewView />);

  expect(await screen.findByText(/No agent can take work on this instance/)).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "Create an agent" })).toHaveAttribute("href", "/settings/agents");
});

it("explains an empty instance and a failed read differently", async () => {
  installInstance({ "GET /standing": { agents: 0, projects: [] } });
  const empty = renderAt("/projects", <OverviewView />);
  expect(await screen.findByText("No project yet.")).toBeInTheDocument();
  empty.unmount();

  installInstance({ "GET /standing": { status: 500, body: { title: "refused", detail: "no" } } });
  renderAt("/projects", <OverviewView />);
  expect(await screen.findByRole("button", { name: "Try again" })).toBeInTheDocument();
});

it("draws every reason as a sentence carrying its number", () => {
  const said = (because: string, over: Record<string, unknown> = {}) =>
    reason(project("X", "waiting", because, over) as never, now);

  expect(said("question", { needs_you: { question: 1, review: 0, unready: 0, stuck: 0, oldest: "2026-09-06T08:00:00Z" } }))
    .toBe("1 question, oldest 4 hours");
  expect(said("review", { needs_you: { question: 0, review: 2, unready: 0, stuck: 0, oldest: "2026-09-06T11:30:00Z" } }))
    .toBe("2 in review, oldest under an hour");
  expect(said("unready", { needs_you: { question: 0, review: 0, unready: 3, stuck: 0, oldest: "2026-09-01T12:00:00Z" } }))
    .toBe("3 issues to triage, oldest 5 days");
  expect(said("stuck", { needs_you: { question: 0, review: 0, unready: 0, stuck: 1, oldest: null } })).toBe("1 issue stuck");
  expect(said("no_agent")).toBe("no agent to pick anything up");
  expect(said("blocked", { work: { in_progress: 0, ready: 0, open: 3 } })).toBe("nothing ready, 3 behind blockers");
  expect(said("nothing_ready")).toBe("nothing an agent could take");
  expect(said("working", { work: { in_progress: 2, ready: 5, open: 9 } })).toBe("2 in progress");
  expect(said("working", { work: { in_progress: 0, ready: 5, open: 9 } })).toBe("5 issues ready");
  expect(said("nothing")).toBe("Nothing open");
});

it("keeps an age coarse enough to read at a glance", () => {
  expect(age("2026-09-06T11:40:00Z", now)).toBe("under an hour");
  expect(age("2026-09-06T11:00:00Z", now)).toBe("1 hour");
  expect(age("2026-09-05T18:00:00Z", now)).toBe("18 hours");
  expect(age("2026-09-03T12:00:00Z", now)).toBe("3 days");
});
