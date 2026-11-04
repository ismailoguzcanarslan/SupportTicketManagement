import { describe, expect, it } from "vitest";
import { onLoad, onSave } from "../../src/forms/account/index.js";
import { createEventContextMock, createFormContextMock, createSaveEventContextMock, simulateUserSave } from "../mocks/index.js";

const PHONE = 3;
const EMAIL = 2;
const REMINDER_ID = "ogz_save_account_phone_recommended";
const REMINDER = "Phone is the preferred contact method, but no phone number is set.";

const accountForm = (preferredContactMethod: number | null, phone: string | null = null) =>
  createFormContextMock({
    entityName: "account",
    attributes: {
      accountcategorycode: { value: 1, attributeType: "optionset" },
      preferredcontactmethodcode: { value: preferredContactMethod, attributeType: "optionset" },
      telephone1: { value: phone },
    },
    tabs: { DETAILS_TAB: { sections: { COMPANY_PROFILE: {} } } },
  });

type FormNotifications = Map<string, { message: string; level: string }>;
const reminders = (formContext: Xrm.FormContext) => (formContext.ui as unknown as { notifications: FormNotifications }).notifications;
const load = (formContext: Xrm.FormContext) => onLoad(createEventContextMock(formContext));
const change = (formContext: Xrm.FormContext, column: string, value: unknown) => {
  const attribute = formContext.getAttribute(column)!;
  attribute.setValue(value as Xrm.Attributes.AttributeValues);
  attribute.fireOnChange();
};

describe("Account form: OnLoad", () => {
  it("applies the rules, and leaves OnSave to the form's event registration", () => {
    const formContext = accountForm(PHONE);

    load(formContext);

    expect(formContext.getAttribute("telephone1")!.getRequiredLevel()).toBe("recommended");
    expect(formContext.data.entity.addOnSave).not.toHaveBeenCalled();
  });

  it("registers one OnChange handler per column, also when onLoad runs again", () => {
    const formContext = accountForm(PHONE);

    load(formContext);
    load(formContext);

    // preferredcontactmethodcode: the required-level rule and the save check share one handler.
    expect(formContext.getAttribute("preferredcontactmethodcode")!.addOnChange).toHaveBeenCalledTimes(2);
    expect(formContext.getAttribute("preferredcontactmethodcode")!.removeOnChange).toHaveBeenCalledOnce();
    expect(formContext.getAttribute("telephone1")!.addOnChange).toHaveBeenCalledTimes(2);
  });
});

describe("Account form: OnSave phone reminder", () => {
  it("reminds the user, without blocking the save or changing data", () => {
    const formContext = accountForm(PHONE);
    const context = createSaveEventContextMock(formContext);

    onSave(context);

    expect(reminders(formContext).get(REMINDER_ID)).toEqual({ message: REMINDER, level: "INFO" });
    expect(context.getEventArgs().preventDefault).not.toHaveBeenCalled();
    expect(formContext.getAttribute("telephone1")!.setValue).not.toHaveBeenCalled();
    expect(formContext.data.save).not.toHaveBeenCalled();
  });

  it("never blocks a save, also when the user saves again with the reminder shown", () => {
    const formContext = accountForm(PHONE);
    load(formContext);

    expect(simulateUserSave(formContext, onSave).saved).toBe(true);
    expect(simulateUserSave(formContext, onSave).saved).toBe(true);
    expect(reminders(formContext).size).toBe(1);
  });

  it.each([
    ["a phone number is set", PHONE, "+90 212 000 00 00"],
    ["phone is not the preferred method", EMAIL, null],
  ])("shows nothing when %s", (_, method, phone) => {
    const formContext = accountForm(method, phone);

    onSave(createSaveEventContextMock(formContext));

    expect(reminders(formContext).size).toBe(0);
  });

  it("clears the reminder as soon as a phone number is entered", () => {
    const formContext = accountForm(PHONE);
    load(formContext);
    onSave(createSaveEventContextMock(formContext));

    change(formContext, "telephone1", "+90 212 000 00 00");

    expect(reminders(formContext).has(REMINDER_ID)).toBe(false);
  });
});
