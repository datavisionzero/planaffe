import { ArrowRightIcon, SearchIcon } from "lucide-react";
import { useMemo, useState, type KeyboardEvent } from "react";
import { useNavigate } from "react-router";
import type { Project } from "@/api/client";
import { useTheme } from "@/components/theme-provider";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Kbd } from "@/components/ui/kbd";
import { rememberProject } from "@/projects/useProjects";
import { useSession } from "@/session/useSession";
import { cn } from "@/lib/utils";
import { keyPattern, viewPath, views } from "./views";

type Command = {
  id: string;
  label: string;
  hint?: string;
  group: string;
  run: () => void;
};

/**
 * The command palette — ⌘K, or Ctrl+K — over the views, the projects and the
 * few acts the shell itself has. A key typed into it opens that issue or epic,
 * which is the fastest way from a chat to a ticket.
 *
 * Owned rather than imported (ADR 0017): a filtered list with a roving index
 * inside a Base UI dialog, which is what a palette is before it does more.
 */
type PaletteProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  projects: Project[];
  current: Project | undefined;
};

export function Palette({ open, onOpenChange, projects, current }: PaletteProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="top-[20%] translate-y-0 gap-0 overflow-hidden p-0 sm:max-w-lg" showCloseButton={false}>
        <DialogHeader className="sr-only">
          <DialogTitle>Command palette</DialogTitle>
          <DialogDescription>Search views, projects and commands, or type an issue key.</DialogDescription>
        </DialogHeader>
        {open && <PaletteBody onOpenChange={onOpenChange} projects={projects} current={current} />}
      </DialogContent>
    </Dialog>
  );
}

/** Mounted while the palette is open, so that its query starts empty every time. */
function PaletteBody({ onOpenChange, projects, current }: Omit<PaletteProps, "open">) {
  const navigate = useNavigate();
  const { setTheme } = useTheme();
  const { signOut } = useSession();
  const [query, setQuery] = useState("");
  const [index, setIndex] = useState(0);

  const commands = useMemo<Command[]>(() => {
    const go = (to: string) => () => {
      onOpenChange(false);
      void navigate(to);
    };

    const list: Command[] = [];
    const key = query.trim().match(keyPattern);

    if (key !== null) {
      const project = key[1]!.toUpperCase();
      const number = key[2]!.toUpperCase();
      const isEpic = number.startsWith("E");

      list.push({
        id: `open:${project}-${number}`,
        label: `Open ${project}-${number}`,
        hint: isEpic ? "epic" : "issue",
        group: "Go to",
        run: go(isEpic ? `/${project}/epics/${project}-${number}` : `/${project}/issues/${project}-${number}`),
      });
    }

    if (current !== undefined) {
      for (const view of views) {
        list.push({
          id: `view:${view.id}`,
          label: view.label,
          hint: view.hint,
          group: current.key,
          run: go(viewPath(current.key, view)),
        });
      }
    }

    for (const project of projects) {
      if (project.key !== current?.key) {
        list.push({
          id: `project:${project.key}`,
          label: project.name,
          hint: project.key,
          group: "Switch project",
          run: () => {
            rememberProject(project.key);
            go(`/${project.key}/ready`)();
          },
        });
      }
    }

    list.push(
      { id: "theme:light", label: "Light theme", group: "Appearance", run: () => { onOpenChange(false); setTheme("light"); } },
      { id: "theme:dark", label: "Dark theme", group: "Appearance", run: () => { onOpenChange(false); setTheme("dark"); } },
      { id: "theme:system", label: "Follow the system", group: "Appearance", run: () => { onOpenChange(false); setTheme("system"); } },
      { id: "settings", label: "Settings", group: "Account", run: go("/settings") },
      { id: "sign-out", label: "Sign out", group: "Account", run: () => { onOpenChange(false); signOut(); } },
    );

    return list;
  }, [current, navigate, onOpenChange, projects, query, setTheme, signOut]);

  const matching = useMemo(() => {
    const needle = query.trim().toLowerCase();

    if (needle === "" || keyPattern.test(needle)) {
      return commands;
    }

    return commands.filter(
      (command) =>
        command.label.toLowerCase().includes(needle) ||
        command.hint?.toLowerCase().includes(needle) ||
        command.group.toLowerCase().includes(needle),
    );
  }, [commands, query]);

  const selected = matching[Math.min(index, Math.max(matching.length - 1, 0))];

  function onKeyDown(event: KeyboardEvent) {
    if (event.key === "ArrowDown") {
      event.preventDefault();
      setIndex((current) => Math.min(current + 1, matching.length - 1));
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      setIndex((current) => Math.max(current - 1, 0));
    } else if (event.key === "Enter" && selected !== undefined) {
      event.preventDefault();
      selected.run();
    }
  }

  let lastGroup: string | undefined;

  return (
    <>
        <div className="flex items-center gap-2 border-b px-3">
          <SearchIcon className="size-4 text-muted-foreground" />
          <input
            autoFocus
            role="combobox"
            aria-expanded
            aria-controls="palette-commands"
            aria-activedescendant={selected ? `palette-${selected.id}` : undefined}
            aria-label="Search issues, or type a command"
            placeholder="Search issues, or type a command"
            value={query}
            onChange={(event) => {
              setQuery(event.target.value);
              setIndex(0);
            }}
            onKeyDown={onKeyDown}
            className="h-11 flex-1 bg-transparent text-sm outline-hidden placeholder:text-muted-foreground"
          />
          <Kbd>esc</Kbd>
        </div>

        <ul id="palette-commands" role="listbox" className="max-h-80 overflow-y-auto p-1">
          {matching.length === 0 && (
            <li className="px-3 py-6 text-center text-sm text-muted-foreground">Nothing matches.</li>
          )}
          {matching.map((command) => {
            const heading = command.group !== lastGroup ? command.group : undefined;
            lastGroup = command.group;

            return (
              <li key={command.id} role="presentation">
                {heading !== undefined && (
                  <div className="px-2 pt-2 pb-1 text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
                    {heading}
                  </div>
                )}
                <div
                  id={`palette-${command.id}`}
                  role="option"
                  aria-selected={command === selected}
                  onMouseMove={() => setIndex(matching.indexOf(command))}
                  onClick={command.run}
                  className={cn(
                    "flex cursor-default items-center gap-3 rounded-md px-2 py-1.5 text-sm",
                    command === selected && "bg-accent text-accent-foreground",
                  )}
                >
                  <span className="flex-1 truncate">{command.label}</span>
                  {command.hint !== undefined && (
                    <span className="truncate text-xs text-muted-foreground">{command.hint}</span>
                  )}
                  {command === selected && <ArrowRightIcon className="size-3.5 text-muted-foreground" />}
                </div>
              </li>
            );
          })}
        </ul>
    </>
  );
}
