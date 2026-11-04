import { describe, expect, it } from "vitest";
import { Control } from "../../src/helpers/control.js";
import { createFormContextMock } from "../mocks/index.js";

// One column with three controls: in two sections and in the header (which is in no section).
const form = () =>
  createFormContextMock({
    attributes: { telephone1: { value: "555" }, name: { value: "Contoso" } },
    controls: {
      telephone1_1: { attributeName: "telephone1" },
      header_telephone1: { attributeName: "telephone1" },
      notes_grid: { controlType: "subgrid" },
    },
    tabs: {
      SUMMARY_TAB: {
        sections: {
          CONTACT: { controls: ["telephone1", "name"] },
          DETAILS: { controls: ["telephone1_1", "notes_grid"] },
        },
      },
      OTHER_TAB: { sections: { CONTACT: { controls: [] } } },
    },
  });

const names = (controls: Xrm.Controls.Control[]) => controls.map((c) => c.getName());

describe("Control.resolve", () => {
  it("resolves a column name to every control of the column, header included (unchanged behaviour)", () => {
    expect(names(Control.resolve(form(), "telephone1"))).toEqual(["telephone1", "telephone1_1", "header_telephone1"]);
  });

  it("resolves a control name that is not a column", () => {
    expect(names(Control.resolve(form(), "notes_grid"))).toEqual(["notes_grid"]);
  });

  it("resolves a column in one section to that section's controls only", () => {
    const formContext = form();

    expect(names(Control.resolve(formContext, { tab: "SUMMARY_TAB", section: "CONTACT", field: "telephone1" }))).toEqual(["telephone1"]);
    expect(names(Control.resolve(formContext, { tab: "SUMMARY_TAB", section: "DETAILS", field: "telephone1" }))).toEqual(["telephone1_1"]);
  });

  it("prefers an exact control name in the section over other controls of the column", () => {
    expect(names(Control.resolve(form(), { tab: "SUMMARY_TAB", section: "DETAILS", field: "telephone1_1" }))).toEqual(["telephone1_1"]);
  });

  it("matches controls without a bound column by name only", () => {
    expect(names(Control.resolve(form(), { tab: "SUMMARY_TAB", section: "DETAILS", field: "notes_grid" }))).toEqual(["notes_grid"]);
  });

  it.each([
    ["a missing tab", { tab: "MISSING", section: "CONTACT", field: "telephone1" }],
    ["a missing section", { tab: "SUMMARY_TAB", section: "MISSING", field: "telephone1" }],
    ["a section of the same name in another tab", { tab: "OTHER_TAB", section: "CONTACT", field: "telephone1" }],
    ["a column that is not in the section", { tab: "SUMMARY_TAB", section: "DETAILS", field: "name" }],
    ["a column that is not on the form", { tab: "SUMMARY_TAB", section: "CONTACT", field: "fax" }],
  ])("resolves nothing (and never another control) for %s", (_, target) => {
    expect(Control.resolve(form(), target)).toEqual([]);
  });

  it("resolves nothing for an unknown name", () => {
    expect(Control.resolve(form(), "missing")).toEqual([]);
  });

  it("changes only the targeted section's control", () => {
    const formContext = form();

    expect(Control.setVisible(formContext, { tab: "SUMMARY_TAB", section: "DETAILS", field: "telephone1" }, false)).toBe(true);
    expect(Control.setDisabled(formContext, { tab: "SUMMARY_TAB", section: "DETAILS", field: "telephone1" }, true)).toBe(true);

    expect(formContext.getControl<Xrm.Controls.StandardControl>("telephone1_1")!.getVisible()).toBe(false);
    expect(formContext.getControl<Xrm.Controls.StandardControl>("telephone1")!.getVisible()).toBe(true);
    expect(formContext.getControl<Xrm.Controls.StandardControl>("header_telephone1")!.getDisabled()).toBe(false);
    expect(Control.setVisible(formContext, { tab: "MISSING", section: "DETAILS", field: "telephone1" }, false)).toBe(false);
  });

  it("describes targets for messages", () => {
    expect(Control.describe("telephone1")).toBe("telephone1");
    expect(Control.describe({ tab: "SUMMARY_TAB", section: "CONTACT", field: "telephone1" })).toBe("SUMMARY_TAB/CONTACT/telephone1");
  });
});

describe("Control notifications", () => {
  type Inspectable = { notifications: Map<string, string>; notificationLevels: Map<string, string> };
  const inspect = (formContext: Xrm.FormContext, name: string) => formContext.getControl(name) as unknown as Inspectable;

  it("shows an ERROR with setNotification, on every control of the column", () => {
    const formContext = form();

    expect(Control.setNotification(formContext, "telephone1", "Invalid number.", "id1")).toBe(true);

    for (const name of ["telephone1", "telephone1_1", "header_telephone1"]) {
      expect(formContext.getControl<Xrm.Controls.StandardControl>(name)!.setNotification).toHaveBeenCalledWith("Invalid number.", "id1");
      expect(inspect(formContext, name).notificationLevels.get("id1")).toBe("ERROR");
    }
  });

  it("shows a RECOMMENDATION with addNotification, only on the targeted control", () => {
    const formContext = form();

    Control.setNotification(formContext, { tab: "SUMMARY_TAB", section: "CONTACT", field: "telephone1" }, "Add a mobile number.", "id2", "RECOMMENDATION");

    expect(formContext.getControl<Xrm.Controls.StandardControl>("telephone1")!.addNotification).toHaveBeenCalledWith({
      messages: ["Add a mobile number."],
      notificationLevel: "RECOMMENDATION",
      uniqueId: "id2",
    });
    expect(inspect(formContext, "telephone1").notificationLevels.get("id2")).toBe("RECOMMENDATION");
    expect(inspect(formContext, "telephone1_1").notifications.size).toBe(0);
  });

  it("does not fall back to a save-blocking error when a control has no addNotification", () => {
    const formContext = createFormContextMock({ controls: { legacy_control: { addNotification: false } } });

    expect(Control.setNotification(formContext, "legacy_control", "Tip.", "id3", "RECOMMENDATION")).toBe(false);
    expect(formContext.getControl<Xrm.Controls.StandardControl>("legacy_control")!.setNotification).not.toHaveBeenCalled();
  });

  it("clears a notification by id and reports a missing target", () => {
    const formContext = form();
    Control.setNotification(formContext, "telephone1", "Invalid number.", "id1");

    expect(Control.clearNotification(formContext, "telephone1", "id1")).toBe(true);
    expect(inspect(formContext, "telephone1").notifications.size).toBe(0);
    expect(Control.clearNotification(formContext, "missing", "id1")).toBe(false);
    expect(Control.setNotification(formContext, "missing", "x", "id1")).toBe(false);
  });
});
