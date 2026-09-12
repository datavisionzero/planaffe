import { BotOffIcon, CheckIcon, ChevronsUpDownIcon, LibraryIcon } from "lucide-react";
import { useNavigate } from "react-router";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Kbd } from "@/components/ui/kbd";
import { drawn } from "@/shell/shortcuts";
import { spacePath } from "@/shell/views";
import type { Space, Spaces } from "./context";

/**
 * The space switcher, in the place the project switcher stands in and never
 * beside it: the header carries the bracket of the area the reader is in, and
 * the knowledge base has no project (VISION 18). It is the second half of the
 * border — whoever sees a space name here knows they are not in the tracker.
 *
 * It is handed the list as it stands rather than the spaces in it, for the
 * reason the project switcher gives: a list that could not be loaded is not a
 * list of none.
 */
export function SpaceSwitcher({
  spaces,
  current,
  open,
  onOpenChange,
  reload,
}: {
  spaces: Spaces;
  current: Space | undefined;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  reload: () => Promise<void>;
}) {
  const navigate = useNavigate();
  const known = spaces.at === "known" ? spaces.spaces : [];

  return (
    <DropdownMenu open={open} onOpenChange={onOpenChange}>
      <DropdownMenuTrigger
        render={<Button variant="ghost" size="sm" className="gap-1.5 px-2 font-medium" aria-label="Switch space" />}
      >
        <LibraryIcon className="size-3.5 text-brand" />
        <span className="hidden sm:inline">{current?.title ?? standing[spaces.at]}</span>
        <span className="sm:hidden">{current?.name ?? "—"}</span>
        <ChevronsUpDownIcon className="size-3.5 text-muted-foreground" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="min-w-56">
        <DropdownMenuGroup>
          <DropdownMenuLabel className="flex items-center justify-between">
            Spaces
            <Kbd>{drawn("global:spaces").join("")}</Kbd>
          </DropdownMenuLabel>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={() => void navigate("/spaces")}>
          <LibraryIcon className="size-3.5" />
          <span className="flex-1">All spaces</span>
          {current === undefined && <CheckIcon className="size-3.5" />}
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        {known.map((space) => (
          <DropdownMenuItem key={space.name} onClick={() => void navigate(spacePath(space.name))}>
            <span className="flex-1 truncate">{space.title}</span>
            {/* The switch is a property of the space and belongs where the
                space is named, not only in its settings. */}
            {space.closed_to_agents && <BotOffIcon className="size-3.5 text-muted-foreground" aria-label="Closed to agents" />}
            {space.name === current?.name && <CheckIcon className="size-3.5" />}
          </DropdownMenuItem>
        ))}
        {spaces.at === "asking" && (
          <div role="status" className="px-2 py-1.5 text-xs text-muted-foreground">Loading the spaces…</div>
        )}
        {spaces.at === "failed" && (
          <div className="space-y-1 px-2 py-1.5 text-xs">
            <p className="text-destructive">The spaces could not be loaded.</p>
            <button type="button" className="text-brand underline-offset-4 hover:underline" onClick={() => void reload()}>
              Try again
            </button>
          </div>
        )}
        {spaces.at === "known" && known.length === 0 && (
          <div className="px-2 py-1.5 text-xs text-muted-foreground">No space yet.</div>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

/** What the trigger says when there is no space to name. */
const standing: Record<Spaces["at"], string> = {
  asking: "Loading…",
  failed: "Spaces unavailable",
  known: "Knowledge base",
};
