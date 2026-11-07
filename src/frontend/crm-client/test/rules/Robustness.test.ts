import { afterEach, describe, expect, it, vi } from "vitest";
import { Diagnostics } from "../../src/helpers/diagnostics.js";
import { Event } from "../../src/helpers/event.js";
import {
  FieldDisabledRule,
  FieldNotificationRule,
  FieldRequiredRule,
  FieldVisibilityRule,
  FormNotificationRule,
  FormRules,
  invalid,
  SaveMode,
  SaveRules,
  SectionSpecificVisibilityRule,
  valid,
  type SaveRule,
} from "../../src/rules/index.js";
import { createFormContextMock, createSaveEventContextMock, fireDataLoad, FormType } from "../mocks/index.js";

afterEach(() => Diagnostics.setEnabled(undefined));

const control = (formContext: Xrm.FormContext, name: string) => formContext.getControl<Xrm.Controls.StandardControl>(name)!;
const change = (formContext: Xrm.FormContext, column: string, value: unknown) => {
  const attribute = formContext.getAttribute(column)!;
  attribute.setValue(value as Xrm.Attributes.AttributeValues);
  attribute.fireOnChange();
};

describe("Diagnostics", () => {
  it("is off by default and writes nothing", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});

    Diagnostics.warn("nothing to see");

    expect(Diagnostics.isEnabled()).toBe(false);
    expect(warn).not.toHaveBeenCalled();
  });

  it("follows the localStorage switch", () => {
    vi.stubGlobal("localStorage", { getItem: (key: string) => (key === "ogz_diagnostics" ? "1" : null) });
    try {
      expect(Diagnostics.isEnabled()).toBe(true);
    } finally {
      vi.unstubAllGlobals();
    }
  });

  it("stays off when localStorage is blocked", () => {
    vi.stubGlobal("localStorage", {
      getItem: () => {
        throw new Error("blocked");
      },
    });
    try {
      expect(Diagnostics.isEnabled()).toBe(false);
    } finally {
      vi.unstubAllGlobals();
    }
  });

  it("writes each message once when on", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    Diagnostics.setEnabled(true);

    Diagnostics.warn("same");
    Diagnostics.warn("same");
    expect(Diagnostics.reportMissing(true, "x", "rule")).toBe(true);

    expect(warn).toHaveBeenCalledExactlyOnceWith("[ogz] same");
  });

  it("names what a rule skipped because it is not on the form", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    Diagnostics.setEnabled(true);
    const formContext = createFormContextMock({ attributes: { status: { value: 1 } } });

    FormRules.initialize(formContext, [
      FieldVisibilityRule({ targets: ["phone", { tab: "T", section: "S", field: "phone" }], field: "status", visibleWhen: 1 }),
      FieldRequiredRule({ targets: "phone", field: "status", requiredWhen: 1 }),
      FieldDisabledRule({ targets: "phone", field: "status", disabledWhen: 1 }),
      FieldNotificationRule({ target: "phone", field: "status", showWhen: 1, message: "m", id: "n1" }),
      SectionSpecificVisibilityRule({ tab: "T", section: "S", field: "status", visibleWhen: 1 }),
      FormNotificationRule({ field: "missing_source", showWhen: 1, message: "m", id: "n2" }),
    ]);

    const messages = warn.mock.calls.map(([message]) => message as string);
    expect(messages).toEqual([
      "[ogz] FieldVisibilityRule(status): target 'phone' is not on the form; skipped.",
      "[ogz] FieldVisibilityRule(status): target 'T/S/phone' is not on the form; skipped.",
      "[ogz] FieldRequiredRule(status): target 'phone' is not on the form; skipped.",
      "[ogz] FieldDisabledRule(status): target 'phone' is not on the form; skipped.",
      "[ogz] FieldNotificationRule(n1): target 'phone' is not on the form; skipped.",
      "[ogz] SectionSpecificVisibilityRule(status): section 'T/S' is not on the form; skipped.",
      "[ogz] FormNotificationRule(n2): source column 'missing_source' is not on the form; skipped.",
    ]);
  });
});

describe("FieldDisabledRule keeps what the form designer locked", () => {
  const rule = () => FieldDisabledRule({ targets: "phone", field: "status", disabledWhen: 2 });

  it("locks on a match and unlocks a field that was editable", () => {
    const formContext = createFormContextMock({ attributes: { status: { value: 2 }, phone: {} } });
    FormRules.initialize(formContext, [rule()]);
    expect(control(formContext, "phone").getDisabled()).toBe(true);

    change(formContext, "status", 1);

    expect(control(formContext, "phone").getDisabled()).toBe(false);
  });

  it("never unlocks a field that is read-only on the form", () => {
    const formContext = createFormContextMock({ attributes: { status: { value: 1 }, phone: { control: { disabled: true } } } });
    FormRules.initialize(formContext, [rule()]);
    expect(control(formContext, "phone").getDisabled()).toBe(true);

    change(formContext, "status", 2);
    change(formContext, "status", 1);

    expect(control(formContext, "phone").getDisabled()).toBe(true);
  });

  it.each([FormType.ReadOnly, FormType.Disabled])("never unlocks anything on a read-only form (form type %s)", (formType) => {
    const formContext = createFormContextMock({ formType, attributes: { status: { value: 1 }, phone: {} } });

    FormRules.initialize(formContext, [rule()]);

    expect(control(formContext, "phone").getDisabled()).toBe(true);
  });
});

describe("SaveRules and save modes", () => {
  const form = () => createFormContextMock({ attributes: { title: { value: "AB" } } });
  const failing = (id: string, extra: Partial<SaveRule> = {}): SaveRule => ({
    id,
    fields: ["title"],
    validate: () => invalid({ field: "title", message: `${id} failed` }),
    ...extra,
  });
  const save = (formContext: Xrm.FormContext, rules: readonly SaveRule[], saveMode: number) =>
    SaveRules.handle(createSaveEventContextMock(formContext, saveMode), rules);
  const notifications = (formContext: Xrm.FormContext) =>
    [...(formContext.ui as unknown as { notifications: Map<string, unknown> }).notifications.keys()];

  it("runs error rules on AutoSave, so autosave never stores invalid data", () => {
    expect(save(form(), [failing("title")], SaveMode.AutoSave)).toBe(false);
  });

  it("skips info rules on AutoSave and keeps the tip they already show", () => {
    const formContext = form();
    const tip = failing("tip", { severity: "info" });

    save(formContext, [tip], SaveMode.Save);
    expect(notifications(formContext)).toEqual(["ogz_save_tip"]);

    expect(save(formContext, [tip], SaveMode.AutoSave)).toBe(true);
    expect(notifications(formContext)).toEqual(["ogz_save_tip"]);
    expect(SaveRules.validate(formContext, [tip], SaveMode.AutoSave)).toEqual({ isValid: true, errors: [], skipped: ["tip"] });
  });

  it("runs a rule only on the save modes it lists", () => {
    const onlyOnSave = failing("only_save", { saveModes: [SaveMode.Save, SaveMode.SaveAndClose] });

    expect(save(form(), [onlyOnSave], SaveMode.Deactivate)).toBe(true);
    expect(save(form(), [onlyOnSave], SaveMode.SaveAndClose)).toBe(false);
  });

  it("runs every rule when no save mode is given", () => {
    const outcome = SaveRules.validate(form(), [failing("a", { saveModes: [SaveMode.Save] }), failing("b", { severity: "info" })]);

    expect(outcome.errors.map((e) => e.ruleId)).toEqual(["a", "b"]);
    expect(outcome.skipped).toBeUndefined();
  });

  it("still clears messages of a rule that now passes and ran", () => {
    const formContext = form();
    let ok = false;
    const rule: SaveRule = { id: "flip", validate: () => (ok ? valid() : invalid({ message: "no" })) };

    save(formContext, [rule], SaveMode.Save);
    ok = true;
    save(formContext, [rule], SaveMode.Save);

    expect(notifications(formContext)).toEqual([]);
  });
});

describe("Events after a save and on data load", () => {
  it("registers post-save and data load handlers once, and removes them", () => {
    const formContext = createFormContextMock();
    const onPostSave = vi.fn();
    const onDataLoad = vi.fn();

    expect(Event.registerOnPostSave(formContext, onPostSave)).toBe(true);
    expect(Event.registerOnPostSave(formContext, onPostSave)).toBe(false);
    expect(Event.registerOnDataLoad(formContext, onDataLoad)).toBe(true);
    expect(Event.registerOnDataLoad(formContext, onDataLoad)).toBe(false);
    expect(formContext.data.entity.addOnPostSave).toHaveBeenCalledOnce();
    expect(formContext.data.addOnLoad).toHaveBeenCalledOnce();

    Event.unregisterOnPostSave(formContext, onPostSave);
    Event.unregisterOnPostSave(formContext, onPostSave);
    Event.unregisterOnDataLoad(formContext, onDataLoad);
    Event.unregisterOnDataLoad(formContext, onDataLoad);

    expect(formContext.data.entity.removeOnPostSave).toHaveBeenCalledExactlyOnceWith(onPostSave);
    expect(formContext.data.removeOnLoad).toHaveBeenCalledOnce();
  });

  it("FormRules applies every rule again when the form data loads again", () => {
    const formContext = createFormContextMock({ attributes: { status: { value: 1 }, phone: {} } });
    const apply = vi.fn();

    const dispose = FormRules.initialize(formContext, [{ dependsOn: [], apply }]);
    expect(apply).toHaveBeenCalledOnce();

    fireDataLoad(formContext);
    expect(apply).toHaveBeenCalledTimes(2);

    dispose();
    fireDataLoad(formContext);
    expect(apply).toHaveBeenCalledTimes(2);
  });

  it("initializing again replaces the data load handler instead of stacking it", () => {
    const formContext = createFormContextMock();
    const apply = vi.fn();

    FormRules.initialize(formContext, [{ dependsOn: [], apply }]);
    FormRules.initialize(formContext, [{ dependsOn: [], apply }]);
    apply.mockClear();
    fireDataLoad(formContext);

    expect(apply).toHaveBeenCalledOnce();
  });
});
