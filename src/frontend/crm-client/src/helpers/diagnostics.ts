// Development diagnostics: explains why a rule did nothing (a column, tab or section that is not on the
// form). Helpers and rules skip what is missing on purpose, so one script works on every form of a table;
// that also hides typos such as a wrong section name. Off by default; turn it on in the browser console
// of the model-driven app and reload the form:
//
//   localStorage.setItem("ogz_diagnostics", "1")      // on
//   localStorage.removeItem("ogz_diagnostics")        // off
//
// Each message is written once per page, as a console warning prefixed with "[ogz]".

const STORAGE_KEY = "ogz_diagnostics";

let override: boolean | undefined;
const written = new Set<string>();

function storageEnabled(): boolean {
  try {
    return globalThis.localStorage?.getItem(STORAGE_KEY) === "1";
  } catch {
    // Storage can be blocked (privacy settings, sandboxed frames).
    return false;
  }
}

export const Diagnostics = {
  /** True when diagnostics are on (localStorage "ogz_diagnostics" = "1", or set by code). */
  isEnabled(): boolean {
    return override ?? storageEnabled();
  },

  /** Turns diagnostics on or off by code (tests); undefined goes back to the localStorage setting. */
  setEnabled(enabled: boolean | undefined): void {
    override = enabled;
    written.clear();
  },

  /** Writes the message once, when diagnostics are on. */
  warn(message: string): void {
    if (!Diagnostics.isEnabled() || written.has(message)) return;
    written.add(message);
    console.warn(`[ogz] ${message}`);
  },

  /** Warns that `what` is not on the form when `found` is false; returns `found`. */
  reportMissing(found: boolean, what: string, context: string): boolean {
    if (!found) Diagnostics.warn(`${context}: ${what} is not on the form; skipped.`);
    return found;
  },
} as const;
