import { lazy, Suspense } from "react";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route, Routes } from "react-router";
import { expect, it } from "vitest";
import { renderAt } from "@/shared/testing";
import { ReleaseErrorBoundary } from "./ReleaseErrorBoundary";

const FailedRelease = lazy(() => Promise.reject(new Error("Release chunk did not load")));

it("leaves a way back to the list when the release screen fails to load", async () => {
  renderAt("/PLAN/releases/0.11.0", (
    <Routes>
      <Route path="/:project/releases" element={<p>Release list</p>} />
      <Route path="/:project/releases/:name" element={
        <ReleaseErrorBoundary project="PLAN">
          <Suspense fallback={<p>Loading…</p>}><FailedRelease /></Suspense>
        </ReleaseErrorBoundary>
      } />
    </Routes>
  ));

  expect(await screen.findByText("This release screen could not load.")).toBeInTheDocument();
  await userEvent.setup().click(screen.getByRole("link", { name: "All releases" }));
  expect(await screen.findByText("Release list")).toBeInTheDocument();
});
