import { describe, expect, it, vi } from "vitest";
import { invalid, MinTextLengthRule, NotWhitespaceRule, SaveRules, valid, type SaveRule } from "../../src/rules/index.js";
import { createFormContextMock, createSaveEventContextMock, rewrapFormContext } from "../mocks/index.js";

type NotificationMap = Map<string, { message: string; level: Xrm.FormNotificationLevel }>;
const notifications = (formContext: Xrm.FormContext) => (formContext.ui as unknown as { notifications: NotificationMap }).notifications;
const fieldNotifications = (formContext: Xrm.FormContext, field: string) =>
  (formContext.getControl(field) as unknown as { notifications: Map<string, string> }).notifications;

const form = (title: string | null, description: string | null = "details") =>
  createFormContextMock({
    attributes: {
      title: { value: title, label: "Title" },
      description: { value: description, attributeType: "memo" },
    },
  });

const titleRule = NotWhitespaceRule({ field: "title" });
const descriptionRule = NotWhitespaceRule({ field: "description", message: "Describe the problem." });

const save = (formContext: Xrm.FormContext, rules: readonly SaveRule[]) => {
  const context = createSaveEventContextMock(formContext);
  const allowed = SaveRules.handle(context, rules);
  return { allowed, args: context.getEventArgs() };
};

describe("NotWhitespaceRule", () => {
  it.each([["Printer offline"], ["  padded  "], ["x"]])("accepts text with content: %j", (value) => {
    expect(titleRule.validate(form(value)).isValid).toBe(true);
  });

  it.each([[" "], ["   "], ["\t\n "]])("rejects whitespace-only text: %j", (value) => {
    const result = titleRule.validate(form(value));

    expect(result.isValid).toBe(false);
    expect(result.errors).toEqual([{ field: "title", message: "Title cannot contain only spaces." }]);
  });

  it.each([[null], [""]])("leaves empty values to the required-level validation: %j", (value) => {
    expect(titleRule.validate(form(value)).isValid).toBe(true);
  });

  it("passes when the column is not on the form", () => {
    expect(titleRule.validate(createFormContextMock()).isValid).toBe(true);
  });

  it("uses the column name when the control has no label, and a custom message or id when given", () => {
    const formContext = createFormContextMock({ attributes: { ogz_code: { value: " ", label: "" } } });

    expect(NotWhitespaceRule({ field: "ogz_code" }).validate(formContext).errors[0]!.message).toBe("ogz_code cannot contain only spaces.");
    expect(descriptionRule.validate(form("t", " ")).errors[0]!.message).toBe("Describe the problem.");
    expect(NotWhitespaceRule({ field: "x", id: "custom" }).id).toBe("custom");
  });
});

describe("MinTextLengthRule", () => {
  const minThree = MinTextLengthRule({ field: "title", minLength: 3 });
  const check = (value: string | null) => minThree.validate(form(value));

  it.each([["abc"], ["  abc  "], ["a b"], ["Printer offline"], ["çğü"]])("accepts %j", (value) => {
    expect(check(value).isValid).toBe(true);
  });

  it.each([[" "], ["   "], ["a"], ["ab"], [" ab "], ["\t\na\n"]])("rejects %j", (value) => {
    expect(check(value).errors).toEqual([{ field: "title", message: "Title must contain at least 3 characters." }]);
  });

  it.each([[null], [""]])("leaves %j to the required-level validation", (value) => {
    expect(check(value).isValid).toBe(true);
  });

  it("counts characters, not UTF-16 units: two emoji are two characters", () => {
    expect(check("\u{1F600}\u{1F600}").isValid).toBe(false);
    expect(check("\u{1F600}\u{1F600}\u{1F600}").isValid).toBe(true);
  });

  it("passes when the column is not on the form", () => {
    expect(minThree.validate(createFormContextMock()).isValid).toBe(true);
  });

  it("supports any minimum, a custom message and id", () => {
    const rule = MinTextLengthRule({ field: "title", minLength: 1, message: "Required text.", id: "custom" });

    expect(rule.id).toBe("custom");
    expect(rule.validate(form("x")).isValid).toBe(true);
    expect(rule.validate(form("  ")).errors[0]!.message).toBe("Required text.");
    expect(MinTextLengthRule({ field: "title", minLength: 3 }).id).toBe("title_min_length");
  });

  it.each([[0], [-1], [2.5], [Number.NaN]])("rejects an invalid minimum (%s) when the rule is defined", (minLength) => {
    expect(() => MinTextLengthRule({ field: "title", minLength })).toThrow(RangeError);
  });
});

describe("SaveRules", () => {
  it("lets a valid form save and shows nothing", () => {
    const formContext = form("Printer offline");

    const { allowed, args } = save(formContext, [titleRule, descriptionRule]);

    expect(allowed).toBe(true);
    expect(args.preventDefault).not.toHaveBeenCalled();
    expect(formContext.ui.setFormNotification).not.toHaveBeenCalled();
  });

  it("blocks the save with an error notification on the form and on the field", () => {
    const formContext = form("   ");

    const { allowed, args } = save(formContext, [titleRule]);

    expect(allowed).toBe(false);
    expect(args.preventDefault).toHaveBeenCalledOnce();
    expect(args.isDefaultPrevented()).toBe(true);
    expect(notifications(formContext).get("ogz_save_title_not_whitespace")).toEqual({ message: "Title cannot contain only spaces.", level: "ERROR" });
    expect(fieldNotifications(formContext, "title").get("ogz_save_title_not_whitespace")).toBe("Title cannot contain only spaces.");
  });

  it("reports every failing rule, not just the first", () => {
    const formContext = form(" ", " ");

    save(formContext, [titleRule, descriptionRule]);

    expect([...notifications(formContext).keys()]).toEqual(["ogz_save_title_not_whitespace", "ogz_save_description_not_whitespace"]);
  });

  it("gives several errors of one rule their own notifications", () => {
    const twoErrors: SaveRule = { id: "pair", validate: () => invalid({ message: "first" }, { message: "second" }) };
    const formContext = form("t");

    save(formContext, [twoErrors]);

    expect([...notifications(formContext).keys()]).toEqual(["ogz_save_pair", "ogz_save_pair_1"]);
  });

  it("does not duplicate notifications when saving repeatedly with the same error", () => {
    const formContext = form(" ");

    save(formContext, [titleRule]);
    save(formContext, [titleRule]);

    expect(notifications(formContext).size).toBe(1);
    expect(fieldNotifications(formContext, "title").size).toBe(1);
  });

  it("clears the notifications once the data is valid again, and saves", () => {
    const formContext = form(" ", " ");
    save(formContext, [titleRule, descriptionRule]);

    formContext.getAttribute("title")!.setValue("Printer offline");
    const { allowed } = save(formContext, [titleRule, descriptionRule]);
    expect(allowed).toBe(false);
    expect([...notifications(formContext).keys()]).toEqual(["ogz_save_description_not_whitespace"]);
    expect(fieldNotifications(formContext, "title").size).toBe(0);

    formContext.getAttribute("description")!.setValue("Fixed after restart");
    const second = save(formContext, [titleRule, descriptionRule]);
    expect(second.allowed).toBe(true);
    expect(second.args.preventDefault).not.toHaveBeenCalled();
    expect(notifications(formContext).size).toBe(0);
  });

  it("blocks the save when a rule throws, keeps checking the other rules, and logs the error", () => {
    const broken: SaveRule = { id: "broken", validate: () => { throw new Error("metadata missing"); } };
    const errorLog = vi.spyOn(console, "error").mockImplementation(() => {});
    const formContext = form(" ");

    const { allowed, args } = save(formContext, [broken, titleRule]);

    expect(allowed).toBe(false);
    expect(args.preventDefault).toHaveBeenCalledOnce();
    expect(notifications(formContext).get("ogz_save_broken")!.message).toContain("could not be validated (rule 'broken': metadata missing)");
    expect(notifications(formContext).has("ogz_save_title_not_whitespace")).toBe(true);
    expect(errorLog).toHaveBeenCalledWith("Save rule 'broken' threw an error", expect.any(Error));
  });

  it("blocks the save when a rule throws something that is not an Error", () => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    const throwsText: SaveRule = { id: "text", validate: () => { throw "boom"; } };

    expect(SaveRules.validate(form("t"), [throwsText]).errors[0]!.message).toContain("rule 'text': boom");
  });

  it("clears form-only notifications (errors without a field) once they are gone", () => {
    let failing = true;
    const formLevel: SaveRule = { id: "form_level", validate: () => (failing ? invalid({ message: "Check the ticket." }) : valid()) };
    const formContext = form("t");
    save(formContext, [formLevel]);

    failing = false;
    save(formContext, [formLevel]);

    expect(formContext.ui.clearFormNotification).toHaveBeenCalledWith("ogz_save_form_level");
    expect(notifications(formContext).size).toBe(0);
  });

  it("treats a failing result without errors as a failure, never as valid", () => {
    const silent: SaveRule = { id: "silent", validate: () => ({ isValid: false, errors: [] }) };

    expect(SaveRules.validate(form("t"), [silent]).errors.map((e) => e.message)).toEqual(["Validation 'silent' failed."]);
  });

  it("validate() reports without touching the UI", () => {
    const formContext = form(" ");

    const outcome = SaveRules.validate(formContext, [titleRule, { id: "ok", validate: valid }]);

    expect(outcome.isValid).toBe(false);
    expect(outcome.errors).toEqual([
      { field: "title", message: "Title cannot contain only spaces.", ruleId: "title_not_whitespace", notificationId: "ogz_save_title_not_whitespace", severity: "error" },
    ]);
    expect(formContext.ui.setFormNotification).not.toHaveBeenCalled();
  });

  it("clears stale notifications when the next OnSave gets a different formContext object", () => {
    const formContext = form(" ");
    save(rewrapFormContext(formContext), [titleRule]);

    formContext.getAttribute("title")!.setValue("Printer offline");
    const { allowed } = save(rewrapFormContext(formContext), [titleRule]);

    expect(allowed).toBe(true);
    expect(notifications(formContext).size).toBe(0);
    expect(fieldNotifications(formContext, "title").size).toBe(0);
  });

  it("keeps notifications of different forms apart", () => {
    const first = form(" ");
    const second = form("valid");

    save(first, [titleRule]);
    save(second, [titleRule]);

    expect(notifications(first).size).toBe(1);
    expect(first.ui.clearFormNotification).not.toHaveBeenCalled();
  });
});

describe("SaveRules.revalidateOnChange", () => {
  const rules = [titleRule, descriptionRule, { id: "no_fields", validate: valid }] as const;

  it("depends on the columns the save rules declare, each once", () => {
    expect(SaveRules.revalidateOnChange([...rules, titleRule]).dependsOn).toEqual(["title", "description"]);
    expect(MinTextLengthRule({ field: "title", minLength: 3 }).fields).toEqual(["title"]);
  });

  it("clears the errors of a rule that is fixed, on both surfaces, and keeps the others", () => {
    const formContext = form(" ", " ");
    save(formContext, rules);

    formContext.getAttribute("title")!.setValue("Printer offline");
    SaveRules.revalidateOnChange(rules).apply(formContext);

    expect([...notifications(formContext).keys()]).toEqual(["ogz_save_description_not_whitespace"]);
    expect(fieldNotifications(formContext, "title").size).toBe(0);
    expect(fieldNotifications(formContext, "description").size).toBe(1);
  });

  it("keeps the error of a rule that still fails, and shows nothing new while typing", () => {
    const formContext = form(" ");
    save(formContext, [titleRule]);
    const other = form("t", " ");

    formContext.getAttribute("title")!.setValue("\t");
    SaveRules.revalidateOnChange([titleRule]).apply(formContext);
    SaveRules.revalidateOnChange([descriptionRule]).apply(other);

    expect(fieldNotifications(formContext, "title").size).toBe(1);
    expect(other.ui.setFormNotification).not.toHaveBeenCalled();
    expect(other.ui.clearFormNotification).not.toHaveBeenCalled();
  });

  it("clears form-only errors of a fixed rule too", () => {
    let failing = true;
    const formLevel: SaveRule = { id: "form_only", validate: () => (failing ? invalid({ message: "Check." }) : valid()) };
    const formContext = form("t");
    save(formContext, [formLevel]);

    failing = false;
    SaveRules.clearResolved(formContext, [formLevel]);

    expect(notifications(formContext).size).toBe(0);
  });
});
