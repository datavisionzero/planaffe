import { XIcon } from "lucide-react";
import { useId, useMemo, useState } from "react";
import { cn } from "@/lib/utils";

/** As much of a label as choosing one needs. */
export type PickableLabel = { name: string; group?: string | null; description?: string | null };

type Option =
  | { at: "label"; label: PickableLabel; replaces?: string }
  | { at: "create"; name: string };

/**
 * Labels are chosen, not typed.
 *
 * A label has to exist before an issue may carry it, so a free-text field asked
 * for a word out of a dictionary it would not open: what there was became
 * visible only in the refusal that came back from saving. This is the one
 * control that shows the set — grouped, with the description each label carries
 * for exactly this moment — and the one place the group's exclusion is answered
 * before the instance has to refuse it.
 *
 * Built out of Tailwind and plain elements the repository owns (ADR 0017); the
 * combobox roles are what a reader with a screen reader is told, since none of
 * the three call sites can afford a listbox that only looks like one.
 */
export function LabelPicker({
  label,
  hint,
  labels,
  value,
  onChange,
  onCreate,
  className,
}: {
  /** The field's own label, drawn above the control. */
  label: string;
  hint?: string;
  /** What the project has to choose from. */
  labels: PickableLabel[];
  /** The chosen names, in the order they were chosen. */
  value: string[];
  onChange: (names: string[]) => void;
  /**
   * Create a label that does not exist yet and hand it back. Omitted where
   * creating from here makes no sense — the list filter chooses among what is
   * there.
   */
  onCreate?: (name: string) => Promise<PickableLabel>;
  className?: string;
}) {
  const id = useId();
  const listId = `${id}-list`;
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const [busy, setBusy] = useState(false);
  const [why, setWhy] = useState<string>();

  const known = useMemo(() => new Map(labels.map((label) => [label.name, label] as const)), [labels]);
  const groupOf = (name: string) => known.get(name)?.group ?? "";
  const chosen = useMemo(() => value.map((name) => known.get(name) ?? { name }), [known, value]);

  const options = useMemo<Option[]>(() => {
    const taken = new Set(value);
    const needle = query.trim().toLowerCase();
    const groups = new Map(value.map((name) => [known.get(name)?.group ?? "", name] as const));
    groups.delete("");

    const matches: Option[] = labels
      .filter((label) => !taken.has(label.name))
      .filter(
        (label) =>
          needle === "" ||
          label.name.toLowerCase().includes(needle) ||
          (label.description ?? "").toLowerCase().includes(needle),
      )
      .sort(byGroupThenName)
      .map((label) => ({ at: "label", label, replaces: groups.get(label.group ?? "") }));

    const named = query.trim();
    const exists = labels.some((label) => label.name.toLowerCase() === named.toLowerCase());

    return onCreate !== undefined && named !== "" && !exists
      ? [...matches, { at: "create", name: named }]
      : matches;
  }, [known, labels, onCreate, query, value]);

  // A list that shrank under the cursor leaves it on whatever is now last.
  const cursor = options.length === 0 ? 0 : Math.min(active, options.length - 1);

  /**
   * A group admits one label at a time, and the instance refuses the second
   * with a list of the issues in the way. Choosing a sibling says so and
   * replaces it here, where the writer can still see what happened.
   */
  function add(label: PickableLabel) {
    const group = label.group ?? "";
    const kept = group === "" ? value : value.filter((name) => groupOf(name) !== group);
    onChange([...kept, label.name]);
    setQuery("");
    setActive(0);
    setWhy(undefined);
  }

  function remove(name: string) {
    onChange(value.filter((chosen) => chosen !== name));
    setWhy(undefined);
  }

  async function choose(option: Option) {
    if (option.at === "label") {
      add(option.label);
      return;
    }

    setBusy(true);
    setWhy(undefined);

    try {
      add(await onCreate!(option.name));
    } catch (problem) {
      setWhy(problem instanceof Error ? problem.message : "The instance did not answer.");
    } finally {
      setBusy(false);
    }
  }

  function onKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();

      if (!open) {
        setOpen(true);
        return;
      }

      if (options.length > 0) {
        setActive((index) => (Math.min(index, options.length - 1) + (event.key === "ArrowDown" ? 1 : -1) + options.length) % options.length);
      }
    } else if (event.key === "Enter") {
      // Enter in a text field submits the form around it. While a choice is
      // open it belongs to the choice, and a name nobody offered is not a
      // reason to save the issue.
      if (query.trim() !== "" || (open && options.length > 0)) {
        event.preventDefault();
      }

      if (open && options.length > 0 && !busy) {
        void choose(options[cursor]);
      }
    } else if (event.key === "Escape") {
      if (open) {
        // The form around this one closes on Escape as well (PLAN-8); the
        // list is what Escape means while it is showing.
        event.preventDefault();
        event.stopPropagation();
        setOpen(false);
      }
    } else if (event.key === "Backspace" && query === "" && value.length > 0) {
      event.preventDefault();
      remove(value[value.length - 1]);
    }
  }

  return (
    <div
      className={cn("grid gap-1 text-sm font-medium", className)}
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget)) {
          setOpen(false);
        }
      }}
    >
      <label htmlFor={id}>{label}</label>
      {hint !== undefined && <span className="text-xs font-normal text-muted-foreground">{hint}</span>}
      <div className="relative">
        <div className="flex min-h-8 w-full flex-wrap items-center gap-1 rounded-lg border border-input bg-transparent px-1.5 py-1 focus-within:border-ring focus-within:ring-3 focus-within:ring-ring/50 dark:bg-input/30">
          {chosen.map((label) => (
            <span
              key={label.name}
              className="inline-flex h-6 max-w-full items-center gap-1 rounded-4xl bg-secondary pr-1 pl-2 text-xs font-medium text-secondary-foreground"
            >
              <span className="truncate">{label.name}</span>
              <button
                type="button"
                aria-label={`Remove ${label.name}`}
                className="rounded-full p-0.5 hover:bg-foreground/10"
                onMouseDown={(event) => event.preventDefault()}
                onClick={() => remove(label.name)}
              >
                <XIcon className="size-3" />
              </button>
            </span>
          ))}
          <input
            id={id}
            role="combobox"
            aria-expanded={open}
            aria-controls={listId}
            aria-autocomplete="list"
            aria-activedescendant={open && options.length > 0 ? `${id}-option-${cursor}` : undefined}
            autoComplete="off"
            className="h-6 min-w-24 flex-1 bg-transparent px-1 text-base font-normal outline-none placeholder:text-muted-foreground md:text-sm"
            placeholder={value.length === 0 ? "Choose labels…" : ""}
            value={query}
            onChange={(event) => {
              setQuery(event.target.value);
              setActive(0);
              setOpen(true);
            }}
            onFocus={() => setOpen(true)}
            onKeyDown={onKeyDown}
          />
        </div>
        {/* Below the field rather than over it: on a phone the suggestions
            must not sit on top of what is being typed. */}
        {open && (
          <ul
            id={listId}
            role="listbox"
            aria-label={label}
            className="absolute inset-x-0 top-full z-20 mt-1 max-h-64 overflow-y-auto rounded-lg border bg-popover py-1 text-sm font-normal shadow-md empty:hidden"
          >
            {options.map((option, index) => (
              <Row
                key={option.at === "create" ? " create" : option.label.name}
                id={`${id}-option-${index}`}
                option={option}
                heading={heading(options, index)}
                active={index === cursor}
                busy={busy && option.at === "create"}
                onHover={() => setActive(index)}
                onChoose={() => void choose(option)}
              />
            ))}
          </ul>
        )}
      </div>
      {why !== undefined && (
        <p role="alert" className="text-xs font-normal text-destructive">
          {why}
        </p>
      )}
    </div>
  );
}

function Row({
  id,
  option,
  heading,
  active,
  busy,
  onHover,
  onChoose,
}: {
  id: string;
  option: Option;
  heading?: string;
  active: boolean;
  busy: boolean;
  onHover: () => void;
  onChoose: () => void;
}) {
  return (
    <>
      {heading !== undefined && (
        <li aria-hidden className="px-3 pt-2 pb-1 text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
          {heading}
        </li>
      )}
      <li
        id={id}
        role="option"
        aria-selected={active}
        // Thumb-sized rows, and the focus stays in the field so that the next
        // one can be chosen without reaching for it again.
        className={cn("flex min-h-9 cursor-pointer items-center gap-2 px-3 py-1.5", active && "bg-accent")}
        onMouseMove={onHover}
        onMouseDown={(event) => event.preventDefault()}
        onClick={onChoose}
      >
        {option.at === "create" ? (
          <span>
            {busy ? "Creating " : "Create "}
            <span className="font-mono text-xs">{option.name}</span>
          </span>
        ) : (
          <>
            <span className="font-mono text-xs">{option.label.name}</span>
            <span className="min-w-0 flex-1 truncate text-xs text-muted-foreground">{option.label.description}</span>
            {option.replaces !== undefined && (
              <span className="shrink-0 text-xs text-brand">replaces {option.replaces}</span>
            )}
          </>
        )}
      </li>
    </>
  );
}

/** The group heading this row opens, if it opens one. */
function heading(options: Option[], index: number): string | undefined {
  const option = options[index];

  if (option.at === "create") {
    return undefined;
  }

  const group = option.label.group ?? "";
  const before = index === 0 ? undefined : options[index - 1];
  const previous = before === undefined || before.at === "create" ? null : before.label.group ?? "";

  if (previous === group) {
    return undefined;
  }

  return group === "" ? "Ungrouped" : `${group} · one of`;
}

/** Grouped first, alphabetically, with the ungrouped ones after them. */
function byGroupThenName(a: PickableLabel, b: PickableLabel): number {
  const left = a.group ?? "";
  const right = b.group ?? "";

  if (left !== right) {
    return left === "" ? 1 : right === "" ? -1 : left.localeCompare(right);
  }

  return a.name.localeCompare(b.name);
}
