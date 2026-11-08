import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { expect, it, vi } from "vitest";
import { installInstance, renderAt } from "@/shared/testing";
import { GroupManager } from "./GroupManager";

const group = { id: "0199a000-0000-7000-8000-000000000010", name: "Products", description: "Existing description" };

it.each([false, true])("saves group content without offering credential autofill (editing: %s)", async (editing) => {
  const method = editing ? "PUT" : "POST";
  const path = editing ? `/project-groups/${group.id}` : "/project-groups";
  const { calls } = installInstance({ [`${method} ${path}`]: group });
  const reload = vi.fn(async () => undefined);
  renderAt("/projects", <GroupManager groups={editing ? [group] : []} reload={reload} />);
  const user = userEvent.setup();
  await user.click(screen.getByRole("button", { name: "Manage groups" }));
  if (editing) await user.click(screen.getByRole("button", { name: "Edit" }));

  const name = screen.getByRole("textbox", { name: "Group name" });
  const description = screen.getByRole("textbox", { name: /Description/ });
  for (const field of [name, description]) {
    expect(field).toHaveAttribute("autocomplete", "off");
    expect(field).toHaveAttribute("data-protonpass-ignore", "true");
  }
  await user.clear(name);
  await user.type(name, "Libraries");
  await user.clear(description);
  await user.type(description, "Shared packages");
  await user.click(screen.getByRole("button", { name: editing ? "Save changes" : "Create group" }));

  expect(await screen.findByRole("status")).toHaveTextContent("Saved.");
  const request = calls.find(call => call.method === method && new URL(call.url).pathname === path)!;
  expect(await request.json()).toEqual({ name: "Libraries", description: "Shared packages" });
  expect(reload).toHaveBeenCalledOnce();
  expect(screen.getByRole("textbox", { name: "Group name" })).toHaveValue("");
});
