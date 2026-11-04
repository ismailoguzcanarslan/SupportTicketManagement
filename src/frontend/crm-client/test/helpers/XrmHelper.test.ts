import { describe, expect, it, vi } from "vitest";
import { XrmHelper } from "../../src/helpers/XrmHelper.js";
import { createFormContextMock, FormType, installXrmMock } from "../mocks/index.js";

describe("XrmHelper.Data", () => {
  it("creates through Xrm.WebApi and returns the id without braces", async () => {
    const xrm = installXrmMock();
    vi.mocked(xrm.WebApi.createRecord).mockResolvedValueOnce({ entityType: "ogz_support_ticket", id: "{ABC-1}" });

    const id = await XrmHelper.Data.create("ogz_support_ticket", { ogz_ticket_title: "Printer offline" });

    expect(id).toBe("abc-1");
    expect(xrm.WebApi.createRecord).toHaveBeenCalledWith("ogz_support_ticket", { ogz_ticket_title: "Printer offline" });
  });

  it("normalizes ids for retrieve, update and delete", async () => {
    const xrm = installXrmMock();

    await XrmHelper.Data.retrieve("account", "{ID-1}", "?$select=name");
    await XrmHelper.Data.update("account", "{ID-1}", { name: "Contoso" });
    await XrmHelper.Data.delete("account", "{ID-1}");

    expect(xrm.WebApi.retrieveRecord).toHaveBeenCalledWith("account", "id-1", "?$select=name");
    expect(xrm.WebApi.updateRecord).toHaveBeenCalledWith("account", "id-1", { name: "Contoso" });
    expect(xrm.WebApi.deleteRecord).toHaveBeenCalledWith("account", "id-1");
  });

  it("passes retrieveMultiple options and propagates errors", async () => {
    const xrm = installXrmMock();
    vi.mocked(xrm.WebApi.retrieveMultipleRecords).mockRejectedValueOnce(new Error("Access denied"));

    await expect(XrmHelper.Data.retrieveMultiple("account", "?$top=5", 5)).rejects.toThrow("Access denied");
    expect(xrm.WebApi.retrieveMultipleRecords).toHaveBeenCalledWith("account", "?$top=5", 5);
  });

  it("fails clearly when Xrm is not available", () => {
    expect(() => XrmHelper.App.getClientUrl()).toThrow("Client API (Xrm) is not available");
  });
});

describe("XrmHelper.Form and App", () => {
  it("reads form id, type and record id", () => {
    const formContext = createFormContextMock({ entityName: "account", entityId: "{REC-1}", formId: "{FORM-1}" });

    expect(XrmHelper.Form.getFormId(formContext)).toBe("form-1");
    expect(XrmHelper.Form.getFormType(formContext)).toBe(FormType.Update);
    expect(XrmHelper.Form.getEntityId(formContext)).toBe("rec-1");
    expect(XrmHelper.Form.getEntityName(formContext)).toBe("account");
  });

  it("returns null ids on a create form without form selector", () => {
    const formContext = createFormContextMock({ formType: FormType.Create });

    expect(XrmHelper.Form.getFormId(formContext)).toBeNull();
    expect(XrmHelper.Form.getEntityId(formContext)).toBeNull();
    expect(XrmHelper.Form.isCreate(formContext)).toBe(true);
  });

  it("saves and refreshes through formContext.data", async () => {
    const formContext = createFormContextMock();

    await XrmHelper.Form.save(formContext);
    await XrmHelper.Form.refresh(formContext);

    expect(formContext.data.save).toHaveBeenCalledWith();
    expect(formContext.data.refresh).toHaveBeenCalledWith(false);
  });

  it("reads app id, name and client URL", async () => {
    installXrmMock();

    expect(await XrmHelper.App.getAppId()).toBe("aaaaaaaa-0000-0000-0000-000000000001");
    expect(await XrmHelper.App.getAppName()).toBe("Support Hub");
    expect(XrmHelper.App.getClientUrl()).toBe("https://test.crm.dynamics.com");
  });
});

describe("XrmHelper.Field, Control and Lookup", () => {
  const form = () =>
    createFormContextMock({
      attributes: {
        name: { value: "Contoso" },
        primarycontactid: { value: [{ id: "{CON-1}", entityType: "contact", name: "Ana" }], attributeType: "lookup" },
      },
      controls: { header_name: { attribute: undefined } },
    });

  it("gets and sets values, and ignores columns that are not on the form", () => {
    const formContext = form();

    expect(XrmHelper.Field.getValue<string>(formContext, "name")).toBe("Contoso");
    expect(XrmHelper.Field.setValue(formContext, "name", "Fabrikam")).toBe(true);
    expect(XrmHelper.Field.getValue(formContext, "name")).toBe("Fabrikam");

    expect(XrmHelper.Field.getValue(formContext, "missing")).toBeNull();
    expect(XrmHelper.Field.setValue(formContext, "missing", "x")).toBe(false);
    expect(XrmHelper.Field.setRequiredLevel(formContext, "missing", "required")).toBe(false);
  });

  it("sets required level and field notifications", () => {
    const formContext = form();

    XrmHelper.Field.setRequiredLevel(formContext, "name", "required");
    XrmHelper.Field.setNotification(formContext, "name", "Required", "n1");

    expect(XrmHelper.Field.getRequiredLevel(formContext, "name")).toBe("required");
    expect(formContext.getControl<Xrm.Controls.StandardControl>("name")!.setNotification).toHaveBeenCalledWith("Required", "n1");
  });

  it("fires OnChange only when asked", () => {
    const formContext = form();
    const attribute = formContext.getAttribute("name")!;

    XrmHelper.Field.setValue(formContext, "name", "A");
    XrmHelper.Field.setValue(formContext, "name", "B", { fireOnChange: true });

    expect(attribute.fireOnChange).toHaveBeenCalledOnce();
  });

  it("changes visibility and disabled state of controls, and reports missing ones", () => {
    const formContext = form();

    expect(XrmHelper.Control.setVisible(formContext, "name", false)).toBe(true);
    expect(XrmHelper.Control.setDisabled(formContext, "header_name", true)).toBe(true);
    expect(XrmHelper.Control.setVisible(formContext, "missing", false)).toBe(false);

    expect(XrmHelper.Control.isVisible(formContext, "name")).toBe(false);
    expect(XrmHelper.Control.isDisabled(formContext, "header_name")).toBe(true);
    expect(XrmHelper.Control.getName(formContext.getControl("name")!)).toBe("name");
  });

  it("reads and sets lookups", () => {
    const formContext = form();

    expect(XrmHelper.Lookup.getId(formContext, "primarycontactid")).toBe("con-1");
    expect(XrmHelper.Lookup.getEntityType(formContext, "primarycontactid")).toBe("contact");
    expect(XrmHelper.Lookup.getName(formContext, "primarycontactid")).toBe("Ana");

    XrmHelper.Lookup.setValue(formContext, "primarycontactid", null);
    expect(XrmHelper.Lookup.getValue(formContext, "primarycontactid")).toBeNull();
    expect(XrmHelper.Lookup.getId(formContext, "missing")).toBeNull();
  });
});

describe("XrmHelper.Tab and Section", () => {
  const form = () =>
    createFormContextMock({
      attributes: { websiteurl: {} },
      tabs: { SUMMARY_TAB: { sections: { DETAILS_SECTION: { controls: ["websiteurl"] } } } },
    });

  it("shows and hides tabs and sections", () => {
    const formContext = form();

    expect(XrmHelper.Tab.setVisible(formContext, "SUMMARY_TAB", false)).toBe(true);
    expect(XrmHelper.Section.setVisible(formContext, "SUMMARY_TAB", "DETAILS_SECTION", false)).toBe(true);

    expect(XrmHelper.Tab.get(formContext, "SUMMARY_TAB")!.getVisible()).toBe(false);
    expect(XrmHelper.Section.get(formContext, "SUMMARY_TAB", "DETAILS_SECTION")!.getVisible()).toBe(false);
  });

  it("disables the controls of a section", () => {
    const formContext = form();

    XrmHelper.Section.setControlsDisabled(formContext, "SUMMARY_TAB", "DETAILS_SECTION", true);

    expect(XrmHelper.Control.isDisabled(formContext, "websiteurl")).toBe(true);
  });

  it("ignores missing tabs and sections", () => {
    const formContext = form();

    expect(XrmHelper.Tab.setVisible(formContext, "NOPE", false)).toBe(false);
    expect(XrmHelper.Section.setVisible(formContext, "SUMMARY_TAB", "NOPE", false)).toBe(false);
    expect(XrmHelper.Section.setVisible(formContext, "NOPE", "DETAILS_SECTION", false)).toBe(false);
  });
});

describe("XrmHelper.UI", () => {
  it("opens alert and confirm dialogs", async () => {
    const xrm = installXrmMock();
    vi.mocked(xrm.Navigation.openConfirmDialog).mockResolvedValueOnce({ confirmed: true });

    await XrmHelper.UI.showAlert("Saved", "Info");
    const confirmed = await XrmHelper.UI.showConfirm("Close ticket?");

    expect(xrm.Navigation.openAlertDialog).toHaveBeenCalledWith({ text: "Saved", title: "Info" });
    expect(xrm.Navigation.openConfirmDialog).toHaveBeenCalledWith({ text: "Close ticket?" });
    expect(confirmed).toBe(true);
  });

  it("sets and clears form notifications", () => {
    const formContext = createFormContextMock();

    XrmHelper.UI.setFormNotification(formContext, "Check phone", "WARNING", "n1");
    XrmHelper.UI.clearFormNotification(formContext, "n1");

    expect(formContext.ui.setFormNotification).toHaveBeenCalledWith("Check phone", "WARNING", "n1");
    expect(formContext.ui.clearFormNotification).toHaveBeenCalledWith("n1");
  });
});
