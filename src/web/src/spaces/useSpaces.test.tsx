import { renderHook, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { aPage, installInstance } from "@/shared/testing";
import { useTreeState } from "./useSpaces";

afterEach(() => vi.unstubAllGlobals());

// From one space to the next, the late answer of the space just left used to
// land on top of the one walked into: one flag was shared by every read.
it("keeps the tree of the space walked into when the one left answers late", async () => {
  let answerA!: () => void;
  const heldA = new Promise<void>((resolve) => { answerA = resolve; });

  installInstance({
    "GET /spaces/a/pages": async () => { await heldA; return [aPage("old", "From A", "a")]; },
    "GET /spaces/b/pages": [aPage("new", "From B", "b")],
  });

  const { result, rerender } = renderHook(({ space }) => useTreeState(space), { initialProps: { space: "a" } });
  rerender({ space: "b" });

  await waitFor(() => expect(result.current.tree.at).toBe("known"));
  answerA();
  await heldA;
  await new Promise((resolve) => setTimeout(resolve, 0));

  expect(result.current.space).toBe("b");
  expect(result.current.tree.at === "known" && result.current.tree.pages.map((page) => page.title)).toEqual(["From B"]);
});
