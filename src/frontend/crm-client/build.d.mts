// Types for build.mjs, used by the bundle tests. Keep in sync with the exports of build.mjs.

import type { BuildOptions, BuildResult } from "esbuild";

/** One Web Resource bundle: a form or command entry point and where it is written. */
export interface BuildEntry {
  /** Absolute path of src/<group>/<name>/index.ts or src/<group>/<name>.ts. */
  entryPoint: string;
  /** Absolute path of dist/webresources/<group>/<name>.js. */
  outfile: string;
  /** Global the bundle exposes, e.g. "Ogz.Forms.SupportTicket". */
  globalName: string;
}

export declare function toPascalCase(name: string): string;

/** Entry points of the package (default: crm-client itself), in sorted order. */
export declare function findEntries(packageDir?: string): BuildEntry[];

export declare function buildOptions(entry: BuildEntry, options?: { dev?: boolean }): BuildOptions;

/** Throws when there is nothing to bundle. */
export declare function requireEntries(entries: BuildEntry[]): BuildEntry[];

/** Bundles every entry; rejects on the first esbuild error. `write: false` keeps output in memory. */
export declare function buildEntries(
  entries: BuildEntry[],
  options?: { dev?: boolean; write?: boolean },
): Promise<BuildResult<{ write: boolean }>[]>;
