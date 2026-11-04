import { describe, expect, it } from "vitest";
import { PUBLISHER_PREFIX, SOLUTION_UNIQUE_NAME } from "../../src/constants/solution.js";

describe("solution constants", () => {
  it("match the Dataverse solution", () => {
    expect(PUBLISHER_PREFIX).toBe("ogz");
    expect(SOLUTION_UNIQUE_NAME).toBe("OguzLabsSupportTicketManagement");
  });
});
