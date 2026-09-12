import path from "node:path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
// From vitest rather than vite, so that the test section below is typed too.
import { defineConfig } from "vitest/config";

/**
 * The first path segment of every route the contract has. Development runs
 * the two toolchains side by side (docs/codebase.md): Vite serves the SPA and
 * forwards what belongs to the instance, so that the application reaches the
 * API at its own origin there as well as in the image.
 */
const instanceRoutes = [
  "/admin",
  "/agents",
  "/epics",
  "/issues",
  "/invitations",
  "/me",
  "/openapi",
  "/projects",
  "/questions",
  "/password-recovery",
  "/session",
  "/sessions",
  "/spaces",
  "/tokens",
  "/users",
  "/version",
];

const toInstance = {
  target: "http://localhost:5142",
  bypass(request: { url?: string; headers: Record<string, string | string[] | undefined> }) {
    const asked = (request.url ?? "/").split("?")[0]!;
    const wants = String(request.headers["accept"] ?? "");

    // A document the browser is navigating to belongs to the application; a
    // path with an extension is an asset or the contract and belongs to
    // whoever answers it. Anything else is the application asking, and goes on
    // to the instance.
    return wants.includes("text/html") && !/\.[^/]+$/.test(asked) ? "/index.html" : undefined;
  },
};

export default defineConfig({
  plugins: [react(), tailwindcss()],

  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },

  // A local `npm run build` lands where the server serves static files from, so
  // that one `dotnet run` gives the whole product. The image does the same in
  // two stages (deploy/Dockerfile).
  build: {
    outDir: "../Planaffe.Api/wwwroot",
    emptyOutDir: true,
  },

  server: {
    port: 5173,
    // Several of these are an address of the application as well as one of the
    // instance — `/projects`, `/admin/projects`, `/spaces`. A navigation to
    // one of them is the screen and stays here; everything else is forwarded.
    // The served application makes the same decision, from the other side and
    // on the same headers (src/Planaffe.Api/Http/BrowserNavigation.cs).
    proxy: Object.fromEntries(instanceRoutes.map((route) => [route, toInstance])),
  },

  test: {
    environment: "jsdom",
    setupFiles: ["./src/shared/setupTests.tsx"],
  },
});
