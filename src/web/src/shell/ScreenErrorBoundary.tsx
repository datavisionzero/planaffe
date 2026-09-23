import { Component, type ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { PageHeader } from "@/shared/PageHeader";

type Props = {
  /** The address the screen stands on; a new one lets the next screen try. */
  at: string;
  children: ReactNode;
};

type State = { failed: boolean; at: string };

/**
 * Keeps a screen that failed — a lazy chunk a redeploy took away, or a render
 * that threw — from taking the shell and the browser history with it
 * (ADR 0006: the frame stands before any screen does). The frame stays, so the
 * navigation is the way on; the screen area says what happened and offers a
 * reload.
 *
 * It stands around the routes rather than inside each one, so it is not
 * remounted by navigation. It forgets the failure when the address changes
 * instead: leaving a broken screen for another one shows that one, and not the
 * sentence about the screen that was left.
 */
export class ScreenErrorBoundary extends Component<Props, State> {
  state: State = { failed: false, at: this.props.at };

  static getDerivedStateFromError(): Partial<State> {
    return { failed: true };
  }

  static getDerivedStateFromProps(props: Props, state: State): Partial<State> | null {
    return props.at === state.at ? null : { failed: false, at: props.at };
  }

  render() {
    if (!this.state.failed) return this.props.children;

    return (
      <>
        <PageHeader title="Screen unavailable" />
        <div role="alert" className="space-y-3 p-4 text-sm">
          <p>This screen could not load. The navigation still works, and a reload fetches the screen again.</p>
          <Button variant="outline" size="sm" onClick={() => window.location.reload()}>Reload page</Button>
        </div>
      </>
    );
  }
}
