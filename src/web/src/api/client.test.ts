import { afterEach, expect, it, vi } from "vitest";
import { installInstance } from "@/shared/testing";
import { api } from "./client";

afterEach(() => vi.unstubAllGlobals());

it("asks for the JSON representation of an API address", async () => {
  const instance = installInstance({ "GET /projects": [] });

  await api.GET("/projects");

  expect(instance.calls).toHaveLength(1);
  expect(instance.calls[0]!.headers.get("Accept")).toBe("application/json");
});
