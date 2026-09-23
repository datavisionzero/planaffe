/**
 * What the frame does when a lazy screen's chunk is gone.
 *
 * Every build names its chunks by their hash, and a redeploy takes the old
 * ones away. A tab opened before it still holds the old entry module and asks
 * for a chunk that answers 404; Vite says so with `vite:preloadError` before
 * the import rejects. One reload fetches the new entry and the screen with it.
 *
 * Only one: a chunk that is still missing after the reload is not stale but
 * broken, and reloading again would be a loop. The reload is remembered in
 * `sessionStorage` for a short while, and where storage cannot be written the
 * reload is not tried at all — without the memory there is no guard — so the
 * error reaches the screen's boundary and the reader decides.
 */
const key = "planaffe.stale-chunk-reload";
const guardMs = 10_000;

export function recoverFromStaleChunk(
  event: Event,
  reload: () => void = () => window.location.reload(),
  now: number = Date.now(),
) {
  try {
    const last = Number(window.sessionStorage.getItem(key) ?? 0);

    if (now - last < guardMs) {
      return;
    }

    window.sessionStorage.setItem(key, String(now));
  } catch {
    return;
  }

  event.preventDefault();
  reload();
}

export function installStaleChunkRecovery() {
  window.addEventListener("vite:preloadError", (event) => recoverFromStaleChunk(event));
}
