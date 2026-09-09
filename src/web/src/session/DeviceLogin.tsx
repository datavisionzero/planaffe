import { useEffect, useState, type FormEvent } from "react";
import { useSearchParams } from "react-router";
import { api, describe, type Me } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { AuthFrame, Field } from "./SignIn";

/**
 * The confirmation page a terminal sends somebody to (ADR 0025).
 *
 * A console somewhere printed eight characters and is polling; this is the
 * half where a human says whether that was them. It authenticates with the
 * browser session they already have — planaffe has a sign-in screen, so
 * asking for a password a second time on this one page would be a second
 * mechanism for nothing.
 */
export function DeviceLogin({ me }: { me: Me }) {
  const [parameters] = useSearchParams();
  const fromAddress = normalize(parameters.get("code") ?? "");
  const [typed, setTyped] = useState(() => forReading(fromAddress));
  const [waiting, setWaiting] = useState<string | null>(null);
  const [refusal, setRefusal] = useState<string | null>(null);
  const [decided, setDecided] = useState<"approved" | "refused" | null>(null);
  const [asking, setAsking] = useState(fromAddress !== "");

  const code = normalize(typed);

  // A code in the address is a code somebody already read out of a terminal:
  // look it up rather than making them press a button to be asked again.
  useEffect(() => {
    if (!fromAddress) return;

    let current = true;

    void (async () => {
      const found = await look(fromAddress);
      if (!current) return;
      if (found.code) setWaiting(found.code);
      else setRefusal(found.refusal);
      setAsking(false);
    })();

    return () => {
      current = false;
    };
  }, [fromAddress]);

  async function find(event: FormEvent) {
    event.preventDefault();
    setAsking(true);
    setRefusal(null);
    const found = await look(code);
    if (found.code) setWaiting(found.code);
    else setRefusal(found.refusal);
    setAsking(false);
  }

  async function decide(approve: boolean) {
    setAsking(true);
    setRefusal(null);
    try {
      const { error, response } = await api.POST("/device-logins/{code}/decide", {
        params: { path: { code } },
        body: { approve },
      });
      if (response.ok) setDecided(approve ? "approved" : "refused");
      else setRefusal(describe(error, response.status));
    } catch {
      setRefusal("The instance did not answer.");
    } finally {
      setAsking(false);
    }
  }

  if (decided) {
    return (
      <AuthFrame>
        <h1 className="text-xl font-semibold">
          {decided === "approved" ? "That console is signed in." : "That login was refused."}
        </h1>
        <p className="text-muted-foreground text-sm">
          {decided === "approved"
            ? "Go back to the terminal — it has what it was waiting for. Nothing else happens here."
            : "Nothing was granted. If you did not start this login, no further step is needed."}
        </p>
      </AuthFrame>
    );
  }

  if (waiting) {
    return (
      <AuthFrame>
        <div>
          <h1 className="text-xl font-semibold">Sign in a console?</h1>
          <p className="text-muted-foreground mt-1 text-sm">
            A console is waiting on the code <span className="font-mono">{waiting}</span>.
          </p>
        </div>
        {/* What the confirmation grants, before the button that grants it. */}
        <p className="text-sm">
          Confirming gives that console a key to planaffe as <strong>{me.name}</strong>, with
          everything you can see and do. It does not expire; you can revoke it later under Tokens.
        </p>
        <p className="text-muted-foreground text-sm">
          If you did not just run <span className="font-mono">pa login</span>, refuse it.
        </p>
        {refusal && (
          <p role="alert" className="text-destructive text-sm">
            {refusal}
          </p>
        )}
        <div className="space-y-3">
          <Button type="button" className="w-full" disabled={asking} onClick={() => void decide(true)}>
            Sign in this console
          </Button>
          {/* No harder to reach than the approval: a refusal somebody has to
              hunt for is a refusal nobody makes. */}
          <Button
            type="button"
            variant="outline"
            className="w-full"
            disabled={asking}
            onClick={() => void decide(false)}
          >
            I did not start this
          </Button>
        </div>
      </AuthFrame>
    );
  }

  return (
    <AuthFrame>
      <div>
        <h1 className="text-xl font-semibold">Sign in a console</h1>
        <p className="text-muted-foreground mt-1 text-sm">
          Enter the code your terminal printed.
        </p>
      </div>
      <form onSubmit={find} className="space-y-5">
        <Field label="Code">
          <Input
            id="code"
            autoFocus
            autoComplete="off"
            spellCheck={false}
            placeholder="XXXX-XXXX"
            className="font-mono tracking-widest uppercase"
            value={typed}
            onChange={(event) => setTyped(forReading(event.target.value))}
          />
        </Field>
        {refusal && (
          <p role="alert" className="text-destructive text-sm">
            {refusal}
          </p>
        )}
        <Button type="submit" className="w-full" disabled={asking || code.length !== 8}>
          Continue
        </Button>
      </form>
    </AuthFrame>
  );
}

/** One lookup, answering either the code as it is read or why not. */
async function look(code: string): Promise<{ code?: string; refusal: string }> {
  try {
    const { data, error, response } = await api.GET("/device-logins/{code}", {
      params: { path: { code } },
    });
    return data ? { code: data.user_code, refusal: "" } : { refusal: describe(error, response.status) };
  } catch {
    return { refusal: "The instance did not answer." };
  }
}

/** The alphabet of the code: the twenty consonants of RFC 8628. */
const alphabet = "BCDFGHJKLMNPQRSTVWXZ";

/** What the instance stores: case, spaces and dashes are how it was shown. */
function normalize(typed: string) {
  return [...typed.toUpperCase()].filter((character) => alphabet.includes(character)).join("");
}

/** What a person sees while they type it: `XXXX-XXXX`. */
function forReading(typed: string) {
  const code = normalize(typed).slice(0, 8);
  return code.length > 4 ? `${code.slice(0, 4)}-${code.slice(4)}` : code;
}
