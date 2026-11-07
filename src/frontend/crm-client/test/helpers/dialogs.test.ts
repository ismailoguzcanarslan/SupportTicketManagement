import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ReopenTicketDialog } from "../../src/dialogs/definitions.js";
import { Dialogs } from "../../src/helpers/dialogs.js";
import { XrmHelper } from "../../src/helpers/XrmHelper.js";
import { installXrmMock } from "../mocks/index.js";

/** sessionStorage of the browser tab (Node has none). */
function installSessionStorage(): Map<string, string> {
  const items = new Map<string, string>();
  vi.stubGlobal("sessionStorage", {
    getItem: (key: string) => items.get(key) ?? null,
    setItem: (key: string, value: string) => void items.set(key, value),
    removeItem: (key: string) => void items.delete(key),
  });
  return items;
}

type PageInput = { webresourceName: string; data: string };

/** Makes navigateTo behave like a page that ends with `result` before its dialog closes. */
function pageEndsWith(xrm: Xrm.XrmStatic, items: Map<string, string>, result: object | null) {
  vi.mocked(xrm.Navigation.navigateTo).mockImplementation((pageInput) => {
    const { requestId } = JSON.parse((pageInput as PageInput).data) as { requestId: string };
    if (result !== null) items.set(`ogz_dialog_result_${requestId}`, JSON.stringify(result));
    return Promise.resolve() as unknown as Xrm.Async.PromiseLike<never>;
  });
}

let items: Map<string, string>;
beforeEach(() => {
  items = installSessionStorage();
});
afterEach(() => vi.unstubAllGlobals());

describe("Dialogs.open", () => {
  it("is part of XrmHelper", () => {
    expect(XrmHelper.Dialogs).toBe(Dialogs);
  });

  it("opens the page's web resource in a centered dialog with the input in the data parameter", async () => {
    const xrm = installXrmMock();

    await Dialogs.open(ReopenTicketDialog, { ticketId: "t1" });

    const [pageInput, options] = vi.mocked(xrm.Navigation.navigateTo).mock.calls[0]!;
    expect(pageInput).toMatchObject({ pageType: "webresource", webresourceName: "ogz_/pages/reopenTicket.html" });
    expect(JSON.parse((pageInput as PageInput).data)).toMatchObject({ v: 1, page: "reopenTicket", input: { ticketId: "t1" } });
    expect(options).toEqual({ target: 2, position: 1, width: 520, height: 420 });
  });

  it("opens a side panel with a title when the definition says so", async () => {
    const xrm = installXrmMock();

    await Dialogs.open({ page: "side", webResource: "ogz_/pages/side.html", position: "side", title: "Details" }, { id: "1" });

    expect(vi.mocked(xrm.Navigation.navigateTo).mock.calls[0]![1]).toEqual({ target: 2, position: 2, title: "Details" });
  });

  it("returns what the page reported, and removes it", async () => {
    const xrm = installXrmMock();
    pageEndsWith(xrm, items, { status: "succeeded", value: { reopenedOn: "2026-11-07T10:00:00Z" } });

    const result = await Dialogs.open(ReopenTicketDialog, { ticketId: "t1" });

    expect(result).toEqual({ status: "succeeded", value: { reopenedOn: "2026-11-07T10:00:00Z" } });
    expect(items.size).toBe(0);
  });

  it("is cancelled when the dialog was closed without a result (X)", async () => {
    const xrm = installXrmMock();
    pageEndsWith(xrm, items, null);

    expect(await Dialogs.open(ReopenTicketDialog, { ticketId: "t1" })).toEqual({ status: "cancelled" });
  });

  it("fails when the dialog cannot open", async () => {
    const xrm = installXrmMock();
    vi.mocked(xrm.Navigation.navigateTo).mockRejectedValueOnce({ message: "Web resource not found." });

    expect(await Dialogs.open(ReopenTicketDialog, { ticketId: "t1" })).toEqual({ status: "failed", error: "Web resource not found." });
  });

  it("fails without opening when the input is too long for the URL", async () => {
    const xrm = installXrmMock();

    const result = await Dialogs.open(ReopenTicketDialog, { ticketId: "x".repeat(5000) });

    expect(result.status).toBe("failed");
    expect(xrm.Navigation.navigateTo).not.toHaveBeenCalled();
  });
});
