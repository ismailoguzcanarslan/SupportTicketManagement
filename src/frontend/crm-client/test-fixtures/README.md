Inputs for `test/build/*.test.ts`. They are outside `src/` and `test/`, so neither the real build nor
`npm run typecheck` sees them, and they contain no TypeScript errors (no red squiggles in editors):

- `broken-import/`: an entry point whose import has types but no runtime file, so esbuild must fail.
- `no-entries/`: a package folder without `src/forms` or `src/commands`.
- `duplicate-entry/`: `src/forms/sample.ts` and `src/forms/sample/index.ts`, two entry points for one bundle.

The "invalid TypeScript" check uses an in-memory source instead of a file.
