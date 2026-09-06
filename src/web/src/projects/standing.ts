import { HourglassIcon, PauseIcon, ThumbsDownIcon, ThumbsUpIcon, type LucideIcon } from "lucide-react";
import type { Schemas } from "@/api/client";

export type ProjectStanding = Schemas["ProjectStanding"];
export type Standing = Schemas["Standing"];

/**
 * How one step is drawn. The instance decides which step a project is on
 * (ADR 0024); this decides only what that looks like, and it never draws the
 * step with colour alone — the icon, the word and the line under it each say
 * it on their own, the way an issue row marks status and priority twice over.
 */
export type Look = {
  /** The word under the key, and what a screen reader is given. */
  word: string;
  /** How many of the icon are drawn: two thumbs are the two ends of the scale. */
  icon: LucideIcon;
  icons: 1 | 2;
  /** The token the icon and the border take, from `index.css`. */
  tone: string;
  /** Where the tile leads: at the reason, not merely at the project. */
  view: "needs-you" | "ready" | null;
};

export const look: Record<Standing, Look> = {
  clear: { word: "Clear", icon: ThumbsUpIcon, icons: 2, tone: "standing-clear", view: null },
  running: { word: "Running", icon: ThumbsUpIcon, icons: 1, tone: "standing-running", view: null },
  // Not the flat hand the plan drew: the frame already spends that icon on
  // "Needs you", and two meanings on one glyph in one window is worse than a
  // second glyph. A pause is what idle is — nothing moving, nothing wrong.
  idle: { word: "Idle", icon: PauseIcon, icons: 1, tone: "standing-idle", view: "ready" },
  waiting: { word: "Waiting", icon: HourglassIcon, icons: 1, tone: "standing-waiting", view: "needs-you" },
  neglected: { word: "Neglected", icon: ThumbsDownIcon, icons: 2, tone: "standing-neglected", view: "needs-you" },
};

/**
 * The line under the word: what produced this step, in words, always with the
 * number that produced it. A tile that only said "Neglected" would send its
 * reader to the list to find out what for.
 */
export function reason(project: ProjectStanding, now: number): string {
  const { because, needs_you: needs, work } = project;
  const waited = needs.oldest === null || needs.oldest === undefined ? "" : `, oldest ${age(needs.oldest, now)}`;

  switch (because) {
    case "question":
      return `${count(needs.question, "question")}${waited}`;
    case "review":
      return `${needs.review} in review${waited}`;
    case "unready":
      return `${count(needs.unready, "issue")} to triage${waited}`;
    case "stuck":
      return `${count(needs.stuck, "issue")} stuck${waited}`;
    case "no_agent":
      return "no agent to pick anything up";
    case "blocked":
      return `nothing ready, ${work.open} behind blockers`;
    case "nothing_ready":
      return "nothing an agent could take";
    case "working":
      return work.in_progress > 0 ? `${work.in_progress} in progress` : `${count(work.ready, "issue")} ready`;
    case "nothing":
    default:
      return "Nothing open";
  }
}

/** The three counts, and nothing about them a colour: they are not a grade. */
export function counts(project: ProjectStanding): string | null {
  const { work } = project;
  return work.open === 0 ? null : `${work.in_progress} in progress · ${work.ready} ready · ${work.open} open`;
}

/**
 * How long something has been waiting, coarse on purpose: the line it stands in
 * is read at a glance, and the three-day step it hints at is a matter of days
 * rather than of minutes.
 */
export function age(when: string, now: number): string {
  const hours = Math.floor((now - new Date(when).getTime()) / 3_600_000);

  if (hours < 1) {
    return "under an hour";
  }

  return hours < 48 ? `${count(hours, "hour")}` : `${Math.floor(hours / 24)} days`;
}

function count(many: number, thing: string): string {
  return `${many} ${thing}${many === 1 ? "" : "s"}`;
}
