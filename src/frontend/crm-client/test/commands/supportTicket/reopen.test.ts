import { ogz_support_ticket_statecode as State } from "@ogz/crm-entities";
import { afterEach, describe, expect, it, vi } from "vitest";
import * as SupportTicketCommands from "../../../src/commands/supportTicket/index.js";
import { ReopenTexts } from "../../../src/commands/supportTicket/reopen/reopen.js";
import { getReopenBlocker, ReopenMessages } from "../../../src/commands/supportTicket/reopen/validation.js";
import { ReopenSupportTicketApi } from "../../../src/constants/customApis.js";
import { createFormContextMock, installXrmMock } from "../../mocks/index.js";

const ID = "11111111-0000-0000-0000-000000000001";

describe("getReopenBlocker", () => {
  it("allows a saved, inactive ticket", () => {
    expect(getReopenBlocker({ id: ID, statecode: State.Inactive })).toBeNull();
  });

  it.each([
    ["not saved", { id: null, statecode: State.Inactive }, ReopenMessages.notSaved],
    ["active", { id: ID, statecode: State.Active }, ReopenMessages.notInactive],
    ["without a state", { id: ID, statecode: null }, ReopenMessages.notInactive],
  ])("blocks a ticket that is %s", (_, candidate, message) => {
    expect(getReopenBlocker(candidate)).toBe(message);
  });
});

/** A saved, inactive ticket by default; the dialog page ends with `dialogResult` (null: closed with X). */
function setup(stored: object = { statecode: State.Inactive }, dialogResult: object | null = { status: "succeeded", value: { reopenedOn: "2026-11-07T10:00:00Z" } }) {
  const xrm = installXrmMock();
  const items = new Map<string, string>();
  vi.stubGlobal("sessionStorage", {
    getItem: (key: string) => items.get(key) ?? null,
    setItem: (key: string, value: string) => void items.set(key, value),
    removeItem: (key: string) => void items.delete(key),
  });
  vi.mocked(xrm.WebApi.retrieveRecord).mockResolvedValue(stored);
  vi.mocked(xrm.Navigation.navigateTo).mockImplementation((pageInput) => {
    const { requestId } = JSON.parse((pageInput as { data: string }).data) as { requestId: string };
    if (dialogResult !== null) items.set(`ogz_dialog_result_${requestId}`, JSON.stringify(dialogResult));
    return Promise.resolve() as unknown as Xrm.Async.PromiseLike<never>;
  });
  const formContext = createFormContextMock({ entityName: "ogz_support_ticket", entityId: `{${ID.toUpperCase()}}` });
  return { xrm, formContext };
}

afterEach(() => vi.unstubAllGlobals());

describe("Ogz.Commands.SupportTicket.reopen", () => {
  it("uses the Custom API names shared with the server", () => {
    expect(ReopenSupportTicketApi).toEqual({
      name: "ogz_ReopenSupportTicket",
      boundEntity: "ogz_support_ticket",
      parameters: { reason: "Reason" },
      response: { reopenedOn: "ReopenedOn" },
    });
  });

  it("reads the stored state, opens the Reopen Ticket dialog for the ticket and refreshes when it succeeded", async () => {
    const { xrm, formContext } = setup();

    expect(await SupportTicketCommands.reopen(formContext)).toBe("done");

    expect(xrm.WebApi.retrieveRecord).toHaveBeenCalledWith("ogz_support_ticket", ID, "?$select=statecode");
    const pageInput = vi.mocked(xrm.Navigation.navigateTo).mock.calls[0]![0] as { webresourceName: string; data: string };
    expect(pageInput.webresourceName).toBe("ogz_/pages/reopenTicket.html");
    expect(JSON.parse(pageInput.data).input).toEqual({ ticketId: ID });
    // The dialog page asks and calls the Custom API; the command itself does neither.
    expect(xrm.Navigation.openConfirmDialog).not.toHaveBeenCalled();
    expect(xrm.WebApi.online.execute).not.toHaveBeenCalled();
    expect(formContext.data.refresh).toHaveBeenCalledWith(false);
  });

  it("changes nothing when the dialog was cancelled or closed", async () => {
    const { formContext } = setup(undefined, null);

    expect(await SupportTicketCommands.reopen(formContext)).toBe("cancelled");

    expect(formContext.data.refresh).not.toHaveBeenCalled();
  });

  it("does not open the dialog for an active ticket", async () => {
    const { xrm, formContext } = setup({ statecode: State.Active });

    expect(await SupportTicketCommands.reopen(formContext)).toBe("blocked");

    expect(xrm.Navigation.openAlertDialog).toHaveBeenCalledWith({ text: ReopenMessages.notInactive, title: ReopenTexts.title });
    expect(xrm.Navigation.navigateTo).not.toHaveBeenCalled();
  });

  it("shows the reason when the dialog could not open", async () => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    const { xrm, formContext } = setup();
    vi.mocked(xrm.Navigation.navigateTo).mockRejectedValueOnce({ message: "Web resource not found." });

    expect(await SupportTicketCommands.reopen(formContext)).toBe("failed");

    expect(xrm.Navigation.openErrorDialog).toHaveBeenCalledWith({ message: ReopenTexts.failed, details: "Web resource not found." });
  });
});
