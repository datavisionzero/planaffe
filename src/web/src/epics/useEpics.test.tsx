import { renderHook, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { useLabels } from "@/projects/useLabels";
import { installInstance } from "@/shared/testing";
import { useEpics } from "./useEpics";

afterEach(() => vi.unstubAllGlobals());

function anEpic(key: string) {
  return { key, project: key.slice(0, key.indexOf("-")), title: `Epic ${key}`, status: "open" };
}

it("reads every page of the epics, not the first hundred", async () => {
  const first = Array.from({ length: 100 }, (_, index) => anEpic(`PLAN-E${index + 1}`));
  installInstance({
    "GET /epics": (request) => new URL(request.url).searchParams.get("cursor") === null
      ? { items: first, total: 101, has_more: true, next_cursor: "c2" }
      : { items: [anEpic("PLAN-E101")], total: 101, has_more: false, next_cursor: null },
  });

  const { result } = renderHook(() => useEpics("PLAN"));

  await waitFor(() => expect(result.current).toHaveLength(101));
  expect(result.current.at(-1)?.key).toBe("PLAN-E101");
});

// A screen that stays mounted while the address moves to another project used
// to offer the epics and labels of the project it came from.
it("offers nothing of the last project while the next one's epics are on their way", async () => {
  let answerLog!: () => void;
  const heldLog = new Promise<void>((resolve) => { answerLog = resolve; });
  installInstance({
    "GET /epics": async (request) => {
      if (new URL(request.url).searchParams.get("project") === "LOG") await heldLog;
      const project = new URL(request.url).searchParams.get("project")!;
      return { items: [anEpic(`${project}-E1`)], total: 1, has_more: false, next_cursor: null };
    },
  });

  const { result, rerender } = renderHook(({ project }) => useEpics(project), { initialProps: { project: "PLAN" } });
  await waitFor(() => expect(result.current.map((epic) => epic.key)).toEqual(["PLAN-E1"]));

  rerender({ project: "LOG" });
  expect(result.current).toEqual([]);

  answerLog();
  await waitFor(() => expect(result.current.map((epic) => epic.key)).toEqual(["LOG-E1"]));
});

it("offers nothing of the last project while the next one's labels are on their way", async () => {
  let answerLog!: () => void;
  const heldLog = new Promise<void>((resolve) => { answerLog = resolve; });
  installInstance({
    "GET /projects/PLAN/labels": [{ name: "plan-only", group: null, description: null }],
    "GET /projects/LOG/labels": async () => { await heldLog; return [{ name: "log-only", group: null, description: null }]; },
  });

  const { result, rerender } = renderHook(({ project }) => useLabels(project), { initialProps: { project: "PLAN" } });
  await waitFor(() => expect(result.current.labels.map((label) => label.name)).toEqual(["plan-only"]));

  rerender({ project: "LOG" });
  expect(result.current.labels).toEqual([]);

  answerLog();
  await waitFor(() => expect(result.current.labels.map((label) => label.name)).toEqual(["log-only"]));
});
