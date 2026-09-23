import { api, describe, type Issue } from "@/api/client";

/**
 * The issue as it stands after an act whose answer is only a part of it — a
 * comment, a question, an answer. Every one of them moves the issue's
 * `updated_at` (ADR 0022), so an issue patched together from the old one and
 * the part would carry a version the instance no longer has: the next guarded
 * write would be refused, and an open draft would take the writer's own
 * comment for a change made elsewhere. Where the read fails the patched-up
 * issue is the best there is, and the next wake reads it again.
 */
export async function reread(key: string, fallback: Issue): Promise<Issue> {
  try {
    const { data } = await api.GET("/issues/{key}", { params: { path: { key } } });
    return data ?? fallback;
  } catch {
    return fallback;
  }
}

/** The acts that answer with the whole issue (ADR 0016). */
export type ActPath = "/issues/{key}/claim" | "/issues/{key}/release" | "/issues/{key}/close" | "/issues/{key}/review" | "/issues/{key}/reopen" | "/issues/{key}/restore";

export async function issueRequest(path: ActPath, issue: Issue, body?: object): Promise<Issue> {
  const result = await api.POST(path as "/issues/{key}/claim", { params: { path: { key: issue.key } }, body: body as never });
  if (!result.data) throw new Error(describe(result.error, result.response.status));
  return result.data;
}

export function date(x: string) { return new Date(x).toLocaleString(); }
