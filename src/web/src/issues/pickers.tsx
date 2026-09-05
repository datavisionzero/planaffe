import { useEffect, useState } from "react";
import { api, type Schemas } from "@/api/client";
import { Picker, type Choice } from "@/components/ui/picker";
import { StatusDot } from "./status";
import { statusLabel } from "./statusLabel";

/**
 * The three fillings of the {@link Picker} the issue form needs beside the
 * labels: the project's epics, the project's members, and a search across
 * issues for a parent and for blockers. Each one is a source of rows and
 * nothing more; the keyboard, the chips and the refusal at the field are the
 * picker's.
 */

/**
 * The epic an issue hangs under. A closed one says so on its row: attaching an
 * issue to it reopens it, and that is worth knowing before choosing rather
 * than after saving.
 */
export function EpicPicker({
  epics,
  value,
  onChange,
  error,
}: {
  epics: Schemas["EpicSummary"][];
  value: string;
  onChange: (key: string) => void;
  error?: string;
}) {
  const choices: Choice[] = epics.map((epic) => ({
    id: epic.key,
    name: epic.key,
    hint: epic.title,
    note: epic.status === "closed" ? <span className="text-brand">closed</span> : undefined,
  }));

  return (
    <Picker
      label="Epic"
      placeholder="No epic"
      empty="No epic of this project matches."
      error={error}
      choices={choices}
      value={value === "" ? [] : [value]}
      onChange={(keys) => onChange(keys[0] ?? "")}
    />
  );
}

/**
 * Who it is on. Nobody is the normal case (VISION 8), so it is a row of its
 * own rather than the emptying of a field.
 */
export function AssigneePicker({
  project,
  value,
  onChange,
  error,
}: {
  project: string | undefined;
  value: string;
  onChange: (name: string) => void;
  error?: string;
}) {
  const [members, setMembers] = useState<Schemas["UserSummary"][]>([]);

  useEffect(() => {
    if (project === undefined) return;

    let current = true;

    void (async () => {
      try {
        const { data } = await api.GET("/projects/{key}/users", { params: { path: { key: project } } });
        if (current) setMembers(data ?? []);
      } catch {
        if (current) setMembers([]);
      }
    })();

    return () => {
      current = false;
    };
  }, [project]);

  const choices: Choice[] = [
    { id: "", name: "Nobody", hint: "Whoever takes it next" },
    ...members.map((member) => ({ id: member.name, name: member.name, hint: member.email })),
  ];

  return (
    <Picker
      label="Assignee"
      placeholder="Nobody"
      empty="No member of this project matches."
      error={error}
      choices={choices}
      value={value === "" ? [] : [value]}
      onChange={(names) => onChange(names[0] ?? "")}
    />
  );
}

/**
 * An issue, found the way the command palette finds one: by key or by title,
 * with the status it is in. One of them for a parent, several for the blockers.
 */
export function IssuePicker({
  label,
  hint,
  project,
  multiple = false,
  exclude = [],
  value,
  onChange,
  error,
}: {
  label: string;
  hint?: string;
  project: string | undefined;
  multiple?: boolean;
  /** Keys this field must not offer — an issue is never its own parent. */
  exclude?: string[];
  value: string[];
  onChange: (keys: string[]) => void;
  error?: string;
}) {
  const [query, setQuery] = useState("");
  const [found, setFound] = useState<Schemas["IssueSummary"][]>([]);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (project === undefined) return;

    let current = true;
    // A keystroke is not a search. The first, empty question is asked at once
    // so that opening the field already offers something.
    const timer = setTimeout(
      () => {
        void (async () => {
          if (current) setBusy(true);

          try {
            const { data } = await api.GET("/issues", {
              params: { query: { project, q: query.trim() || undefined, limit: 10 } },
            });
            if (current) setFound(data?.items ?? []);
          } catch {
            if (current) setFound([]);
          } finally {
            if (current) setBusy(false);
          }
        })();
      },
      query === "" ? 0 : 200,
    );

    return () => {
      current = false;
      clearTimeout(timer);
    };
  }, [project, query]);

  const hidden = new Set(exclude);
  const choices: Choice[] = found
    .filter((issue) => !hidden.has(issue.key))
    .map((issue) => ({
      id: issue.key,
      name: issue.key,
      hint: issue.title,
      note: (
        <>
          <StatusDot status={issue.status} />
          <span className="text-muted-foreground">{statusLabel(issue.status)}</span>
        </>
      ),
    }));

  return (
    <Picker
      label={label}
      hint={hint}
      multiple={multiple}
      placeholder="Key or title…"
      empty={busy ? "Asking the instance…" : "No issue of this project matches."}
      busy={busy}
      error={error}
      choices={choices}
      value={value}
      onChange={onChange}
      onQuery={setQuery}
    />
  );
}
