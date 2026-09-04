import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router";
import { api, describe } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/shared/PageHeader";

export function NewProjectView() {
  const navigate = useNavigate(); const [error, setError] = useState("");
  async function create(event: FormEvent<HTMLFormElement>) { event.preventDefault(); const data = new FormData(event.currentTarget); const result = await api.POST("/projects", { body: { key: String(data.get("key")), name: String(data.get("name")), triage_required: data.has("triage"), review_required: data.has("review") } }); if (result.data) navigate(`/${result.data.key}/ready`); else setError(describe(result.error, result.response.status)); }
  return <><PageHeader title="Create project" /><form className="mx-auto grid w-full max-w-lg gap-4 p-6" onSubmit={(e) => void create(e)}><label className="text-sm">Key<Input name="key" required pattern="[A-Za-z][A-Za-z0-9]{1,9}" className="mt-1 uppercase" /></label><label className="text-sm">Name<Input name="name" required className="mt-1" /></label><label className="flex gap-2 text-sm"><input name="triage" type="checkbox" /> Require triage</label><label className="flex gap-2 text-sm"><input name="review" type="checkbox" /> Require review</label>{error && <p role="alert" className="text-sm text-destructive">{error}</p>}<Button type="submit">Create project</Button></form></>;
}
