import { useState } from "react";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { expect, it, vi } from "vitest";
import { useLocation } from "react-router";
import { aProject, installInstance, renderAt } from "@/shared/testing";
import { ProjectSwitcher } from "./ProjectSwitcher";
const group = { id: "0199a000-0000-7000-8000-000000000010", name: "Products", description: null };
function Picker() {
  const [open, setOpen] = useState(true);
  const location = useLocation();
  return <><output aria-label="Location">{location.pathname}</output><ProjectSwitcher open={open} onOpenChange={setOpen} current={aProject} viewPath="in-progress" reload={vi.fn()} projects={{ at: "known", projects: [{ ...aProject, group_id: group.id }, { ...aProject, key: "LOG", name: "logaffe", group_id: group.id }, { ...aProject, key: "WEB", name: "Website", group_id: null }] }} /></>;
}
it("searches names, keys and groups, and opens a keyboard result in the same view", async () => {
  installInstance({ "GET /project-groups": [group] }); renderAt("/PLAN/in-progress", <Picker />);
  const user = userEvent.setup();
  const search = await screen.findByRole("combobox", { name: "Search projects" });
  await user.type(search, "Products");
  await waitFor(() => expect(screen.getAllByRole("option")).toHaveLength(2));
  expect(screen.getByRole("option", { name: /planaffe/ })).toHaveAttribute("aria-selected", "true");
  await user.clear(search); await user.type(search, "log{Enter}");
  expect(screen.getByLabelText("Location")).toHaveTextContent("/LOG/in-progress");
  expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
});
it("filters ungrouped projects and explains an empty search", async () => {
  installInstance({ "GET /project-groups": [group] }); renderAt("/PLAN/in-progress", <Picker />);
  const user = userEvent.setup();
  await user.click(await screen.findByRole("button", { name: /Ungrouped/ }));
  expect(screen.getAllByRole("option")).toHaveLength(1);
  await user.type(screen.getByRole("combobox"), "missing");
  expect(screen.getByText("No matching projects")).toBeInTheDocument();
  await user.keyboard("{Escape}");
  expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
});
