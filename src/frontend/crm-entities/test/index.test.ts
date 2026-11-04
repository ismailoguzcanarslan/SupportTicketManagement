import { describe, expect, it } from "vitest";

describe("@ogz/crm-entities entry point", () => {
  it("loads the generated barrel", async () => {
    const entities = await import("../src/index.js");

    expect(entities).toBeTypeOf("object");
  });
});
