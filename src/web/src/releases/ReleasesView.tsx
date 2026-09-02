import { ArrivesLater } from "@/shared/PageHeader";

export function ReleasesView() {
  return (
    <ArrivesLater
      title="Releases"
      hint="What shipped together — the open release fills itself as issues are done."
      cut="cut two, which adds releases to the API"
    />
  );
}
