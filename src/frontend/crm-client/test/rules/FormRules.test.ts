import { describe, expect, it, vi } from "vitest";
import {
  FieldDisabledRule,
  FieldRequiredRule,
  FieldVisibilityRule,
  FormRules,
  matches,
  SectionSpecificVisibilityRule,
  type FormRule,
} from "../../src/rules/index.js";
import { createFormContextMock } from "../mocks/index.js";

const form = (category: number | null) =>
  createFormContextMock({
    attributes: {
      accountcategorycode: { value: category, attributeType: "optionset" },
      telephone1: {},
      websiteurl: {},
    },
    tabs: { SUMMARY_TAB: { sections: { DETAILS_SECTION: { controls: ["websiteurl"] } } } },
  });

const section = (formContext: Xrm.FormContext) => formContext.ui.tabs.get("SUMMARY_TAB")!.sections.get("DETAILS_SECTION")!;

const change = (formContext: Xrm.FormContext, value: number | null) => {
  const attribute = formContext.getAttribute("accountcategorycode")!;
  attribute.setValue(value);
  attribute.fireOnChange();
};

const visibilityRule = SectionSpecificVisibilityRule({
  tab: "SUMMARY_TAB",
  section: "DETAILS_SECTION",
  field: "accountcategorycode",
  visibleWhen: 1,
});

describe("FormRules", () => {
  it("evaluates rules on load", () => {
    const formContext = form(2);

    FormRules.initialize(formContext, [visibilityRule]);

    expect(section(formContext).getVisible()).toBe(false);
  });

  it("re-evaluates when the dependent column changes", () => {
    const formContext = form(2);
    FormRules.initialize(formContext, [visibilityRule]);

    change(formContext, 1);
    expect(section(formContext).getVisible()).toBe(true);

    change(formContext, null);
    expect(section(formContext).getVisible()).toBe(false);
  });

  it("registers one OnChange handler per column, even when initialized twice", () => {
    const formContext = form(1);
    const required = FieldRequiredRule({ targets: "telephone1", field: "accountcategorycode", requiredWhen: 1 });
    const attribute = formContext.getAttribute("accountcategorycode")!;

    FormRules.initialize(formContext, [visibilityRule, required]);
    FormRules.initialize(formContext, [visibilityRule, required]);

    expect(attribute.addOnChange).toHaveBeenCalledTimes(2);
    expect(attribute.removeOnChange).toHaveBeenCalledOnce();
  });

  it("dispose removes the OnChange handlers", () => {
    const formContext = form(2);
    const dispose = FormRules.initialize(formContext, [visibilityRule]);

    dispose();
    change(formContext, 1);

    expect(section(formContext).getVisible()).toBe(false);
  });

  it("does nothing when the source column, tab or section is missing", () => {
    const formContext = createFormContextMock({ attributes: { telephone1: {} } });
    const rules = [
      visibilityRule,
      SectionSpecificVisibilityRule({ tab: "NOPE", section: "NOPE", field: "telephone1", visibleWhen: "x" }),
    ];

    expect(() => FormRules.initialize(formContext, rules)).not.toThrow();
  });

  it("keeps applying other rules when one rule fails", () => {
    const formContext = form(1);
    const broken: FormRule = { dependsOn: [], apply: () => { throw new Error("boom"); } };
    const errorLog = vi.spyOn(console, "error").mockImplementation(() => {});

    FormRules.initialize(formContext, [broken, FieldDisabledRule({ targets: "telephone1", field: "accountcategorycode", disabledWhen: 1 })]);

    expect(formContext.getControl<Xrm.Controls.StandardControl>("telephone1")!.getDisabled()).toBe(true);
    expect(errorLog).toHaveBeenCalledOnce();
  });
});

describe("built-in rules", () => {
  it("FieldRequiredRule switches between the configured level and none", () => {
    const formContext = form(1);
    FormRules.initialize(formContext, [
      FieldRequiredRule({ targets: ["telephone1"], field: "accountcategorycode", requiredWhen: 1, level: "recommended" }),
    ]);

    expect(formContext.getAttribute("telephone1")!.getRequiredLevel()).toBe("recommended");
    change(formContext, 2);
    expect(formContext.getAttribute("telephone1")!.getRequiredLevel()).toBe("none");
  });

  it("FieldVisibilityRule and FieldDisabledRule accept lists and predicates", () => {
    const formContext = form(3);
    FormRules.initialize(formContext, [
      FieldVisibilityRule({ targets: "websiteurl", field: "accountcategorycode", visibleWhen: [1, 2] }),
      FieldDisabledRule({ targets: "telephone1", field: "accountcategorycode", disabledWhen: (v: number | null) => v === null || v > 2 }),
    ]);

    expect(formContext.getControl<Xrm.Controls.StandardControl>("websiteurl")!.getVisible()).toBe(false);
    expect(formContext.getControl<Xrm.Controls.StandardControl>("telephone1")!.getDisabled()).toBe(true);
  });

  it("matches compares values, lists and predicates", () => {
    expect(matches(1, 1)).toBe(true);
    expect(matches<number>(null, 1)).toBe(false);
    expect(matches(2, [1, 2])).toBe(true);
    expect(matches(true, (v: boolean | null) => v === true)).toBe(true);
  });
});
