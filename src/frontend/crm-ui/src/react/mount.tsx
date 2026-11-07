// Starts a page in its HTML web resource: makes the app's Xrm available and renders the page inside the
// theme. Each page's index.tsx calls mountPage(<ThePage />).

import type { ReactElement } from "react";
import { createRoot } from "react-dom/client";
import { OgzThemeProvider } from "./theme.js";

/**
 * A web resource dialog runs in its own frame without a global Xrm; the app's Xrm is on the parent
 * window (same origin). Copies it to this window, so code written for form scripts (getXrm()) works.
 */
export function connectXrm(win: Window = globalThis.window): boolean {
  if (Reflect.get(globalThis, "Xrm") !== undefined) return true;
  try {
    const parentXrm: unknown = win.parent === win ? undefined : Reflect.get(win.parent, "Xrm");
    if (parentXrm === undefined) return false;
    Reflect.set(globalThis, "Xrm", parentXrm);
    return true;
  } catch {
    // A parent on another origin cannot be read.
    return false;
  }
}

export function mountPage(page: ReactElement, container: HTMLElement | null = document.getElementById("root")): void {
  if (container === null) throw new Error("The page has no element with id 'root'.");
  connectXrm();
  createRoot(container).render(<OgzThemeProvider>{page}</OgzThemeProvider>);
}
