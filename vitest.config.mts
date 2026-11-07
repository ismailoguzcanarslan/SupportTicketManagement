import { defineConfig } from "vitest/config";

// Root Vitest config: runs every frontend package as a separate project.
// Coverage is configured here because, with projects, it is collected once for the whole run.
export default defineConfig({
  test: {
    projects: ["src/frontend/*"],
    coverage: {
      provider: "v8",
      reportsDirectory: "coverage",
      reporter: ["text", "html", "lcov", "cobertura"],
      include: ["src/frontend/*/src/**/*.{ts,tsx}"],
      // Generated code is not handwritten and is not measured.
      // Page entries only mount the page; their bundles are built and checked by the crm-pages build tests.
      exclude: ["src/frontend/*/src/generated/**", "**/*.d.ts", "src/frontend/crm-pages/src/pages/*/index.tsx"],
    },
  },
});
