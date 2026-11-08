import { useCallback, useEffect, useState } from "react";
import { api, type Schemas } from "@/api/client";
export type ProjectGroup = Schemas["ProjectGroup"];
export function useProjectGroups() {
  const [groups, setGroups] = useState<ProjectGroup[]>([]);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);
  const reload = useCallback(async () => {
    try {
      const { data } = await api.GET("/project-groups");
      setFailed(!data);
      if (data) setGroups(data);
    } catch { setFailed(true); } finally { setLoading(false); }
  }, []);
  useEffect(() => { void (async () => { await reload(); })(); }, [reload]);
  return { groups, failed, loading, reload };
}
