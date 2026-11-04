import { fileURLToPath } from "node:url";
import { defineProject } from "vitest/config";

export default defineProject({
  resolve: {
    // Test against crm-entities sources so tests do not depend on its dist/ build.
    alias: {
      "@ogz/crm-entities": fileURLToPath(new URL("../crm-entities/src/index.ts", import.meta.url)),
    },
  },
  test: {
    name: "crm-client",
    environment: "node",
    include: ["test/**/*.test.ts"],
    setupFiles: ["test/setup.ts"],
  },
});
