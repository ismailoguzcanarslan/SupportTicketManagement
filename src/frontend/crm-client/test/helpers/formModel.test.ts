import { describe, expect, it, vi } from "vitest";
import { FormModel, XrmHelper } from "../../src/helpers/XrmHelper.js";
import { createFormContextMock } from "../mocks/index.js";

const PRIORITY = [
  { value: 1, text: "Low" },
  { value: 3, text: "Critical" },
];

function form() {
  return createFormContextMock({
    attributes: {
      telephone1: { value: null, requiredLevel: "none" },
      emailaddress1: { value: "help@contoso.com", requiredLevel: "required" },
      priority: { value: 3, attributeType: "optionset", options: PRIORITY },
      parentaccountid: { value: null, attributeType: "lookup", control: { lookup: true } },
    },
    controls: {
      header_telephone1: { attributeName: "telephone1" },
      notes_grid: { controlType: "subgrid", grid: { recordCount: 2 } },
    },
    tabs: { SUMMARY_TAB: { sections: { CONTACT: { controls: ["telephone1", "emailaddress1"] } } } },
  });
}

const control = (formContext: Xrm.FormContext, name: string) => formContext.getControl<Xrm.Controls.StandardControl>(name)!;

describe("FormModel", () => {
  it("is part of XrmHelper", () => {
    expect(XrmHelper.FormModel).toBe(FormModel);
  });

  it("works on a column with chained calls", () => {
    const formContext = form();
    const priority = FormModel(formContext).field<number>("priority");

    expect(priority.exists()).toBe(true);
    expect(priority.target).toBe("priority");
    expect(priority.logicalName()).toBe("priority");
    expect(priority.getValue()).toBe(3);
    expect(priority.getText()).toBe("Critical");

    priority.setValue(1).setRequired(true).setLabel("Urgency").setDisabled(true).setVisible(false).setSubmitMode("always");

    expect(priority.getValue()).toBe(1);
    expect(priority.isDirty()).toBe(true);
    expect(priority.isRequired()).toBe(true);
    expect(priority.getLabel()).toBe("Urgency");
    expect(priority.isDisabled()).toBe(true);
    expect(priority.isVisible()).toBe(false);
    expect(formContext.getAttribute("priority")!.getSubmitMode()).toBe("always");
  });

  it("works on a field of a section, leaving its other controls alone", () => {
    const formContext = form();
    const phone = FormModel(formContext).tab("SUMMARY_TAB").section("CONTACT").field<string>("telephone1");

    phone.setValue("+90 555", { fireOnChange: false }).setRequiredLevel("recommended").setVisible(false).focus();

    expect(phone.target).toEqual({ tab: "SUMMARY_TAB", section: "CONTACT", field: "telephone1" });
    expect(formContext.getAttribute("telephone1")!.getValue()).toBe("+90 555");
    expect(formContext.getAttribute("telephone1")!.getRequiredLevel()).toBe("recommended");
    expect(control(formContext, "telephone1").getVisible()).toBe(false);
    expect(control(formContext, "header_telephone1").getVisible()).toBe(true);
    // focus() goes to a visible control only; the one in the section is hidden.
    expect(control(formContext, "telephone1").setFocus).not.toHaveBeenCalled();
  });

  it("clears, runs OnChange and shows notifications", () => {
    const formContext = form();
    const onChange = vi.fn();
    formContext.getAttribute("emailaddress1")!.addOnChange(onChange);
    const email = FormModel(formContext).field("emailaddress1");

    email.clear().fireOnChange().notify("Check the address.", "email_hint", "RECOMMENDATION");

    expect(email.getValue()).toBeNull();
    expect(onChange).toHaveBeenCalledOnce();
    expect(control(formContext, "emailaddress1").addNotification).toHaveBeenCalledOnce();
    email.clearNotification("email_hint");
    expect(control(formContext, "emailaddress1").clearNotification).toHaveBeenCalledWith("email_hint");
  });

  it("filters choices and lookups", () => {
    const formContext = form();
    const model = FormModel(formContext);

    model.field("priority").setOptions([3]).removeOptions([3]).resetOptions();
    model.field("parentaccountid").setLookupFilter("<filter />", "account").setLookupDefaultView("{VIEW}");

    const priority = control(formContext, "priority") as unknown as Xrm.Controls.OptionSetControl;
    expect(priority.clearOptions).toHaveBeenCalledTimes(2);
    expect(priority.removeOption).toHaveBeenCalledWith(3);
    const lookup = control(formContext, "parentaccountid") as unknown as Xrm.Controls.LookupControl;
    expect(lookup.addPreSearch).toHaveBeenCalledOnce();
    expect(lookup.setDefaultView).toHaveBeenCalledWith("{VIEW}");
  });

  it("works on a section", () => {
    const formContext = form();
    const contact = FormModel(formContext).section("SUMMARY_TAB", "CONTACT");

    expect(contact.exists()).toBe(true);
    expect(contact.tab).toBe("SUMMARY_TAB");
    expect(contact.name).toBe("CONTACT");
    expect(contact.fields()).toEqual(["telephone1", "emailaddress1"]);
    expect(contact.getValues()).toEqual({ telephone1: null, emailaddress1: "help@contoso.com" });

    contact.setRequiredAll(true, ["emailaddress1"]);
    expect(contact.getMissingRequired()).toEqual(["telephone1"]);
    contact.setRequiredAll(false);
    expect(formContext.getAttribute("emailaddress1")!.getRequiredLevel()).toBe("none");

    contact.setVisible(false).setControlsDisabled(true);
    expect(contact.isVisible()).toBe(false);
    expect(control(formContext, "telephone1").getDisabled()).toBe(true);
  });

  it("works on a tab", () => {
    const formContext = form();
    const summary = FormModel(formContext).tab("SUMMARY_TAB");

    summary.collapse();
    expect(formContext.ui.tabs.get("SUMMARY_TAB")!.getDisplayState()).toBe("collapsed");
    summary.expand().focus().setControlsDisabled(true).setVisible(false);

    expect(summary.name).toBe("SUMMARY_TAB");
    expect(summary.exists()).toBe(true);
    expect(summary.isVisible()).toBe(false);
    expect(formContext.ui.tabs.get("SUMMARY_TAB")!.getDisplayState()).toBe("expanded");
    expect(control(formContext, "emailaddress1").getDisabled()).toBe(true);
  });

  it("works on a subgrid", () => {
    const formContext = form();
    const notes = FormModel(formContext).grid("notes_grid");

    expect(notes.name).toBe("notes_grid");
    expect(notes.exists()).toBe(true);
    expect(notes.refresh().getRecordCount()).toBe(2);
  });

  it("tolerates what is not on the form", () => {
    const model = FormModel(form());

    expect(model.formContext).toBeDefined();
    expect(model.field("missing").setValue("x").setRequired(true).setVisible(false).getValue()).toBeNull();
    expect(model.field("missing").exists()).toBe(false);
    expect(model.tab("NOPE").section("NOPE").field("telephone1").exists()).toBe(false);
    expect(model.tab("NOPE").exists()).toBe(false);
    expect(model.section("SUMMARY_TAB", "NOPE").getValues()).toEqual({});
    expect(model.grid("missing").exists()).toBe(false);
  });
});
