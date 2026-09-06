import { api } from "@/api/client";

/**
 * The one moment in the product where something is finished.
 *
 * A project reaching `clear` is the end of a stretch of work, and it is worth
 * a second of the screen. Two rules keep that from wearing out. It fires only
 * on a change somebody was there to see — never on a first load, which would
 * spend the gesture on arriving rather than on finishing — and it comes in two
 * sizes, so that the full one stays the one that means "done".
 *
 * It carries nothing anybody has to read: the tile beside it already says what
 * changed, so the canvas is `aria-hidden`, takes no pointer, and a reader who
 * asked for less motion is simply given none.
 */
export type Celebration =
  /** A project reached `clear`: nothing is open at all. */
  | { kind: "clear" }
  /** A project came out of waiting and is moving again — smaller, at its tile. */
  | { kind: "relieved"; at: Element };

/** Whether this browser has been asked for less motion. */
export function stillness(): boolean {
  return typeof window.matchMedia === "function" && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

/**
 * Fires the celebration, fetching the library on the way.
 *
 * The import is here and not at the top of a module the shell reaches: whoever
 * never brings a project to `clear` never loads it, and the first load has a
 * budget that CI holds (`docs/human-interface.md`). Nothing is awaited by a
 * caller — a celebration that failed to arrive is not an error anybody should
 * be told about.
 */
export async function celebrate(celebration: Celebration): Promise<void> {
  if (stillness()) {
    return;
  }

  try {
    const { default: confetti } = await import("canvas-confetti");

    if (celebration.kind === "relieved") {
      const box = celebration.at.getBoundingClientRect();

      await confetti({
        particleCount: 24,
        spread: 55,
        startVelocity: 22,
        ticks: 60,
        scalar: 0.7,
        disableForReducedMotion: true,
        origin: {
          x: (box.left + box.width / 2) / window.innerWidth,
          y: (box.top + box.height / 2) / window.innerHeight,
        },
      });

      return;
    }

    // Out of the two bottom corners, towards the middle: the shape of a
    // celebration everybody already knows, and it leaves the middle of the
    // screen — where the tiles are — clear while it crosses it.
    for (const from of [0, 1]) {
      void confetti({
        particleCount: 70,
        spread: 70,
        startVelocity: 45,
        ticks: 120,
        disableForReducedMotion: true,
        origin: { x: from, y: 1 },
        angle: from === 0 ? 60 : 120,
      });
    }
  } catch {
    // The library did not arrive. The screen has already said what changed.
  }
}

/**
 * The celebration where the human is standing, which is usually not the
 * overview: whoever closes the last open issue is on that issue's screen.
 *
 * It needs no memory of how the project stood before, and that is the point.
 * The caller only asks after an act that closed an issue which was open a
 * moment ago — so the project cannot have been `clear` then, and finding it
 * `clear` now is the change itself rather than a guess about one.
 */
export async function celebrateIfCleared(project: string): Promise<void> {
  if (stillness()) {
    return;
  }

  try {
    const { data } = await api.GET("/standing");

    if (data?.projects.find((standing) => standing.key === project)?.standing === "clear") {
      await celebrate({ kind: "clear" });
    }
  } catch {
    // Nothing is owed here: the act itself already answered on the screen.
  }
}
