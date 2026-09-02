/**
 * The user's token, kept in this browser.
 *
 * The API has one way in — a bearer token (ADR 0015) — and the web application
 * is one client of it like `pa` is. So signing in is pasting a token, the
 * same one `pa` uses from `PLANAFFE_TOKEN`, and it stays in localStorage until
 * the user signs out or the instance stops accepting it. Nothing else of the
 * session is kept: who the token is, is asked of the instance on every load.
 */
const key = "planaffe.token";

export function readToken(): string | null {
  try {
    return window.localStorage.getItem(key);
  } catch {
    return null;
  }
}

export function keepToken(token: string): void {
  window.localStorage.setItem(key, token);
}

export function forgetToken(): void {
  window.localStorage.removeItem(key);
}
