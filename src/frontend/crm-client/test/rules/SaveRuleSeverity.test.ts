import { describe, expect, it, vi } from "vitest";
import { FormRules, RequiredWhenRule, SaveRules, type SaveRule } from "../../src/rules/index.js";
import { createFormContextMock, createSaveEventContextMock, simulateUserSave } from "../mocks/index.js";

const PHONE = 3;
const EMAIL = 2;
const ID = "ogz_save_phone_reminder";

const form = (method: number | null, phone: string | null = null) =>
  createFormContextMock({
    attributes: {
      preferredcontactmethodcode: { value: method, attributeType: "optionset" },
      telephone1: { value: phone },
    },
  });

const phoneRule = (severity?: "error" | "info") =>
  RequiredWhenRule({
    field: "telephone1",
    whenField: "preferredcontactmethodcode",
    when: PHONE,
    message: "Phone is preferred, but no number is set.",
    id: "phone_reminder",
    ...(severity === undefined ? {} : { severity }),
  });

type Inspectable = { notifications: Map<string, string>; notificationLevels: Map<string, string> };
const phoneControl = (formContext: Xrm.FormContext) => formContext.getControl("telephone1") as unknown as Inspectable;
const formNotifications = (formContext: Xrm.FormContext) =>
  (formContext.ui as unknown as { notifications: Map<string, { message: string; level: string }> }).notifications;
const save = (formContext: Xrm.FormContext, rules: readonly SaveRule[]) => {
  const context = createSaveEventContextMock(formContext);
  return { allowed: SaveRules.handle(context, rules), args: context.getEventArgs() };
};
const change = (formContext: Xrm.FormContext, column: string, value: unknown) => {
  const attribute = formContext.getAttribute(column)!;
  attribute.setValue(value as Xrm.Attributes.AttributeValues);
  attribute.fireOnChange();
};

describe("RequiredWhenRule", () => {
  it.each([
    ["the condition holds and the field is empty", PHONE, null, false],
    ["the condition holds and the field is filled", PHONE, "+90 212 000 00 00", true],
    ["the condition does not hold", EMAIL, null, true],
    ["the condition column is empty", null, null, true],
  ])("when %s, valid is %s", (_, method, phone, isValid) => {
    expect(phoneRule().validate(form(method, phone)).isValid).toBe(isValid);
  });

  it("reports the message on the field", () => {
    expect(phoneRule().validate(form(PHONE)).errors).toEqual([{ field: "telephone1", message: "Phone is preferred, but no number is set." }]);
  });

  it("passes when either column is not on the form", () => {
    const onlyMethod = createFormContextMock({ attributes: { preferredcontactmethodcode: { value: PHONE, attributeType: "optionset" } } });

    expect(phoneRule().validate(onlyMethod).isValid).toBe(true);
    expect(phoneRule().validate(createFormContextMock({ attributes: { telephone1: {} } })).isValid).toBe(true);
  });

  it("declares both columns, a default id and no severity unless given", () => {
    const rule = RequiredWhenRule({ field: "telephone1", whenField: "preferredcontactmethodcode", when: PHONE, message: "x" });

    expect(rule.fields).toEqual(["telephone1", "preferredcontactmethodcode"]);
    expect(rule.id).toBe("telephone1_required_when_preferredcontactmethodcode");
    expect(rule.severity).toBeUndefined();
    expect(phoneRule("info").severity).toBe("info");
  });
});

describe("SaveRules severity", () => {
  it("an error (the default) blocks the save and uses setNotification on the field", () => {
    const formContext = form(PHONE);

    const { allowed, args } = save(formContext, [phoneRule()]);

    expect(allowed).toBe(false);
    expect(args.preventDefault).toHaveBeenCalledOnce();
    expect(formNotifications(formContext).get(ID)!.level).toBe("ERROR");
    expect(phoneControl(formContext).notificationLevels.get(ID)).toBe("ERROR");
  });

  it("info shows an INFO notification and a RECOMMENDATION, and lets the save go on", () => {
    const formContext = form(PHONE);

    const { allowed, args } = save(formContext, [phoneRule("info")]);

    expect(allowed).toBe(true);
    expect(args.preventDefault).not.toHaveBeenCalled();
    expect(formNotifications(formContext).get(ID)).toEqual({ message: "Phone is preferred, but no number is set.", level: "INFO" });
    expect(phoneControl(formContext).notificationLevels.get(ID)).toBe("RECOMMENDATION");
    expect(formContext.getControl<Xrm.Controls.StandardControl>("telephone1")!.setNotification).not.toHaveBeenCalled();
  });

  it("info never blocks the next save either (its field message is not a save-blocking error)", () => {
    const formContext = form(PHONE);
    const rules = [phoneRule("info")];

    expect(simulateUserSave(formContext, (context) => SaveRules.handle(context, rules))).toEqual({ handlerRan: true, saved: true });
    expect(simulateUserSave(formContext, (context) => SaveRules.handle(context, rules))).toEqual({ handlerRan: true, saved: true });
    expect(formNotifications(formContext).size).toBe(1);
  });

  it("info without a field shows only the form notification", () => {
    const formContext = form(PHONE);
    const formOnly: SaveRule = { id: "form_tip", severity: "info", validate: () => ({ isValid: false, errors: [{ message: "Check the account." }] }) };

    const { allowed } = save(formContext, [formOnly]);

    expect(allowed).toBe(true);
    expect(formNotifications(formContext).get("ogz_save_form_tip")).toEqual({ message: "Check the account.", level: "INFO" });
    expect(phoneControl(formContext).notifications.size).toBe(0);
  });

  it("only errors make the outcome invalid; info messages are still reported", () => {
    const outcome = SaveRules.validate(form(PHONE), [phoneRule("info")]);

    expect(outcome.isValid).toBe(true);
    expect(outcome.errors.map((e) => e.severity)).toEqual(["info"]);
    expect(SaveRules.validate(form(PHONE), [phoneRule("info"), phoneRule("error")]).isValid).toBe(false);
  });

  it("a rule of severity info that throws still blocks the save", () => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    const broken: SaveRule = { id: "broken_info", severity: "info", validate: () => { throw new Error("boom"); } };

    const { allowed } = save(form(PHONE), [broken]);

    expect(allowed).toBe(false);
  });

  it("clears an info message once fixed, and keeps it while the rule still fails", () => {
    const formContext = form(PHONE);
    const rules = [phoneRule("info")];
    FormRules.initialize(formContext, [SaveRules.revalidateOnChange(rules)]);
    save(formContext, rules);

    change(formContext, "telephone1", "");
    expect(formNotifications(formContext).has(ID)).toBe(true);

    change(formContext, "preferredcontactmethodcode", EMAIL);
    expect(formNotifications(formContext).has(ID)).toBe(false);
    expect(phoneControl(formContext).notifications.size).toBe(0);
  });
});
