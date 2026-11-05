import { ogz_support_ticket_statecode as State, ogz_support_ticket_statuscode as Status } from "@ogz/crm-entities";
import { describe, expect, it, vi } from "vitest";
import * as SupportTicketCommands from "../../../src/commands/supportTicket/index.js";
import { RESOLVED, ResolveTexts } from "../../../src/commands/supportTicket/resolve/resolve.js";
import { getResolveBlocker, ResolveMessages } from "../../../src/commands/supportTicket/resolve/validation.js";
import { createFormContextMock, installXrmMock } from "../../mocks/index.js";

const ID = "11111111-0000-0000-0000-000000000001";
const ACTIVE_WITH_DESCRIPTION = { statecode: State.Active, ogz_description: "Printer is offline since Monday." };

describe("getResolveBlocker", () => {
  const ticket = { id: ID, statecode: State.Active, description: "Printer is offline." };

  it("allows an active, saved ticket with a description", () => {
    expect(getResolveBlocker(ticket)).toBeNull();
  });

  it.each([
    ["not saved", { ...ticket, id: null }, ResolveMessages.notSaved],
    ["inactive", { ...ticket, statecode: State.Inactive }, ResolveMessages.notActive],
    ["without a state", { ...ticket, statecode: null }, ResolveMessages.notActive],
    ["without a description", { ...ticket, description: null }, ResolveMessages.noDescription],
    ["with an empty description", { ...ticket, description: "" }, ResolveMessages.noDescription],
    ["with only spaces as description", { ...ticket, description: "   " }, ResolveMessages.noDescription],
  ])("blocks a ticket that is %s", (_, candidate, message) => {
    expect(getResolveBlocker(candidate)).toBe(message);
  });

  it("reports the first problem only", () => {
    expect(getResolveBlocker({ id: null, statecode: State.Inactive, description: null })).toBe(ResolveMessages.notSaved);
    expect(getResolveBlocker({ id: ID, statecode: State.Inactive, description: null })).toBe(ResolveMessages.notActive);
  });
});

/** A saved Support Ticket form, Dataverse returning `stored` and the user answering `confirmed`. */
function setup(options: { stored?: object; confirmed?: boolean; entityId?: string; dirty?: boolean } = {}) {
  const xrm = installXrmMock();
  vi.mocked(xrm.WebApi.retrieveRecord).mockResolvedValue(options.stored ?? ACTIVE_WITH_DESCRIPTION);
  vi.mocked(xrm.Navigation.openConfirmDialog).mockResolvedValue({ confirmed: options.confirmed ?? true });
  const formContext = createFormContextMock({ entityName: "ogz_support_ticket", entityId: options.entityId ?? `{${ID.toUpperCase()}}` });
  vi.mocked(formContext.data.entity.getIsDirty).mockReturnValue(options.dirty ?? false);
  return { xrm, formContext };
}

describe("Ogz.Commands.SupportTicket.resolve", () => {
  it("exposes the Support Ticket commands", () => {
    expect(Object.keys(SupportTicketCommands)).toEqual(["resolve"]);
  });

  it("resolves: reads the stored record, asks, writes statecode and statuscode together, refreshes", async () => {
    const { xrm, formContext } = setup();

    expect(await SupportTicketCommands.resolve(formContext)).toBe("resolved");

    expect(xrm.WebApi.retrieveRecord).toHaveBeenCalledWith("ogz_support_ticket", ID, "?$select=statecode,ogz_description");
    expect(xrm.Navigation.openConfirmDialog).toHaveBeenCalledWith({ text: ResolveTexts.confirm, title: ResolveTexts.confirmTitle });
    expect(xrm.WebApi.updateRecord).toHaveBeenCalledWith("ogz_support_ticket", ID, { statecode: 1, statuscode: 2 });
    expect(formContext.data.refresh).toHaveBeenCalledWith(false);
    expect(xrm.Utility.showProgressIndicator).toHaveBeenCalledWith(ResolveTexts.progress);
    expect(xrm.Utility.closeProgressIndicator).toHaveBeenCalledOnce();
  });

  it("writes the Inactive state with the Resolved status reason", () => {
    expect(RESOLVED).toEqual({ statecode: State.Inactive, statuscode: Status.Resolved });
  });

  it("stops on a new, unsaved ticket without calling the Web API", async () => {
    const { xrm, formContext } = setup({ entityId: "" });

    expect(await SupportTicketCommands.resolve(formContext)).toBe("blocked");

    expect(xrm.Navigation.openAlertDialog).toHaveBeenCalledWith({ text: ResolveMessages.notSaved, title: ResolveTexts.confirmTitle });
    expect(xrm.WebApi.retrieveRecord).not.toHaveBeenCalled();
  });

  it("stops while the form has unsaved changes", async () => {
    const { xrm, formContext } = setup({ dirty: true });

    expect(await SupportTicketCommands.resolve(formContext)).toBe("blocked");

    expect(xrm.Navigation.openAlertDialog).toHaveBeenCalledWith({ text: ResolveTexts.unsavedChanges, title: ResolveTexts.confirmTitle });
    expect(xrm.WebApi.retrieveRecord).not.toHaveBeenCalled();
  });

  it.each([
    ["inactive", { statecode: State.Inactive, ogz_description: "Done." }, ResolveMessages.notActive],
    ["without a description", { statecode: State.Active, ogz_description: null }, ResolveMessages.noDescription],
  ])("stops on a stored ticket that is %s, before asking", async (_, stored, message) => {
    const { xrm, formContext } = setup({ stored });

    expect(await SupportTicketCommands.resolve(formContext)).toBe("blocked");

    expect(xrm.Navigation.openAlertDialog).toHaveBeenCalledWith({ text: message, title: ResolveTexts.confirmTitle });
    expect(xrm.Navigation.openConfirmDialog).not.toHaveBeenCalled();
    expect(xrm.WebApi.updateRecord).not.toHaveBeenCalled();
  });

  it("changes nothing when the user cancels", async () => {
    const { xrm, formContext } = setup({ confirmed: false });

    expect(await SupportTicketCommands.resolve(formContext)).toBe("cancelled");

    expect(xrm.WebApi.updateRecord).not.toHaveBeenCalled();
    expect(formContext.data.refresh).not.toHaveBeenCalled();
  });

  it("shows the error and closes the progress indicator when the update fails", async () => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    const { xrm, formContext } = setup();
    vi.mocked(xrm.WebApi.updateRecord).mockRejectedValueOnce(new Error("Status transition not allowed."));

    expect(await SupportTicketCommands.resolve(formContext)).toBe("failed");

    expect(xrm.Utility.closeProgressIndicator).toHaveBeenCalledOnce();
    expect(xrm.Navigation.openErrorDialog).toHaveBeenCalledWith({ message: ResolveTexts.failed, details: "Status transition not allowed." });
    expect(formContext.data.refresh).not.toHaveBeenCalled();
  });

  it("shows the error when the record cannot be read", async () => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    const { xrm, formContext } = setup();
    vi.mocked(xrm.WebApi.retrieveRecord).mockRejectedValueOnce("Network error");

    expect(await SupportTicketCommands.resolve(formContext)).toBe("failed");

    expect(xrm.Navigation.openErrorDialog).toHaveBeenCalledWith({ message: ResolveTexts.failed, details: "Network error" });
    expect(xrm.WebApi.updateRecord).not.toHaveBeenCalled();
  });
});
