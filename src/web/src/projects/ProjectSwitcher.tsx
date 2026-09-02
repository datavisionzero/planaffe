import { CheckIcon, ChevronsUpDownIcon } from "lucide-react";
import { useNavigate } from "react-router";
import type { Project } from "@/api/client";
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
import { rememberProject } from "./useProjects";

/**
 * The project switcher of the header (ADR 0006). Switching keeps the view:
 * whoever is in "In progress" of PLAN lands in "In progress" of the next
 * project, because the question was about the view, not about the project.
 */
export function ProjectSwitcher({
  projects,
  current,
  viewPath,
}: {
  projects: Project[];
  current: Project | undefined;
  viewPath: string;
}) {
  const navigate = useNavigate();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button variant="ghost" size="sm" className="gap-1.5 px-2 font-medium" aria-label="Switch project" />
        }
      >
        <span className="font-mono text-xs font-medium tracking-wide text-brand">
          {current?.key ?? "—"}
        </span>
        <span className="hidden sm:inline">{current?.name ?? "No project"}</span>
        <ChevronsUpDownIcon className="size-3.5 text-muted-foreground" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="min-w-56">
        <DropdownMenuGroup>
          <DropdownMenuLabel className="flex items-center justify-between">
            Projects
            <Kbd>⌘P</Kbd>
          </DropdownMenuLabel>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        {projects.map((project) => (
          <DropdownMenuItem
            key={project.key}
            onClick={() => {
              rememberProject(project.key);
              void navigate(`/${project.key}/${viewPath}`);
            }}
          >
            <span className="w-14 font-mono text-xs text-muted-foreground">{project.key}</span>
            <span className="flex-1 truncate">{project.name}</span>
            {project.key === current?.key && <CheckIcon className="size-3.5" />}
          </DropdownMenuItem>
        ))}
        {projects.length === 0 && (
          <div className="px-2 py-1.5 text-xs text-muted-foreground">
            No project yet. <code className="font-mono">pa project create</code> makes one.
          </div>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
