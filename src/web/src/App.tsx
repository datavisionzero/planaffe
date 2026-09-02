import { useEffect, useState } from "react";
import { api, whenSignedOut, type Me } from "@/api/client";
import { SignIn } from "@/session/SignIn";
import { SessionProvider } from "@/session/Session";
import { forgetToken, readToken } from "@/session/token";
import { Shell } from "@/shell/Shell";

/**
 * Whether this browser is somebody, which decides the first screen.
 *
 * A browser without a token gets the sign-in field. One with a token asks the
 * instance who it is, and shows the shell once that answers — the frame
 * renders before any list does (ADR 0006), and a token the instance no longer
 * accepts sends the user back to the field.
 */
type Standing =
  | { at: "asking" }
  | { at: "unreachable" }
  | { at: "stranger" }
  | { at: "known"; me: Me };

export function App() {
  const [standing, setStanding] = useState<Standing>(() =>
    readToken() === null ? { at: "stranger" } : { at: "asking" },
  );

  useEffect(() => {
    if (standing.at !== "asking") {
      return;
    }

    let current = true;

    void (async () => {
      try {
        const { data, response } = await api.GET("/me");

        if (!current) {
          return;
        }

        if (data !== undefined) {
          setStanding({ at: "known", me: data });
        } else if (response.status === 401) {
          forgetToken();
          setStanding({ at: "stranger" });
        } else {
          setStanding({ at: "unreachable" });
        }
      } catch {
        if (current) {
          setStanding({ at: "unreachable" });
        }
      }
    })();

    return () => {
      current = false;
    };
  }, [standing.at]);

  useEffect(
    () =>
      whenSignedOut(() => {
        forgetToken();
        setStanding({ at: "stranger" });
      }),
    [],
  );

  switch (standing.at) {
    case "asking":
      return null;

    case "unreachable":
      return (
        <main className="flex min-h-svh flex-col items-center justify-center gap-3 p-6 text-center">
          <p className="font-medium">The instance did not answer.</p>
          <button
            type="button"
            className="text-brand text-sm underline-offset-4 hover:underline"
            onClick={() => setStanding({ at: "asking" })}
          >
            Try again
          </button>
        </main>
      );

    case "stranger":
      return <SignIn onSignedIn={(me) => setStanding({ at: "known", me })} />;

    case "known":
      return (
        <SessionProvider
          value={{
            me: standing.me,
            signOut: () => {
              forgetToken();
              setStanding({ at: "stranger" });
            },
          }}
        >
          <Shell />
        </SessionProvider>
      );
  }
}
