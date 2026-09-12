import { render } from "@testing-library/react";
import type { ReactElement } from "react";
import { MemoryRouter } from "react-router";
import { vi } from "vitest";
import { ThemeProvider } from "@/components/theme-provider";
import { TooltipProvider } from "@/components/ui/tooltip";
import { forgetLabels } from "@/projects/useLabels";

/**
 * An instance to stand in front of the generated client: a route table of
 * `METHOD /path` to what it answers. Anything not listed answers 404 with a
 * problem document, the way the real one would.
 */
export type Answer = { status?: number; body?: unknown } | Record<string, unknown> | unknown[];
export type Route = Answer | ((request: Request) => Answer);

export function installInstance(routes: Record<string, Route>) {
  const calls: Request[] = [];

  // A new instance is a new label set; the shared one outlives a test
  // otherwise, and the next one reads answers this one gave.
  forgetLabels();

  const fetch = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = input instanceof Request ? input : new Request(input, init);
    calls.push(request);

    const url = new URL(request.url);
    const route = `${request.method} ${url.pathname}`;
    const answer = routes[route];

    if (answer === undefined) {
      return problem(404, `no route ${route}`);
    }

    const resolved = typeof answer === "function" ? answer(request) : answer;
    const { status, body } = isEnvelope(resolved) ? resolved : { status: 200, body: resolved };

    return new Response(body === undefined ? null : JSON.stringify(body), {
      status: status ?? 200,
      headers: { "content-type": "application/json" },
    });
  });

  vi.stubGlobal("fetch", fetch);

  return { calls, fetch };
}

function isEnvelope(value: unknown): value is { status?: number; body?: unknown } {
  return typeof value === "object" && value !== null && ("status" in value || "body" in value) && !("key" in value);
}

export function problem(status: number, detail: string): Response {
  return new Response(JSON.stringify({ type: "about:blank", title: "refused", status, detail }), {
    status,
    headers: { "content-type": "application/problem+json" },
  });
}

export function renderAt(path: string, element: ReactElement) {
  return render(
    <ThemeProvider storageKey="test.theme">
      <TooltipProvider>
        <MemoryRouter initialEntries={[path]}>{element}</MemoryRouter>
      </TooltipProvider>
    </ThemeProvider>,
  );
}

export const aUser = {
  id: "0199a000-0000-7000-8000-000000000001",
  kind: "user" as const,
  name: "maintainer",
  administrator: true,
  email: "maintainer@example.test",
  owner: null,
  token: { prefix: "pa_abcd", created_at: "2026-09-02T10:00:00Z" },
  metadata: null,
  metadata_reported_at: null,
  deletion_grace_days: 7,
};

export const aProject = {
  key: "PLAN",
  name: "planaffe",
  triage_required: false,
  review_required: false,
  instructions_page: null,
  created_at: "2026-09-02T10:00:00Z",
  updated_at: "2026-09-02T10:00:00Z",
};

export const aSpace = {
  name: "handbook",
  title: "The company handbook",
  closed_to_agents: false,
  author: { id: aUser.id, kind: "user" as const, name: aUser.name },
  created_at: "2026-09-02T10:00:00Z",
  updated_at: "2026-09-02T10:00:00Z",
  deleted_at: null,
};

/**
 * A page of a space, from its address: the slug, the parent and the depth are
 * what the address already says (ADR 0028), so a test writes the address and
 * not the same thing four times.
 */
export function aSpacePage(path: string, title: string, space = aSpace.name) {
  const segments = path.split("/");

  return {
    path,
    space,
    slug: segments[segments.length - 1]!,
    parent: segments.length > 1 ? segments.slice(0, -1).join("/") : null,
    depth: segments.length - 1,
    title,
    body: "",
    author: { id: aUser.id, kind: "user" as const, name: aUser.name },
    updated_by: { id: aUser.id, kind: "user" as const, name: aUser.name },
    created_at: "2026-09-02T10:00:00Z",
    updated_at: "2026-09-02T10:00:00Z",
  };
}
