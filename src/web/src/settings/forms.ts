/** What the administration screens do with a form and a timestamp. */

export const date = (value: string) => new Date(value).toLocaleString();

/** The same instant where the hour says nothing: a project was created on a day. */
export const day = (value: string) => new Date(value).toLocaleDateString();

/**
 * The day a deleted thing stops being restorable: the instant it went plus the
 * instance's grace period, which `GET /me` carries as `deletion_grace_days`.
 *
 * The span is added here rather than delivered beside every `deleted_at`. One
 * number on the caller answers the projects, and answers epics, pages and
 * labels the same way when their screens come to ask (PLAN-73). Whoever prints
 * this says "at least": the purge is opportunistic, so the grace period is a
 * floor and a project nobody writes to keeps its deleted rows longer
 * (ADR 0013).
 */
export const restorableUntil = (deletedAt: string, graceDays: number) =>
  day(new Date(new Date(deletedAt).getTime() + graceDays * 24 * 60 * 60 * 1000).toISOString());

/**
 * A form submitted to the instance. React empties `currentTarget` once the
 * event has been dispatched, so the form is taken here and handed to the
 * action: one that awaits and then reads it back off the event finds null, and
 * the `TypeError` that follows is reported as if the write had failed.
 */
export async function submitting(
  event: React.FormEvent<HTMLFormElement>,
  setNotice: (notice: string) => void,
  action: (data: FormData, form: HTMLFormElement) => Promise<void>,
) {
  event.preventDefault();
  const form = event.currentTarget;
  setNotice("");

  try {
    await action(new FormData(form), form);
    setNotice("Saved.");
  } catch (error) {
    setNotice(error instanceof Error ? error.message : "The instance did not answer.");
  }
}
