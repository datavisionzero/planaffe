import { MoreHorizontalIcon } from "lucide-react";
import { useId, type ReactNode } from "react";
import { Navigate, NavLink, Route, Routes, useLocation, useParams } from "react-router";
import { Button } from "@/components/ui/button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";
import { PageHeader } from "@/shared/PageHeader";

/**
 * The address of the screen itself.
 *
 * Every one of these screens is the element of a splat route, and a relative
 * link inside a splat route resolves against everything the splat matched:
 * from `/settings/security` a `to="tokens"` entry led to
 * `/settings/security/tokens`, which is no area at all. One click was enough
 * to make every other area unreachable, and the address grew a segment with
 * each further one. The splat is exactly what the screen is not, so taking it
 * off the current address leaves the screen.
 */
function useScreen() {
  const here = useLocation().pathname.replace(/\/$/, "").split("/");
  const inside = useParams()["*"] ?? "";
  return here.slice(0, here.length - (inside === "" ? 0 : inside.split("/").length)).join("/");
}

/** One area of an administration screen: a nav entry and the route behind it. */
export type Area = {
  /** Where the nav entry leads, relative to the screen. */
  to: string;
  /** The route pattern, where it is wider than the entry — `projects/*`. */
  path?: string;
  label: string;
  element: ReactNode;
};

/**
 * The three administration screens, drawn the same way.
 *
 * Each of them used to be one long page of sections with no navigation inside
 * it: `/admin` was one address for three subjects, nobody could be sent to the
 * user administration, and a reload always landed at the top. What the roadmap
 * adds — project-wide agent instructions, notifications, a forge connection —
 * had nowhere to go but another box at the bottom.
 *
 * So an area is an address. Adding one costs an entry in this list and a
 * route, and nothing else moves.
 */
export function SettingsShell({ title, areas }: { title: string; areas: Area[] }) {
  const screen = useScreen();

  return (
    <>
      <PageHeader title={title} />
      <div className="mx-auto w-full max-w-5xl p-4 sm:p-6 md:grid md:grid-cols-[11rem_1fr] md:gap-8">
        {/* Wide: the list stands beside what it opens. Narrow: it folds above
            the area rather than squeezing a second column in. */}
        <nav aria-label={`${title} areas`} className="mb-4 md:mb-0">
          <ul className="-mx-1 flex gap-1 overflow-x-auto px-1 pb-1 md:flex-col md:overflow-visible md:pb-0">
            {areas.map((area) => (
              <li key={area.to}>
                <NavLink
                  to={`${screen}/${area.to}`}
                  className={({ isActive }) =>
                    cn(
                      "block shrink-0 rounded-md px-3 py-1.5 text-sm whitespace-nowrap hover:bg-accent",
                      isActive && "bg-accent font-medium",
                    )
                  }
                >
                  {area.label}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
        <div className="min-w-0 space-y-8">
          <Routes>
            {/* The address the screen had before it had areas still works, and
                lands on the first one. */}
            <Route index element={<Navigate to={`${screen}/${areas[0].to}`} replace />} />
            {areas.map((area) => (
              <Route key={area.to} path={area.path ?? area.to} element={area.element} />
            ))}
          </Routes>
        </div>
      </div>
    </>
  );
}

export function Section({ title, description, children }: { title: string; description?: string; children: ReactNode }) {
  const id = `section-${title.replaceAll(" ", "-")}`;
  return (
    <section aria-labelledby={id}>
      <h2 id={id} className="font-medium">{title}</h2>
      {description && <p className="mb-3 text-sm text-muted-foreground">{description}</p>}
      <div className="rounded-md border p-3">{children}</div>
    </section>
  );
}

/**
 * The head a list of any length carries: a search, whatever narrows it, and
 * what is left.
 *
 * Both lists of the instance administration said everything at once and could
 * not be asked anything. They are the same problem twice — ten projects of
 * which three are deleted, every user of every state in one run — so this is
 * the same answer once, and each list passes the one control that is its own.
 *
 * The count is a `status` and not a heading: it changes while somebody types,
 * and what changes under a reader who cannot see it has to be spoken.
 */
export function ListHead({ label, placeholder, search, onSearch, said, children }: {
  label: string;
  placeholder: string;
  search: string;
  onSearch: (search: string) => void;
  /** The result count, where something is narrowing the list. */
  said?: string;
  /** The filter and the sort, which are each list's own. */
  children: ReactNode;
}) {
  const id = useId();

  return (
    <div className="mb-3 grid gap-2 sm:grid-cols-[2fr_1fr_1fr]">
      <div className="grid gap-1 text-sm font-medium">
        <label htmlFor={id}>{label}</label>
        <Input id={id} type="search" value={search} placeholder={placeholder} onChange={(event) => onSearch(event.target.value)} className="font-normal" />
      </div>
      {children}
      {said !== undefined && <p role="status" className="text-xs text-muted-foreground sm:col-span-3">{said}</p>}
    </div>
  );
}

export function Rows({ children, empty }: { children: ReactNode[]; empty: string }) {
  return <div className="divide-y rounded-md border">{children.length ? children : <p className="p-3 text-sm text-muted-foreground">{empty}</p>}</div>;
}

export function Row({ title, detail, action }: { title: ReactNode; detail?: ReactNode; action?: ReactNode }) {
  return (
    <div className="flex min-h-12 items-center gap-3 p-3">
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium">{title}</p>
        {detail && <p className="text-xs text-muted-foreground">{detail}</p>}
      </div>
      {action}
    </div>
  );
}

/**
 * The acts of one row. Three buttons side by side do not carry on a phone,
 * and the row is where they belong rather than beside the row's own text.
 */
export function RowMenu({ label, children }: { label: string; children: ReactNode }) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger render={<Button variant="ghost" size="sm" aria-label={label} />}>
        <MoreHorizontalIcon />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">{children}</DropdownMenuContent>
    </DropdownMenu>
  );
}

/**
 * What the instance answered, beside the act that asked. It used to stand at
 * the foot of the whole page: one clicked at the top and read at the bottom.
 */
export function Said({ notice }: { notice: string }) {
  return notice === "" ? null : <p role="status" className="mt-3 text-sm text-muted-foreground">{notice}</p>;
}

export function Secret({ value }: { value: string }) {
  return (
    <div role="status" className="mb-3 rounded-md border border-brand/40 bg-brand/5 p-3">
      <p className="text-xs text-muted-foreground">Copy this secret now. It will not be shown again.</p>
      <code className="break-all text-xs">{value}</code>
    </div>
  );
}
