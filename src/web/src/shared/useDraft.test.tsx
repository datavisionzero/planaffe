import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, expect, it } from "vitest";
import { aUser, renderAt } from "./testing";
import { useDraft } from "./useDraft";

const key = `planaffe.draft:${location.origin}:${aUser.id}:comment`;

function Field() {
  const { value, setValue, recovery } = useDraft("comment", "", null);
  return <>{recovery}<label>Comment<textarea id="comment" value={value} onChange={(event) => setValue(event.target.value)} /></label></>;
}

afterEach(() => window.localStorage.clear());

// The field stays editable under the banner. What was typed there used to be
// neither saved nor guarded, and leaving the screen lost it without a word.
it("takes typing under the banner as the answer to it, and keeps what is typed", async () => {
  window.localStorage.setItem(key, JSON.stringify({ value: "An older draft", baseVersion: null, savedAt: "2026-09-20T10:00:00Z" }));
  renderAt("/", <Field />);
  const user = userEvent.setup();

  expect(await screen.findByText("A saved draft is available for this form.")).toBeInTheDocument();
  await user.type(screen.getByRole("textbox", { name: "Comment" }), "New text");

  expect(screen.queryByText("A saved draft is available for this form.")).not.toBeInTheDocument();
  expect(JSON.parse(window.localStorage.getItem(key)!).value).toBe("New text");
});

it("still restores the waiting draft on request", async () => {
  window.localStorage.setItem(key, JSON.stringify({ value: "An older draft", baseVersion: null, savedAt: "2026-09-20T10:00:00Z" }));
  renderAt("/", <Field />);
  const user = userEvent.setup();

  await user.click(await screen.findByRole("button", { name: "Restore draft" }));

  expect(screen.getByRole("textbox", { name: "Comment" })).toHaveValue("An older draft");
});
