import { render, screen } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { ThemeProvider } from "@/components/theme-provider";
import { draftPrefix, forgetDrafts, recall, remember } from "./storage";

afterEach(() => {
  vi.restoreAllMocks();
  window.localStorage.clear();
});

function refuseStorage() {
  vi.spyOn(window, "localStorage", "get").mockImplementation(() => {
    throw new DOMException("blocked", "SecurityError");
  });
}

// Signing out leaves the browser to whoever comes next; the ticket text in
// unsent drafts used to stay behind.
it("throws away every draft and nothing else", () => {
  window.localStorage.setItem(`${draftPrefix}origin:user:comment`, "{}");
  window.localStorage.setItem(`${draftPrefix}origin:other:page`, "{}");
  window.localStorage.setItem("planaffe.theme", "dark");

  forgetDrafts();

  expect(window.localStorage.getItem("planaffe.theme")).toBe("dark");
  expect(window.localStorage.length).toBe(1);
});

it("answers nothing and keeps nothing where storage is refused", () => {
  refuseStorage();

  expect(() => remember("local", "key", "value")).not.toThrow();
  expect(recall("local", "key")).toBeNull();
  expect(() => forgetDrafts()).not.toThrow();
});

// The theme was read in the root render without a guard, so a browser that
// refuses storage lost the whole application.
it("draws the application where storage is refused, in the default theme", () => {
  refuseStorage();

  render(<ThemeProvider storageKey="planaffe.theme"><p>The application</p></ThemeProvider>);

  expect(screen.getByText("The application")).toBeInTheDocument();
});
