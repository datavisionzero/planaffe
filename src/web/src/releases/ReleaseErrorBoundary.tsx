import { Component, type ReactNode } from "react";
import { Link } from "react-router";
import { Button } from "@/components/ui/button";
import { PageHeader } from "@/shared/PageHeader";

/** Keep a failed detail screen from taking the shell and browser history with it. */
export class ReleaseErrorBoundary extends Component<{ project: string; children: ReactNode }, { failed: boolean }> {
  state = { failed: false };

  static getDerivedStateFromError() {
    return { failed: true };
  }

  render() {
    if (!this.state.failed) return this.props.children;

    return (
      <>
        <PageHeader title="Release unavailable" />
        <div className="space-y-3 p-4 text-sm">
          <p>This release screen could not load.</p>
          <div className="flex items-center gap-4">
            <Link className="text-brand hover:underline" to={`/${this.props.project}/releases`}>All releases</Link>
            <Button variant="outline" size="sm" onClick={() => window.location.reload()}>Reload page</Button>
          </div>
        </div>
      </>
    );
  }
}
