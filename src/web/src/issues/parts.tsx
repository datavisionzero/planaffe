import { date } from "./acts";

/** The small pieces the issue screen and its parts all draw with. */
export function Field({ name, className, children }: { name: string; className?: string; children: React.ReactNode }) { return <div className={className}><div className="text-[11px] font-medium tracking-wide text-muted-foreground uppercase">{name}</div><div className="mt-0.5">{children}</div></div>; }
export function Eyebrow({ children }: { children: React.ReactNode }) { return <h2 className="text-xs font-semibold tracking-wide uppercase">{children}</h2>; }
export function Byline({ name, at, edited }: { name?: string; at: string; edited?: string | null }) { return <p className="mt-1 text-xs text-muted-foreground">{name && <>{name} · </>}<time dateTime={at}>{date(at)}</time>{edited != null && <> · <span title={date(edited)}>edited</span></>}</p>; }
