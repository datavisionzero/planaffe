import {
  BookOpenIcon,
  CircleDotIcon,
  HandIcon,
  LayersIcon,
  ListIcon,
  PackageIcon,
  TagIcon,
  type LucideIcon,
} from "lucide-react";

/**
 * The views of a project, as ADR 0006 lists them, in the order the navigation
 * shows them. Every view is a route under `/:project/`, and the four issue
 * views carry their filter as the defaults the URL may add to — a pasted link
 * says what it shows.
 */
export type IssueFilter = {
  status?: string[];
  ready?: boolean;
  claimed?: string;
  has_open_question?: boolean;
};

export type View = {
  id: string;
  label: string;
  path: string;
  icon: LucideIcon;
  group: "views" | "structure";
  /** The list the view is a window on; absent for a view that is not a list of issues. */
  filter?: IssueFilter;
  /** What the view is for, in one sentence the empty state and the palette use. */
  hint: string;
};

export const views: View[] = [
  {
    id: "ready",
    label: "Ready for agents",
    path: "ready",
    icon: CircleDotIcon,
    group: "views",
    filter: { status: ["todo"], ready: true },
    hint: "What next would hand out, in that order.",
  },
  {
    id: "in-progress",
    label: "In progress",
    path: "in-progress",
    icon: LayersIcon,
    group: "views",
    filter: { status: ["in_progress"] },
    hint: "Who holds what, and since when.",
  },
  {
    id: "needs-you",
    label: "Needs you",
    path: "needs-you",
    icon: HandIcon,
    group: "views",
    filter: { status: ["review"] },
    hint: "What only a human can resolve: questions and reviews.",
  },
  {
    id: "all",
    label: "All issues",
    path: "issues",
    icon: ListIcon,
    group: "views",
    filter: {},
    hint: "Every issue of the project, filtered by the URL.",
  },
  {
    id: "epics",
    label: "Epics",
    path: "epics",
    icon: BookOpenIcon,
    group: "structure",
    hint: "What belongs together, with progress.",
  },
  {
    id: "releases",
    label: "Releases",
    path: "releases",
    icon: PackageIcon,
    group: "structure",
    hint: "What shipped together.",
  },
  {
    id: "labels",
    label: "Labels",
    path: "labels",
    icon: TagIcon,
    group: "structure",
    hint: "The project's labels and their groups.",
  },
];

export function viewPath(project: string, view: View): string {
  return `/${project}/${view.path}`;
}

/** `PLAN-42` and `PLAN-E4` — what a pasted key looks like, case aside. */
export const keyPattern = /^([A-Z][A-Z0-9]*)-(E?\d+)$/i;
