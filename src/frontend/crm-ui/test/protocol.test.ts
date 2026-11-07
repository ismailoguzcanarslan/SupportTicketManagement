import { describe, expect, it, vi } from "vitest";
import {
  createRequest,
  defineDialog,
  encodeRequest,
  MAX_ENCODED_REQUEST_LENGTH,
  readRequest,
  takeResult,
  toErrorMessage,
  writeResult,
} from "../src/protocol/index.js";

const Sample = defineDialog<{ ticketId: string }, { reopenedOn: string }>({ page: "sample", width: 400 });
const search = (data: unknown) => `?data=${encodeURIComponent(typeof data === "string" ? data : JSON.stringify(data))}`;

function memoryStorage(): Storage {
  const items = new Map<string, string>();
  return {
    get length() {
      return items.size;
    },
    clear: () => items.clear(),
    getItem: (key) => items.get(key) ?? null,
    key: (index) => [...items.keys()][index] ?? null,
    removeItem: (key) => void items.delete(key),
    setItem: (key, value) => void items.set(key, value),
  };
}

describe("defineDialog", () => {
  it("names the page's HTML web resource", () => {
    expect(Sample).toEqual({ page: "sample", width: 400, webResource: "ogz_/pages/sample.html" });
  });
});

describe("requests", () => {
  it("travel as JSON in the data parameter and are read back on the page", () => {
    const request = createRequest(Sample, { ticketId: "t1" }, "r1");

    expect(request).toEqual({ v: 1, requestId: "r1", page: "sample", input: { ticketId: "t1" } });
    expect(readRequest(Sample, search(encodeRequest(request)))).toEqual(request);
  });

  it("get a new id each time", () => {
    expect(createRequest(Sample, { ticketId: "t" }).requestId).not.toBe(createRequest(Sample, { ticketId: "t" }).requestId);
  });

  it("get an id without crypto.randomUUID too", () => {
    vi.stubGlobal("crypto", {});
    try {
      expect(createRequest(Sample, { ticketId: "t" }).requestId).toMatch(/^[a-z0-9]+-[a-z0-9]+$/);
    } finally {
      vi.unstubAllGlobals();
    }
  });

  it("are refused when too long for a URL", () => {
    const request = createRequest(Sample, { ticketId: "x".repeat(MAX_ENCODED_REQUEST_LENGTH) });

    expect(() => encodeRequest(request)).toThrow("too long for the page URL");
  });

  it.each([
    ["no data parameter", ""],
    ["data that is not JSON", search("not json")],
    ["another page", search({ v: 1, requestId: "r", page: "other", input: {} })],
    ["another version", search({ v: 2, requestId: "r", page: "sample", input: {} })],
    ["no request id", search({ v: 1, page: "sample", input: {} })],
    ["no input", search({ v: 1, requestId: "r", page: "sample", input: null })],
  ])("are not read from %s", (_, value) => {
    expect(readRequest(Sample, value)).toBeNull();
  });

  it("are read from the page URL by default", () => {
    window.history.replaceState(null, "", `/page.html${search({ v: 1, requestId: "r", page: "sample", input: { ticketId: "t" } })}`);

    expect(readRequest(Sample)?.input).toEqual({ ticketId: "t" });
    window.history.replaceState(null, "", "/");
  });
});

describe("results", () => {
  it("are written by the page and taken once by the opener", () => {
    const storage = memoryStorage();

    expect(writeResult("r1", { status: "succeeded", value: { reopenedOn: "2026-11-07" } }, storage)).toBe(true);

    expect(takeResult("r1", storage)).toEqual({ status: "succeeded", value: { reopenedOn: "2026-11-07" } });
    expect(takeResult("r1", storage)).toEqual({ status: "cancelled" });
  });

  it("count as cancelled when the page left none (closed with X)", () => {
    expect(takeResult("unknown", memoryStorage())).toEqual({ status: "cancelled" });
  });

  it("count as failed when unreadable", () => {
    const storage = memoryStorage();
    storage.setItem("ogz_dialog_result_r1", "{broken");

    expect(takeResult("r1", storage)).toEqual({ status: "failed", error: "The dialog returned an unreadable result." });
  });

  it("use sessionStorage by default", () => {
    writeResult("r2", { status: "cancelled" });

    expect(sessionStorage.getItem("ogz_dialog_result_r2")).toBe('{"status":"cancelled"}');
    expect(takeResult("r2")).toEqual({ status: "cancelled" });
  });

  it("are not written without storage", () => {
    expect(writeResult("r", { status: "cancelled" }, null)).toBe(false);
    expect(takeResult("r", null)).toEqual({ status: "cancelled" });
  });

  it("treat blocked storage as no storage", () => {
    vi.stubGlobal("sessionStorage", undefined);
    Object.defineProperty(globalThis, "sessionStorage", {
      configurable: true,
      get() {
        throw new Error("blocked");
      },
    });
    try {
      expect(writeResult("r", { status: "cancelled" })).toBe(false);
    } finally {
      vi.unstubAllGlobals();
    }
  });
});

describe("toErrorMessage", () => {
  it.each([
    ["an Error", new Error("boom"), "boom"],
    ["a Web API error object", { errorCode: 1, message: "Not allowed." }, "Not allowed."],
    ["an object without a message", { code: 1 }, "[object Object]"],
    ["text", "offline", "offline"],
  ])("reads %s", (_, error, message) => {
    expect(toErrorMessage(error)).toBe(message);
  });
});
