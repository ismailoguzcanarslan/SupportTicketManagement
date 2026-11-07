// Types for build.mjs, used by the build tests. Keep in sync with the exports of build.mjs.

import type { BuildOptions, BuildResult } from "esbuild";

/** One dialog page and the two Web Resources it is built into. */
export interface Page {
  /** Folder name, e.g. "reopenTicket". */
  name: string;
  /** Absolute path of src/pages/<name>/index.tsx. */
  entryPoint: string;
  /** Absolute path of dist/webresources/pages/<name>.js. */
  script: string;
  /** Absolute path of dist/webresources/pages/<name>.html. */
  html: string;
}

/** Pages of the package (default: crm-pages itself), in sorted order. */
export declare function findPages(packageDir?: string): Page[];

/** The HTML web resource that loads <name>.js. */
export declare function pageHtml(name: string): string;

export declare function buildOptions(page: Page, options?: { dev?: boolean }): BuildOptions;

/** Bundles every page (and writes its HTML); rejects without pages or on the first esbuild error. */
export declare function buildPages(pages: Page[], options?: { dev?: boolean; write?: boolean }): Promise<BuildResult<{ write: boolean }>[]>;
