// Failure paths and edge cases of the public XrmHelper API, complementing XrmHelper.test.ts.

import { describe, expect, it, vi } from "vitest";
import { XrmHelper } from "../../src/helpers/XrmHelper.js";
import { createFormContextMock, createGlobalContextMock, createXrmMock, installXrmMock } from "../mocks/index.js";

describe("Data", () => {
  it("returns the retrieved record and the retrieveMultiple result", async () => {
    const xrm = installXrmMock();
    vi.mocked(xrm.WebApi.retrieveRecord).mockResolvedValueOnce({ name: "Contoso" });
    vi.mocked(xrm.WebApi.retrieveMultipleRecords).mockResolvedValueOnce({ entities: [{ name: "A" }, { name: "B" }] } as Xrm.RetrieveMultipleResult);

    expect(await XrmHelper.Data.retrieve("account", "id-1")).toEqual({ name: "Contoso" });
    expect((await XrmHelper.Data.retrieveMultiple("account")).entities).toHaveLength(2);
    expect(xrm.WebApi.retrieveMultipleRecords).toHaveBeenCalledWith("account", undefined, undefined);
  });

  it.each(["create", "update", "delete", "retrieve"] as const)("propagates Web API errors from %s", async (operation) => {
    const xrm = installXrmMock();
    const error = { errorCode: -2147220969, message: "Record not found" };
    vi.mocked(xrm.WebApi.createRecord).mockRejectedValueOnce(error);
    vi.mocked(xrm.WebApi.updateRecord).mockRejectedValueOnce(error);
    vi.mocked(xrm.WebApi.deleteRecord).mockRejectedValueOnce(error);
    vi.mocked(xrm.WebApi.retrieveRecord).mockRejectedValueOnce(error);

    const call = {
      create: () => XrmHelper.Data.create("account", {}),
      update: () => XrmHelper.Data.update("account", "id", {}),
      delete: () => XrmHelper.Data.delete("account", "id"),
      retrieve: () => XrmHelper.Data.retrieve("account", "id"),
    }[operation];

    await expect(call()).rejects.toEqual(error);
  });
});

describe("Form", () => {
  it("refreshes with save when asked and passes save options through", async () => {
    const formContext = createFormContextMock();
    const options = { saveMode: 70 } as Xrm.SaveOptions;

    await XrmHelper.Form.refresh(formContext, true);
    await XrmHelper.Form.save(formContext, options);

    expect(formContext.data.refresh).toHaveBeenCalledWith(true);
    expect(formContext.data.save).toHaveBeenCalledWith(options);
  });

  it("rejects when saving fails", async () => {
    const formContext = createFormContextMock();
    vi.mocked(formContext.data.save).mockRejectedValueOnce(new Error("Required fields missing"));

    await expect(XrmHelper.Form.save(formContext)).rejects.toThrow("Required fields missing");
  });
});

describe("Field", () => {
  it("clears notifications on all controls of a column", () => {
    const formContext = createFormContextMock({ attributes: { name: {} } });

    XrmHelper.Field.setNotification(formContext, "name", "Required", "n1");
    XrmHelper.Field.clearNotification(formContext, "name", "n1");

    expect(formContext.getControl<Xrm.Controls.StandardControl>("name")!.clearNotification).toHaveBeenCalledWith("n1");
  });

  it("reports missing columns for notifications, required level and exists", () => {
    const formContext = createFormContextMock();

    expect(XrmHelper.Field.setNotification(formContext, "missing", "m", "n1")).toBe(false);
    expect(XrmHelper.Field.clearNotification(formContext, "missing")).toBe(false);
    expect(XrmHelper.Field.getRequiredLevel(formContext, "missing")).toBeNull();
    expect(XrmHelper.Field.exists(formContext, "missing")).toBe(false);
    expect(XrmHelper.Field.getControls(formContext, "missing")).toEqual([]);
  });

  it("returns the attribute name", () => {
    const formContext = createFormContextMock({ attributes: { name: {} } });

    expect(XrmHelper.Field.getName(XrmHelper.Field.get(formContext, "name")!)).toBe("name");
  });
});

describe("Control", () => {
  it("reports missing controls for every operation", () => {
    const formContext = createFormContextMock();

    expect(XrmHelper.Control.setDisabled(formContext, "missing", true)).toBe(false);
    expect(XrmHelper.Control.isVisible(formContext, "missing")).toBeNull();
    expect(XrmHelper.Control.isDisabled(formContext, "missing")).toBeNull();
    expect(XrmHelper.Control.get(formContext, "missing")).toBeNull();
  });
});

describe("Lookup", () => {
  it("returns null for an empty lookup", () => {
    const formContext = createFormContextMock({ attributes: { parentaccountid: { value: null, attributeType: "lookup" } } });

    expect(XrmHelper.Lookup.getValue(formContext, "parentaccountid")).toBeNull();
    expect(XrmHelper.Lookup.getEntityType(formContext, "parentaccountid")).toBeNull();
    expect(XrmHelper.Lookup.getName(formContext, "parentaccountid")).toBeNull();
  });

  it("returns null for an empty array value", () => {
    const formContext = createFormContextMock({ attributes: { parentaccountid: { value: [], attributeType: "lookup" } } });

    expect(XrmHelper.Lookup.getId(formContext, "parentaccountid")).toBeNull();
  });

  it("sets a lookup with a normalized id and reports missing columns", () => {
    const formContext = createFormContextMock({ attributes: { parentaccountid: { attributeType: "lookup" } } });

    expect(XrmHelper.Lookup.setValue(formContext, "parentaccountid", { id: "{ABC}", entityType: "account" })).toBe(true);
    expect(XrmHelper.Lookup.getValue(formContext, "parentaccountid")).toEqual({ id: "abc", entityType: "account", name: "" });
    expect(XrmHelper.Lookup.setValue(formContext, "missing", null)).toBe(false);
  });
});

describe("Tab", () => {
  it("disables all controls of a tab and reports missing tabs", () => {
    const formContext = createFormContextMock({
      attributes: { name: {}, websiteurl: {} },
      tabs: { SUMMARY_TAB: { sections: { A: { controls: ["name"] }, B: { controls: ["websiteurl"] } } } },
    });

    expect(XrmHelper.Tab.setControlsDisabled(formContext, "SUMMARY_TAB", true)).toBe(true);
    expect(XrmHelper.Control.isDisabled(formContext, "name")).toBe(true);
    expect(XrmHelper.Control.isDisabled(formContext, "websiteurl")).toBe(true);
    expect(XrmHelper.Tab.setControlsDisabled(formContext, "NOPE", true)).toBe(false);
    expect(XrmHelper.Section.setControlsDisabled(formContext, "SUMMARY_TAB", "NOPE", true)).toBe(false);
  });
});

describe("Event", () => {
  it("runs a registered OnSave handler with the save context", () => {
    const formContext = createFormContextMock();
    const onSave = vi.fn();
    XrmHelper.Event.registerOnSave(formContext, onSave);

    const handlers = (formContext.data.entity as unknown as { saveHandlers: Xrm.Events.SaveEventHandler[] }).saveHandlers;
    handlers.forEach((handler) => handler({} as Xrm.Events.SaveEventContext));

    expect(onSave).toHaveBeenCalledOnce();
  });

  it("ignores unregistering handlers that were never registered", () => {
    const formContext = createFormContextMock({ attributes: { name: {} } });

    XrmHelper.Event.unregisterOnSave(formContext, vi.fn());
    XrmHelper.Event.unregisterOnChange(formContext, "name", vi.fn());
    XrmHelper.Event.unregisterOnChange(formContext, "missing", vi.fn());

    expect(formContext.data.entity.removeOnSave).not.toHaveBeenCalled();
    expect(formContext.getAttribute("name")!.removeOnChange).not.toHaveBeenCalled();
  });
});

describe("UI", () => {
  it("returns false when the user declines a confirmation", async () => {
    installXrmMock();

    expect(await XrmHelper.UI.showConfirm("Close?", "Confirm")).toBe(false);
  });

  it("opens error dialogs and shows/hides the progress indicator", async () => {
    const xrm = installXrmMock();

    await XrmHelper.UI.showError("Failed", "details");
    await XrmHelper.UI.showError("Failed");
    await XrmHelper.UI.showAlert("Saved");
    XrmHelper.UI.showProgress("Working...");
    XrmHelper.UI.hideProgress();

    expect(xrm.Navigation.openErrorDialog).toHaveBeenCalledWith({ message: "Failed", details: "details" });
    expect(xrm.Navigation.openErrorDialog).toHaveBeenCalledWith({ message: "Failed" });
    expect(xrm.Navigation.openAlertDialog).toHaveBeenCalledWith({ text: "Saved" });
    expect(xrm.Utility.showProgressIndicator).toHaveBeenCalledWith("Working...");
    expect(xrm.Utility.closeProgressIndicator).toHaveBeenCalledOnce();
  });

  it("propagates dialog failures", async () => {
    const xrm = installXrmMock();
    vi.mocked(xrm.Navigation.openAlertDialog).mockRejectedValueOnce(new Error("dialog failed"));

    await expect(XrmHelper.UI.showAlert("x")).rejects.toThrow("dialog failed");
  });
});

describe("App", () => {
  it("falls back to the unique name, then null, when the app has no display name", async () => {
    installXrmMock(createXrmMock(createGlobalContextMock({ appProperties: { uniqueName: "ogz_SupportHub" } })));
    expect(await XrmHelper.App.getAppName()).toBe("ogz_SupportHub");
    expect(await XrmHelper.App.getAppId()).toBeNull();

    installXrmMock(createXrmMock(createGlobalContextMock({ appProperties: {} })));
    expect(await XrmHelper.App.getAppName()).toBeNull();
  });

  it("propagates failures reading the app properties", async () => {
    const globalContext = createGlobalContextMock();
    vi.mocked(globalContext.getCurrentAppProperties).mockRejectedValueOnce(new Error("not in an app"));
    installXrmMock(createXrmMock(globalContext));

    await expect(XrmHelper.App.getAppId()).rejects.toThrow("not in an app");
  });
});
