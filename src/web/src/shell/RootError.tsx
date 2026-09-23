/**
 * The router's last resort. Every screen fails inside the shell's own
 * boundary; this is for a failure above it — the session check or the frame
 * itself — where React Router would otherwise draw its developer page,
 * "Unexpected Application Error", in place of the whole application.
 */
export function RootError() {
  return (
    <main role="alert" className="flex min-h-svh flex-col items-center justify-center gap-3 p-6 text-center">
      <p className="font-medium">planaffe could not load.</p>
      <p className="text-sm text-muted-foreground">A new version may have been deployed while this tab was open.</p>
      <button
        type="button"
        className="text-brand text-sm underline-offset-4 hover:underline"
        onClick={() => window.location.reload()}
      >
        Reload page
      </button>
    </main>
  );
}
