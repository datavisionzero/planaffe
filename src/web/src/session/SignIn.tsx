import { useState, type FormEvent } from "react";
import { api, describe, type Me } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { keepToken } from "./token";

/**
 * The first screen of a browser that has no token: a field to paste one into.
 *
 * There is no password and no account form here on purpose. Identities are
 * created by an administrator with `pa user create`, and a user's key to the
 * console and to this page is the same user token (ADR 0015). The token is
 * checked by asking the instance who it is, and kept only when that answers.
 */
export function SignIn({ onSignedIn }: { onSignedIn: (me: Me) => void }) {
  const [token, setToken] = useState("");
  const [refusal, setRefusal] = useState<string | null>(null);
  const [asking, setAsking] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();

    const candidate = token.trim();

    if (candidate === "") {
      return;
    }

    setAsking(true);
    setRefusal(null);

    try {
      const { data, error, response } = await api.GET("/me", {
        headers: { Authorization: `Bearer ${candidate}` },
      });

      if (data === undefined) {
        setRefusal(
          response.status === 401
            ? "The instance does not know this token."
            : describe(error, response.status),
        );
        return;
      }

      keepToken(candidate);
      onSignedIn(data);
    } catch {
      setRefusal("The instance did not answer.");
    } finally {
      setAsking(false);
    }
  }

  return (
    <main className="flex min-h-svh items-center justify-center p-6">
      <form onSubmit={submit} className="w-full max-w-sm space-y-5">
        <div className="flex items-center gap-2 text-base font-semibold">
          <span aria-hidden className="size-4.5 rounded-sm bg-brand" />
          planaffe
        </div>

        <div className="space-y-2">
          <label htmlFor="token" className="block text-sm font-medium">
            Your token
          </label>
          <Input
            id="token"
            type="password"
            autoComplete="off"
            autoFocus
            value={token}
            onChange={(event) => setToken(event.target.value)}
            placeholder="pa_…"
            className="font-mono"
          />
          <p className="text-muted-foreground text-xs">
            The user token an administrator created for you with{" "}
            <code className="font-mono">pa token create</code>. It stays in this browser.
          </p>
        </div>

        {refusal !== null && (
          <p role="alert" className="text-destructive text-sm">
            {refusal}
          </p>
        )}

        <Button type="submit" disabled={asking || token.trim() === ""} className="w-full">
          Sign in
        </Button>
      </form>
    </main>
  );
}
