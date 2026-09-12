import { createContext } from "react";
import type { Schemas } from "@/api/client";

export type Space = Schemas["Space"];
export type SpacePageSummary = Schemas["SpacePageSummary"];
export type SpacePage = Schemas["SpacePage"];

/** The spaces the caller may see, as the frame holds them. */
export type Spaces = { at: "asking" } | { at: "failed" } | { at: "known"; spaces: Space[] };

/**
 * The list the shell reads once and the way a screen that changed it asks for
 * it again — the same arrangement `projects/context.ts` describes, for the
 * other bracket. The shell is never remounted by navigation (ADR 0006), so a
 * screen that creates or renames a space has no other way to make the frame
 * agree with the address it is about to lead to.
 */
export type SpaceList = { spaces: Spaces; reload: () => Promise<void> };

export const SpacesContext = createContext<SpaceList | null>(null);

export type Tree = { at: "asking" } | { at: "failed"; why: string } | { at: "known"; pages: SpacePageSummary[] };

/**
 * The open space's page tree. It is the navigation of the area, so the frame
 * holds it and every screen reads it from here: the page above draws the path
 * from the root down out of these rows rather than asking once per level, and
 * a handgrip on the tree calls `reload` instead of writing a second copy of it
 * forward.
 */
export type PageTree = { space: string | undefined; tree: Tree; reload: () => Promise<void> };

export const TreeContext = createContext<PageTree | null>(null);
