import { describe, expect, it } from "vitest";
import {
  FieldDisabledRule,
  FieldNotificationRule,
  FieldVisibilityRule,
  FormRules,
  MinTextLengthRule,
  SaveRules,
  selectRuleSet,
  type FieldTarget,
  type FormRule,
} from "../../src/rules/index.js";
import { createFormContextMock, simulateUserSave } from "../mocks/index.js";

const PHONE = 3;
const EMAIL = 2;
const CONTACT: FieldTarget = { tab: "SUMMARY_TAB", section: "CONTACT", field: "telephone1" };

const form = (method: number | null, formId?: string) =>
  createFormContextMock({
    ...(formId === undefined ? {} : { formId }),
    attributes: { preferredcontactmethodcode: { value: method, attributeType: "optionset" }, telephone1: { value: null } },
    controls: { header_telephone1: { attributeName: "telephone1" } },
    tabs: { SUMMARY_TAB: { sections: { CONTACT: { controls: ["telephone1"] } } } },
  });

type Inspectable = { notifications: Map<string, string>; notificationLevels: Map<string, string> };
const control = (formContext: Xrm.FormContext, name: string) => formContext.getControl(name) as unknown as Inspectable;
const changeMethod = (formContext: Xrm.FormContext, method: number | null) => {
  const attribute = formContext.getAttribute("preferredcontactmethodcode")!;
  attribute.setValue(method);
  attribute.fireOnChange();
};

const phoneHint = (options: Partial<Parameters<typeof FieldNotificationRule>[0]> = {}) =>
  FieldNotificationRule({
    target: CONTACT,
    field: "preferredcontactmethodcode",
    showWhen: PHONE,
    message: "Enter the phone number used for contact.",
    id: "ogz_test_phone_hint",
    ...options,
  });

describe("FieldNotificationRule", () => {
  it("shows a recommendation on the targeted control only, by default", () => {
    const formContext = form(PHONE);

    FormRules.initialize(formContext, [phoneHint()]);

    expect(control(formContext, "telephone1").notifications.get("ogz_test_phone_hint")).toBe("Enter the phone number used for contact.");
    expect(control(formContext, "telephone1").notificationLevels.get("ogz_test_phone_hint")).toBe("RECOMMENDATION");
    expect(control(formContext, "header_telephone1").notifications.size).toBe(0);
  });

  it("is re-evaluated on change: cleared when the condition no longer holds, shown again when it does", () => {
    const formContext = form(PHONE);
    FormRules.initialize(formContext, [phoneHint()]);

    changeMethod(formContext, EMAIL);
    expect(control(formContext, "telephone1").notifications.size).toBe(0);

    changeMethod(formContext, PHONE);
    expect([...control(formContext, "telephone1").notifications.keys()]).toEqual(["ogz_test_phone_hint"]);
  });

  it("registers one OnChange handler per dependency, also when onLoad runs again", () => {
    const formContext = form(PHONE);
    const rules = [phoneHint(), phoneHint({ id: "ogz_test_other", target: "telephone1" })];

    FormRules.initialize(formContext, rules);
    FormRules.initialize(formContext, rules);
    changeMethod(formContext, EMAIL);

    const attribute = formContext.getAttribute("preferredcontactmethodcode")!;
    expect(attribute.addOnChange).toHaveBeenCalledTimes(2);
    expect(attribute.removeOnChange).toHaveBeenCalledOnce();
  });

  it("with level ERROR blocks saving while shown, and stops blocking once the condition clears", () => {
    const formContext = form(PHONE);
    FormRules.initialize(formContext, [phoneHint({ level: "ERROR", target: "telephone1" })]);
    const noSaveRules = () => undefined;

    expect(control(formContext, "header_telephone1").notificationLevels.get("ogz_test_phone_hint")).toBe("ERROR");
    expect(simulateUserSave(formContext, noSaveRules).saved).toBe(false);

    changeMethod(formContext, EMAIL);
    expect(simulateUserSave(formContext, noSaveRules).saved).toBe(true);
  });

  it("a recommendation never blocks saving", () => {
    const formContext = form(PHONE);
    FormRules.initialize(formContext, [phoneHint()]);

    expect(simulateUserSave(formContext, () => undefined).saved).toBe(true);
  });

  it("does nothing, and does not fail, when the target or the source column is missing", () => {
    const formContext = form(PHONE);

    expect(() => FormRules.initialize(formContext, [
      phoneHint({ target: { tab: "SUMMARY_TAB", section: "MISSING", field: "telephone1" } }),
      phoneHint({ field: "missing_column" }),
    ])).not.toThrow();
    expect(control(formContext, "telephone1").notifications.size).toBe(0);
  });

  it("leaves SaveRules notifications on the same control alone", () => {
    const formContext = createFormContextMock({
      attributes: { title: { value: "ab" }, preferredcontactmethodcode: { value: PHONE, attributeType: "optionset" } },
    });
    const titleRule = MinTextLengthRule({ field: "title", minLength: 3 });
    FormRules.initialize(formContext, [
      phoneHint({ target: "title", id: "ogz_test_title_hint" }),
      SaveRules.revalidateOnChange([titleRule]),
    ]);
    simulateUserSave(formContext, (context) => SaveRules.handle(context, [titleRule]));
    expect([...control(formContext, "title").notifications.keys()]).toEqual(["ogz_test_title_hint", "ogz_save_title_min_length"]);

    changeMethod(formContext, EMAIL);

    expect([...control(formContext, "title").notifications.keys()]).toEqual(["ogz_save_title_min_length"]);
  });
});

describe("FieldVisibilityRule and FieldDisabledRule with structured targets", () => {
  it("change only the control in the targeted section, and still accept column names", () => {
    const formContext = form(EMAIL);

    FormRules.initialize(formContext, [
      FieldVisibilityRule({ targets: [CONTACT], field: "preferredcontactmethodcode", visibleWhen: PHONE }),
      FieldDisabledRule({ targets: "header_telephone1", field: "preferredcontactmethodcode", disabledWhen: EMAIL }),
    ]);

    expect(formContext.getControl<Xrm.Controls.StandardControl>("telephone1")!.getVisible()).toBe(false);
    expect(formContext.getControl<Xrm.Controls.StandardControl>("header_telephone1")!.getVisible()).toBe(true);
    expect(formContext.getControl<Xrm.Controls.StandardControl>("header_telephone1")!.getDisabled()).toBe(true);
  });

  it("accept a single structured target", () => {
    const formContext = form(EMAIL);

    FormRules.initialize(formContext, [FieldDisabledRule({ targets: CONTACT, field: "preferredcontactmethodcode", disabledWhen: EMAIL })]);

    expect(formContext.getControl<Xrm.Controls.StandardControl>("telephone1")!.getDisabled()).toBe(true);
    expect(formContext.getControl<Xrm.Controls.StandardControl>("header_telephone1")!.getDisabled()).toBe(false);
  });
});

describe("selectRuleSet", () => {
  const defaultRules: readonly FormRule[] = [phoneHint()];
  const agentRules: readonly FormRule[] = [];
  const AGENT_FORM = "{AB12CD34-0000-0000-0000-000000000001}";

  it("picks the set registered for the shown form, by id in any format", () => {
    const sets = { default: defaultRules, byFormId: { [AGENT_FORM]: agentRules } };

    expect(selectRuleSet(form(PHONE, "ab12cd34-0000-0000-0000-000000000001"), sets)).toBe(agentRules);
  });

  it("falls back to the default set for other forms, forms without a form selector, and no registrations", () => {
    const sets = { default: defaultRules, byFormId: { [AGENT_FORM]: agentRules } };

    expect(selectRuleSet(form(PHONE, "00000000-0000-0000-0000-000000000009"), sets)).toBe(defaultRules);
    expect(selectRuleSet(form(PHONE), sets)).toBe(defaultRules);
    expect(selectRuleSet(form(PHONE, AGENT_FORM), { default: defaultRules })).toBe(defaultRules);
  });
});
