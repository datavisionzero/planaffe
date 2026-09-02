import { useEffect, useState } from "react";
import { api, type Project } from "@/api/client";

/**
 * The projects the signed-in identity can see, asked once per load and
 * handed to the switcher and the landing route. Multiple projects are the
 * normal case (VISION 6.2), so this is part of the frame, not of a screen.
 */
export type Projects =
  | { at: "asking" }
  | { at: "failed" }
  | { at: "known"; projects: Project[] };

export function useProjects(): Projects {
  const [projects, setProjects] = useState<Projects>({ at: "asking" });

  useEffect(() => {
    let current = true;

    void (async () => {
      try {
        const { data } = await api.GET("/projects");

        if (current) {
          setProjects(data === undefined ? { at: "failed" } : { at: "known", projects: data });
        }
      } catch {
        if (current) {
          setProjects({ at: "failed" });
        }
      }
    })();

    return () => {
      current = false;
    };
  }, []);

  return projects;
}

/**
 * The project the user was in last, so that `/` lands there. A convenience of
 * this browser, nothing the instance knows.
 */
const lastProjectKey = "planaffe.project";

export function rememberProject(key: string): void {
  try {
    window.localStorage.setItem(lastProjectKey, key);
  } catch {
    // A browser that keeps nothing lands on the first project instead.
  }
}

export function lastProject(): string | null {
  try {
    return window.localStorage.getItem(lastProjectKey);
  } catch {
    return null;
  }
}
