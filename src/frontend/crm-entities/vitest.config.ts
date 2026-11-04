import { defineProject } from "vitest/config";

export default defineProject({
  test: {
    name: "crm-entities",
    environment: "node",
    include: ["test/**/*.test.ts"],
  },
});
