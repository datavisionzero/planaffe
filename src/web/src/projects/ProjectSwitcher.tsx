import { CheckIcon, ChevronsUpDownIcon, FolderIcon, LayoutGridIcon, PlusIcon, SearchIcon } from "lucide-react";
import { useId, useRef, useState } from "react";
import { useNavigate } from "react-router";
import type { Project } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Kbd } from "@/components/ui/kbd";
import { cn } from "@/lib/utils";
import type { Projects } from "./useProjects";
import { useProjectGroups } from "./useProjectGroups";

type Props = { projects: Projects; current: Project | undefined; viewPath: string; open: boolean; onOpenChange: (open: boolean) => void; reload: () => Promise<void> };
export function ProjectSwitcher(props: Props) {
  return <Dialog open={props.open} onOpenChange={props.onOpenChange}>
    <DialogTrigger render={<Button variant="ghost" size="sm" className="min-w-0 max-w-64 gap-2" aria-label="Switch project" />}>
      <span className="truncate font-mono text-xs text-brand">{props.current?.key ?? "Projects"}</span>
      <span className="hidden truncate sm:block">{props.current?.name ?? "Choose a project"}</span>
      <ChevronsUpDownIcon className="size-3.5 shrink-0 text-muted-foreground" />
    </DialogTrigger>
    <DialogContent className="flex max-h-[90dvh] flex-col gap-0 overflow-x-hidden overflow-y-auto p-0 sm:max-w-3xl">
      <div className="shrink-0 border-b px-6 py-5"><DialogTitle className="text-lg">Your projects</DialogTitle><DialogDescription>Find your project. Switching keeps your current view.</DialogDescription></div>
      {props.open && <Chooser {...props} />}
    </DialogContent>
  </Dialog>;
}
function Chooser({ projects, current, viewPath, onOpenChange, reload }: Props) {
  const navigate = useNavigate();
  const { groups, failed, reload: reloadGroups } = useProjectGroups();
  const [query, setQuery] = useState("");
  const [group, setGroup] = useState("all");
  const [active, setActive] = useState(0);
  const list = useRef<HTMLDivElement>(null);
  const id = useId();
  const known = projects.at === "known" ? projects.projects : [];
  const name = (id: string | null | undefined) => groups.find(g => g.id === id)?.name ?? "Ungrouped";
  const terms = query.toLocaleLowerCase().trim().split(/\s+/);
  const found = known.filter(p => (group === "all" || (p.group_id ?? "ungrouped") === group) && terms.every(t => `${p.key} ${p.name} ${name(p.group_id)}`.toLocaleLowerCase().includes(t)))
    .sort((a, b) => name(a.group_id).localeCompare(name(b.group_id)) || a.name.localeCompare(b.name));
  const index = Math.min(active, Math.max(0, found.length - 1));
  function go(path: string) { onOpenChange(false); void navigate(path); }
  function move(next: number) { setActive(next); list.current?.querySelectorAll('[role="option"]')[next]?.scrollIntoView?.({ block: "nearest" }); }
  const choices = [{ id: "all", name: "All projects", count: known.length }, ...groups.map(g => ({ ...g, count: known.filter(p => p.group_id === g.id).length })), { id: "ungrouped", name: "Ungrouped", count: known.filter(p => !p.group_id).length }];
  return <>
    <div className="flex min-w-0 shrink-0 items-center gap-3 border-b px-6 py-3">
      <SearchIcon className="size-5 shrink-0 text-muted-foreground" />
      <input name="project-search" autoComplete="off" data-protonpass-ignore="true" autoFocus role="combobox" aria-label="Search projects" aria-expanded aria-controls={`${id}-list`} aria-activedescendant={found.length ? `${id}-${index}` : undefined}
        placeholder="Search projects, keys or groups…" value={query} className="h-10 min-w-0 flex-1 bg-transparent outline-none placeholder:text-muted-foreground"
        onChange={e => { setQuery(e.target.value); setActive(0); }} onKeyDown={e => {
          if (e.key === "ArrowDown" || e.key === "ArrowUp") { e.preventDefault(); if (found.length) move((index + (e.key === "ArrowDown" ? 1 : found.length - 1)) % found.length); }
          if (e.key === "Enter" && found[index]) { e.preventDefault(); go(`/${found[index].key}/${viewPath}`); }
        }} />
      {query && <Button variant="ghost" size="sm" onClick={() => { setQuery(""); setActive(0); }}>Clear</Button>}
    </div>
    <div className="flex min-h-80 min-w-0 flex-col sm:flex-row">
      <nav aria-label="Project groups" className="flex gap-1 overflow-auto border-b bg-muted/30 p-3 sm:w-52 sm:shrink-0 sm:flex-col sm:border-r sm:border-b-0">
        {choices.filter(g => g.id === "all" || g.count > 0).map(g => <button key={g.id} type="button" aria-pressed={group === g.id} onClick={() => { setGroup(g.id); setActive(0); }}
          className={cn("flex shrink-0 items-center gap-2 rounded-lg px-3 py-2.5 text-left text-sm hover:bg-accent", group === g.id && "bg-accent font-medium text-accent-foreground")}>
          <FolderIcon className="size-4 shrink-0 text-muted-foreground" /><span className="min-w-0 flex-1 truncate">{g.name}</span><span className="text-xs tabular-nums text-muted-foreground">{g.count}</span>
        </button>)}
      </nav>
      <div className="min-w-0 flex-1">
        <div className="flex justify-between px-5 py-3 text-xs text-muted-foreground"><span>{query ? "Search results" : choices.find(g => g.id === group)?.name}</span><span role="status">{found.length} projects</span></div>
        {failed && <p className="px-5 text-xs text-destructive">Groups could not be loaded. <button className="underline" onClick={() => void reloadGroups()}>Retry groups</button></p>}
        <div ref={list} id={`${id}-list`} role="listbox" aria-label="Projects" className="max-h-80 overflow-y-auto px-3 pb-3">
          {found.map((p, i) => <div key={p.key}>
            {(i === 0 || name(found[i - 1].group_id) !== name(p.group_id)) && <div className="px-2 pt-3 pb-1 text-xs font-medium text-muted-foreground">{name(p.group_id)}</div>}
            <button id={`${id}-${i}`} role="option" aria-selected={p.key === current?.key} type="button" onFocus={() => setActive(i)} onMouseMove={() => setActive(i)} onClick={() => go(`/${p.key}/${viewPath}`)}
              className={cn("flex w-full items-center gap-3 rounded-lg p-3 text-left outline-none transition-colors focus-visible:ring-2 focus-visible:ring-ring", i === index ? "bg-accent" : "hover:bg-accent/60")}>
              <span className="flex size-10 shrink-0 items-center justify-center rounded-lg border bg-background font-mono text-xs font-semibold text-brand">{p.key.slice(0, 2)}</span>
              <span className="min-w-0 flex-1"><span className="block truncate font-medium">{p.name}</span><span className="block font-mono text-xs text-muted-foreground">{p.key}</span></span>
              {p.key === current?.key && <span className="flex items-center gap-1 text-xs text-brand"><CheckIcon className="size-3.5" />Current</span>}
            </button>
          </div>)}
          {projects.at === "asking" && <p role="status" className="p-6 text-muted-foreground">Loading projects…</p>}
          {projects.at === "failed" && <div className="p-6"><p>The projects could not be loaded.</p><Button variant="outline" onClick={() => void reload()}>Try again</Button></div>}
          {projects.at === "known" && found.length === 0 && <div className="space-y-2 px-4 py-10 text-center"><SearchIcon className="mx-auto size-7 text-muted-foreground" /><p className="font-medium">{known.length ? "No matching projects" : "Your first project starts here"}</p><p className="text-sm text-muted-foreground">{known.length ? "Try another name or key, or choose a different group." : "Create a project to give your work a home."}</p></div>}
        </div>
      </div>
    </div>
    <div className="flex shrink-0 flex-wrap items-center justify-between gap-2 border-t bg-muted/20 px-4 py-3">
      <Button variant="ghost" size="sm" onClick={() => go("/projects")}><LayoutGridIcon />Overview</Button>
      <span className="hidden items-center gap-1 text-xs text-muted-foreground sm:flex"><Kbd>↑</Kbd><Kbd>↓</Kbd> navigate <Kbd>Enter</Kbd> open</span>
      <Button variant="outline" size="sm" onClick={() => go("/projects/new")}><PlusIcon />Create project</Button>
    </div>
  </>;
}
