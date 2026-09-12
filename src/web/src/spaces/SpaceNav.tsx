import { ChevronRightIcon } from "lucide-react";
import { useState } from "react";
import { NavLink, useLocation } from "react-router";
import {
  SidebarMenu,
  SidebarMenuAction,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarMenuSub,
  SidebarMenuSubButton,
  SidebarMenuSubItem,
  useSidebar,
} from "@/components/ui/sidebar";
import { spacePagePath } from "@/shell/views";
import type { SpacePageSummary, Tree } from "./context";
import { ancestorsOf, childrenOf } from "./tree";

/**
 * The page tree of a space, in the place the project's views stand in: in the
 * knowledge base the tree *is* the navigation (VISION 18), so it is drawn by
 * the frame and not by a screen, and on a phone it is the same drawer.
 *
 * The rows arrive parents before children and siblings by title
 * (`docs/api.md`), so nothing is sorted here. What is decided here is only
 * which branches stand open: the one the open page is in, and whatever a
 * reader opened by hand.
 */
export function SpaceNav({ space, tree, path }: { space: string; tree: Tree; path: string | undefined }) {
  const { setOpenMobile } = useSidebar();
  const { pathname } = useLocation();
  // What a reader folded or unfolded by hand, and the page it was done under.
  // The branch the open page stands in is open by itself, so the hand is only
  // ever the exception to that — and it belongs to the page it was made on:
  // walking somewhere else and not seeing where you landed is the one thing a
  // tree must not do.
  const [hand, setHand] = useState<{ at: string | undefined; folds: Record<string, boolean> }>({ at: path, folds: {} });
  const folds = hand.at === path ? hand.folds : {};
  const trail = path === undefined ? [] : ancestorsOf(path);
  const isOpen = (address: string) => folds[address] ?? trail.includes(address);

  if (tree.at === "asking") {
    return <p role="status" className="px-2 py-1.5 text-xs text-muted-foreground">Loading the pages…</p>;
  }

  if (tree.at === "failed") {
    return <p className="px-2 py-1.5 text-xs text-destructive">{tree.why}</p>;
  }

  if (tree.pages.length === 0) {
    return <p className="px-2 py-1.5 text-xs text-muted-foreground">No page in this space yet.</p>;
  }

  const toggle = (address: string) =>
    setHand({ at: path, folds: { ...folds, [address]: !isOpen(address) } });

  return (
    <SidebarMenu>
      {childrenOf(tree.pages, null).map((page) => (
        <Branch
          key={page.path}
          page={page}
          pages={tree.pages}
          space={space}
          pathname={pathname}
          isOpen={isOpen}
          onToggle={toggle}
          onWalk={() => setOpenMobile(false)}
        />
      ))}
    </SidebarMenu>
  );
}

type BranchProps = {
  page: SpacePageSummary;
  pages: SpacePageSummary[];
  space: string;
  pathname: string;
  isOpen: (path: string) => boolean;
  onToggle: (path: string) => void;
  onWalk: () => void;
};

/**
 * One page and, where it is open, the pages under it. It draws itself at the
 * top level as a menu item and below that as a sub item, which is what the
 * three levels look like in this sidebar; deeper than that does not exist
 * (ADR 0028).
 */
function Branch({ page, pages, space, pathname, isOpen, onToggle, onWalk }: BranchProps) {
  const children = childrenOf(pages, page.path);
  const to = spacePagePath(space, page.path);
  const active = pathname === to;
  const open = isOpen(page.path);
  const link = <NavLink to={to} onClick={onWalk} />;

  // The two shapes are written out rather than picked into a variable: they
  // are two components with two sets of props, and a union of them is a type
  // nobody can call.
  const fold = children.length > 0 && (
    <SidebarMenuAction
      aria-label={`${open ? "Fold" : "Unfold"} ${page.title}`}
      aria-expanded={open}
      className={open ? "rotate-90" : undefined}
      onClick={() => onToggle(page.path)}
    >
      <ChevronRightIcon />
    </SidebarMenuAction>
  );

  const below = open && children.length > 0 && (
    <SidebarMenuSub>
      {children.map((child) => (
        <Branch
          key={child.path}
          page={child}
          pages={pages}
          space={space}
          pathname={pathname}
          isOpen={isOpen}
          onToggle={onToggle}
          onWalk={onWalk}
        />
      ))}
    </SidebarMenuSub>
  );

  if (page.depth === 0) {
    return (
      <SidebarMenuItem>
        <SidebarMenuButton isActive={active} render={link}>
          <span>{page.title}</span>
        </SidebarMenuButton>
        {fold}
        {below}
      </SidebarMenuItem>
    );
  }

  return (
    <SidebarMenuSubItem>
      <SidebarMenuSubButton isActive={active} render={link}>
        <span>{page.title}</span>
      </SidebarMenuSubButton>
      {fold}
      {below}
    </SidebarMenuSubItem>
  );
}
