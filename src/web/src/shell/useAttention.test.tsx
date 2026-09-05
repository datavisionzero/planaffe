import { act, render, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { useAttentionState } from "./useAttention";

/**
 * The frame's held read of "Needs you". The instance is driven by hand here
 * rather than answering at once, because what this loop is for is the time
 * between a question and its answer: the connection it holds, when it lets go
 * of it, and what the navigation says while it has none.
 */
type Round = { request: Request; answer: (response: Response) => void; refuse: () => void };

const rounds: Round[] = [];

function installHeldInstance() {
  rounds.length = 0;

  vi.stubGlobal(
    "fetch",
    vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const request = input instanceof Request ? input : new Request(input, init);

      return new Promise<Response>((answer, reject) => {
        const refuse = () => reject(new Error("the instance did not answer"));
        request.signal.addEventListener("abort", () => reject(new DOMException("aborted", "AbortError")));
        rounds.push({ request, answer, refuse });
      });
    }),
  );
}

function page(total: number, etag: string): Response {
  return new Response(JSON.stringify({ items: [], total, has_more: false, next_cursor: null, agents: 1 }), {
    status: 200,
    headers: { "content-type": "application/json", etag },
  });
}

/** What the navigation would draw, and how often it was told to look again. */
function Probe({ project }: { project: string | undefined }) {
  const { needsYou, pulse } = useAttentionState(project);

  return <p>{`${needsYou === null ? "nothing known" : needsYou} · ${pulse}`}</p>;
}

function shown(): string {
  return screen.getByText(/·/).textContent!;
}

// Generous, because a round the instance answered `304` waits out the loop's
// floor before it asks again — the one thing that keeps a `304` nobody waited
// for from becoming a spin.
async function round(at: number): Promise<Round> {
  await waitFor(() => expect(rounds.length).toBeGreaterThan(at), { timeout: 3_000 });
  return rounds[at];
}

function query(request: Request) {
  return new URL(request.url).searchParams;
}

let hidden = false;

Object.defineProperty(document, "hidden", { configurable: true, get: () => hidden });

function look(away: boolean) {
  hidden = away;
  act(() => document.dispatchEvent(new Event("visibilitychange")));
}

afterEach(() => {
  vi.unstubAllGlobals();
  hidden = false;
});

it("reads once plainly, then holds a read against the last validator", async () => {
  installHeldInstance();
  render(<Probe project="PLAN" />);

  // The first read is a plain one. A held read with no validator would wait
  // for a first item, and a list that is empty is exactly what the navigation
  // has to be able to say.
  const first = await round(0);
  expect(query(first.request).get("limit")).toBe("1");
  expect(query(first.request).get("wait")).toBeNull();
  expect(first.request.headers.get("If-None-Match")).toBeNull();

  // Nothing changed yet — the first answer is what a screen reading the same
  // list has just read for itself, so no pulse goes with it.
  await act(async () => first.answer(page(2, '"a"')));
  expect(shown()).toBe("2 · 0");

  const second = await round(1);
  expect(query(second.request).get("wait")).toBe("60");
  expect(second.request.headers.get("If-None-Match")).toBe('"a"');

  // The instance said the list changed, so whoever else reads it asks again.
  await act(async () => second.answer(page(3, '"b"')));
  await waitFor(() => expect(shown()).toBe("3 · 1"));

  // A round that reached its deadline changes nothing and is asked again with
  // the validator it went out with.
  const third = await round(2);
  expect(third.request.headers.get("If-None-Match")).toBe('"b"');
  await act(async () => third.answer(new Response(null, { status: 304, headers: { etag: '"b"' } })));

  const fourth = await round(3);
  expect(fourth.request.headers.get("If-None-Match")).toBe('"b"');
  expect(shown()).toBe("3 · 1");
});

it("keeps the number it last knew when the instance stops answering", async () => {
  installHeldInstance();
  render(<Probe project="PLAN" />);

  await act(async () => (await round(0)).answer(page(4, '"a"')));
  expect(shown()).toBe("4 · 0");

  await act(async () => (await round(1)).refuse());

  // A navigation carries no error banner, and an instance that is restarting
  // is not a reason to forget what was true a second ago.
  expect(shown()).toBe("4 · 0");
});

it("holds nothing while nobody is looking, and reads at once on coming back", async () => {
  installHeldInstance();
  render(<Probe project="PLAN" />);

  await act(async () => (await round(0)).answer(page(1, '"a"')));
  const held = await round(1);

  look(true);

  // Under HTTP/1.1 the browser has six connections per origin; a tab nobody is
  // looking at may not spend one of them on a number nobody can see.
  await waitFor(() => expect(held.request.signal.aborted).toBe(true));
  expect(rounds).toHaveLength(2);

  look(false);

  const back = await round(2);
  expect(back.request.headers.get("If-None-Match")).toBe('"a"');
  expect(shown()).toBe("1 · 0");
});

it("drops the read of a project that is no longer open", async () => {
  installHeldInstance();
  const { unmount } = render(<Probe project="PLAN" />);

  const first = await round(0);
  unmount();

  await waitFor(() => expect(first.request.signal.aborted).toBe(true));
});
