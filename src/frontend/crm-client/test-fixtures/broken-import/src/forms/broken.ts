// Fixture for bundle tests: the imported module has type declarations (missing-runtime.d.ts) but no
// runtime file, so it type checks cleanly while esbuild must fail to bundle it.
import { missing } from "./missing-runtime.js";

export function onLoad(): string {
  return missing;
}
