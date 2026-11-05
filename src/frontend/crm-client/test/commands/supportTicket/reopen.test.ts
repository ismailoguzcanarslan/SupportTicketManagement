import { ogz_support_ticket_statecode as State } from "@ogz/crm-entities";
import { describe, expect, it, vi } from "vitest";
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

function setup(stored: object = { statecode: State.Inactive }) {
  const xrm = installXrmMock();
  vi.mocked(xrm.WebApi.retrieveRecord).mockResolvedValue(stored);
  vi.mocked(xrm.Navigation.openConfirmDialog).mockResolvedValue({ confirmed: true });
  const formContext = createFormContextMock({ entityName: "ogz_support_ticket", entityId: `{${ID.toUpperCase()}}` });
  return { xrm, formContext };
}

describe("Ogz.Commands.SupportTicket.reopen", () => {
  it("uses the Custom API names shared with the server", () => {
    expect(ReopenSupportTicketApi).toEqual({
      name: "ogz_ReopenSupportTicket",
      boundEntity: "ogz_support_ticket",
      parameters: { reason: "Reason" },
      response: { reopenedOn: "ReopenedOn" },
    });
  });

  it("reads the stored state, asks, calls the Custom API on the ticket and refreshes", async () => {
    const { xrm, formContext } = setup();

    expect(await SupportTicketCommands.reopen(formContext)).toBe("done");

    expect(xrm.WebApi.retrieveRecord).toHaveBeenCalledWith("ogz_support_ticket", ID, "?$select=statecode");
    expect(xrm.Navigation.openConfirmDialog).toHaveBeenCalledWith({ text: ReopenTexts.confirm, title: ReopenTexts.title });
    const request = vi.mocked(xrm.WebApi.online.execute).mock.calls[0]![0] as { entity: unknown; getMetadata(): { operationName: string } };
    expect(request.entity).toEqual({ entityType: "ogz_support_ticket", id: ID });
    expect(request.getMetadata().operationName).toBe("ogz_ReopenSupportTicket");
    expect(xrm.WebApi.updateRecord).not.toHaveBeenCalled();
    expect(formContext.data.refresh).toHaveBeenCalledWith(false);
  });

  it("does not call the Custom API for an active ticket", async () => {
    const { xrm, formContext } = setup({ statecode: State.Active });

    expect(await SupportTicketCommands.reopen(formContext)).toBe("blocked");

    expect(xrm.Navigation.openAlertDialog).toHaveBeenCalledWith({ text: ReopenMessages.notInactive, title: ReopenTexts.title });
    expect(xrm.WebApi.online.execute).not.toHaveBeenCalled();
  });

  it("shows the server's reason when the Custom API rejects the call", async () => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    const { xrm, formContext } = setup();
    vi.mocked(xrm.WebApi.online.execute).mockRejectedValueOnce({ message: "Only inactive tickets can be reopened." });

    expect(await SupportTicketCommands.reopen(formContext)).toBe("failed");

    expect(xrm.Navigation.openErrorDialog).toHaveBeenCalledWith({
      message: ReopenTexts.failed,
      details: "Only inactive tickets can be reopened.",
    });
  });
});
