import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { createBrowserRouter } from "react-router";
import { RouterProvider } from "react-router/dom";

import "./index.css";
import { App } from "./App";
import { ThemeProvider } from "@/components/theme-provider";
import { TooltipProvider } from "@/components/ui/tooltip";
import { RootError } from "@/shell/RootError";
import { installStaleChunkRecovery } from "@/shell/staleChunk";

installStaleChunkRecovery();

const router = createBrowserRouter([{ path: "*", element: <App />, errorElement: <RootError /> }]);

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <ThemeProvider storageKey="planaffe.theme">
      <TooltipProvider>
        <RouterProvider router={router} />
      </TooltipProvider>
    </ThemeProvider>
  </StrictMode>,
);
