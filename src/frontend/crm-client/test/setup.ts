import { afterEach, vi } from "vitest";
import { uninstallXrmMock } from "./mocks/index.js";

// No test may leak a global Xrm or mock state into the next one.
afterEach(() => {
  uninstallXrmMock();
  vi.restoreAllMocks();
});
