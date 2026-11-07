import { defineProject } from "vitest/config";

export default defineProject({
  test: {
    name: "crm-ui",
    // React components render into a simulated browser DOM.
    environment: "jsdom",
    include: ["test/**/*.test.{ts,tsx}"],
    setupFiles: ["test/setup.ts"],
  },
});
