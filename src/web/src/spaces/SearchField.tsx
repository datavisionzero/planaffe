import { SearchIcon } from "lucide-react";
import { useEffect, useId, useRef, useState } from "react";
import { useLocation, useNavigate, useSearchParams } from "react-router";
import { Input } from "@/components/ui/input";
import { is, typing, overlaid } from "@/shell/shortcuts";
import { spacePath } from "@/shell/views";
import { useSpaceList } from "./useSpaces";

/** Typed words settle before the address is written, as in the palette. */
const settle = 200;

/**
 * The search of the knowledge base, in the frame rather than on a screen: it
 * stands over the page tree, which is where a reader is when they go looking
 * (VISION 18).
 *
 * What is typed goes into the address — `/spaces?q=…` across the whole
 * knowledge base, `/spaces/{name}?q=…` inside a space — and the address is
 * what the screen reads. That is the same arrangement the issue list has, and
 * it buys the same two things: a pasted link says what it shows, and going
 * back gives the search back.
 */
export function SearchField({ space }: { space: string | undefined }) {
  const { spaces } = useSpaceList();
  const [params] = useSearchParams();
  const location = useLocation();
  const navigate = useNavigate();
  const field = useRef<HTMLInputElement>(null);
  const id = useId();
  const asked = params.get("q") ?? "";

  // What is in the field, and which address it was typed against. When the
  // address changes under it — back, a walk to another space, a cleared
  // search — the address wins, and no effect is needed to say so.
  const [hand, setHand] = useState({ of: asked, text: asked });
  const text = hand.of === asked ? hand.text : asked;

  const where = space === undefined ? "/spaces" : spacePath(space);

  useEffect(() => {
    if (text === asked) {
      return;
    }

    const timer = setTimeout(() => {
      // The first word of a search is a step of its own; what follows corrects
      // it, so the way back is out of the search and not through every letter
      // of it.
      void navigate(text === "" ? where : `${where}?q=${encodeURIComponent(text)}`, { replace: asked !== "" });
    }, settle);

    return () => clearTimeout(timer);
  }, [asked, navigate, text, where]);

  // The key that jumps into the search on an issue list does it here too. It
  // is handled where the field is rather than in the shell, so that nothing
  // has to reach across the frame for a focus.
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (is("global:search", event) && !typing(event) && !overlaid(event)) {
        event.preventDefault();
        field.current?.focus();
      }
    }

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  // A knowledge base with no space in it has nothing to search, and a field
  // over an empty navigation is an offer that cannot be taken up. The screen
  // says what a space is instead.
  if (spaces.at === "known" && spaces.spaces.length === 0) {
    return null;
  }

  return (
    <div className="relative px-2 py-1">
      <SearchIcon className="pointer-events-none absolute top-1/2 left-4 size-3.5 -translate-y-1/2 text-muted-foreground" aria-hidden />
      <Input
        ref={field}
        id={id}
        name="q"
        type="search"
        value={text}
        aria-label={space === undefined ? "Search the knowledge base" : `Search ${space}`}
        placeholder={space === undefined ? "Search the knowledge base" : "Search this space"}
        className="h-8 pl-7 text-sm"
        onChange={(event) => setHand({ of: asked, text: event.target.value })}
        onKeyDown={(event) => {
          if (event.key === "Escape") {
            event.preventDefault();
            setHand({ of: asked, text: "" });
            field.current?.blur();
          }
        }}
      />
      {/* A search inside a space says so under the field rather than in the
          placeholder, because the placeholder is gone as soon as it matters. */}
      {space !== undefined && text !== "" && location.pathname.startsWith(spacePath(space)) && (
        <p className="px-1 pt-1 text-xs text-muted-foreground">In this space.</p>
      )}
    </div>
  );
}
