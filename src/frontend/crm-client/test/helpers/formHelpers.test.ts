import { describe, expect, it, vi } from "vitest";
import { Control } from "../../src/helpers/control.js";
import { Field } from "../../src/helpers/field.js";
import { Grid } from "../../src/helpers/grid.js";
import { Section, Tab } from "../../src/helpers/tab.js";
import { createFormContextMock } from "../mocks/index.js";

const PRIORITY = [
  { value: 1, text: "Low" },
  { value: 2, text: "Normal" },
  { value: 3, text: "Critical" },
];
const CONTACT = { tab: "SUMMARY_TAB", section: "CONTACT" } as const;
const ACTIVE_ONLY = `<filter><condition attribute="statecode" operator="eq" value="0" /></filter>`;

/**
 * SUMMARY_TAB/CONTACT holds telephone1 and emailaddress1; SUMMARY_TAB/DETAILS holds a second control of
 * telephone1 ("telephone1_1"), priority and the notes subgrid. telephone1 is also in the header.
 */
function form() {
  return createFormContextMock({
    attributes: {
      telephone1: { value: "+90 212 000 00 00", requiredLevel: "required" },
      emailaddress1: { value: null, requiredLevel: "required" },
      priority: { value: 3, attributeType: "optionset", options: PRIORITY },
      tags: { value: [1, 3], attributeType: "multiselectoptionset", options: PRIORITY },
      parentaccountid: {
        value: [{ id: "{A1}", entityType: "account", name: "Contoso" }],
        attributeType: "lookup",
        control: { lookup: true },
      },
      followup: { value: new Date("2026-11-07T09:00:00Z"), attributeType: "datetime" },
      donotemail: { value: false, attributeType: "boolean" },
      revenue: { value: 1500, attributeType: "money" },
    },
    controls: {
      telephone1_1: { attributeName: "telephone1" },
      header_telephone1: { attributeName: "telephone1" },
      notes_grid: { controlType: "subgrid", grid: { recordCount: 4 } },
    },
    tabs: {
      SUMMARY_TAB: {
        sections: {
          CONTACT: { controls: ["telephone1", "emailaddress1"] },
          DETAILS: { controls: ["telephone1_1", "priority", "notes_grid"] },
        },
      },
    },
  });
}

type ChoiceControl = { offeredOptions: Xrm.OptionSetValue[] };
type LookupControl = { runPreSearch(): { filter: string; entityType: string | undefined }[]; preSearchHandlers: unknown[] };
const control = <T>(formContext: Xrm.FormContext, name: string) => formContext.getControl(name) as unknown as T & Xrm.Controls.StandardControl;

describe("Field with a column name or a section target", () => {
  it("reads the column of the control placed in the section", () => {
    const formContext = form();

    expect(Field.getValue(formContext, { ...CONTACT, field: "telephone1" })).toBe("+90 212 000 00 00");
    expect(Field.getValue(formContext, { tab: "SUMMARY_TAB", section: "DETAILS", field: "telephone1_1" })).toBe("+90 212 000 00 00");
    expect(Field.getLogicalName(formContext, { tab: "SUMMARY_TAB", section: "DETAILS", field: "telephone1_1" })).toBe("telephone1");
    expect(Field.getLogicalName(formContext, "header_telephone1")).toBe("telephone1");
  });

  it("finds nothing outside the section and for columns not on the form", () => {
    const formContext = form();

    expect(Field.exists(formContext, { ...CONTACT, field: "priority" })).toBe(false);
    expect(Field.getValue(formContext, { ...CONTACT, field: "priority" })).toBeNull();
    expect(Field.exists(formContext, { tab: "NOPE", section: "CONTACT", field: "telephone1" })).toBe(false);
    expect(Field.exists(formContext, "missing")).toBe(false);
    expect(Field.setValue(formContext, "missing", "x")).toBe(false);
    expect(Field.getLogicalName(formContext, "missing")).toBeNull();
  });

  it("writes through a section target to the one column", () => {
    const formContext = form();

    expect(Field.setValue(formContext, { ...CONTACT, field: "telephone1" }, "+90 555")).toBe(true);

    expect(Field.getValue(formContext, "telephone1")).toBe("+90 555");
    expect(Field.isDirty(formContext, "telephone1")).toBe(true);
    expect(Field.isDirty(formContext, "emailaddress1")).toBe(false);
    expect(Field.isDirty(formContext, "missing")).toBeNull();
  });

  it("clears a value, optionally running its OnChange handlers", () => {
    const formContext = form();
    const onChange = vi.fn();
    formContext.getAttribute("telephone1")!.addOnChange(onChange);

    expect(Field.clear(formContext, "telephone1")).toBe(true);
    expect(onChange).not.toHaveBeenCalled();
    Field.clear(formContext, "telephone1", { fireOnChange: true });

    expect(Field.getValue(formContext, "telephone1")).toBeNull();
    expect(onChange).toHaveBeenCalledOnce();
  });

  it("runs OnChange on request", () => {
    const formContext = form();

    expect(Field.fireOnChange(formContext, { ...CONTACT, field: "telephone1" })).toBe(true);
    expect(formContext.getAttribute("telephone1")!.fireOnChange).toHaveBeenCalledOnce();
    expect(Field.fireOnChange(formContext, "missing")).toBe(false);
  });

  it("sets the submit mode", () => {
    const formContext = form();

    expect(Field.setSubmitMode(formContext, "revenue", "always")).toBe(true);
    expect(formContext.getAttribute("revenue")!.getSubmitMode()).toBe("always");
    expect(Field.setSubmitMode(formContext, "missing", "always")).toBe(false);
  });

  it.each([
    ["a choice", "priority", "Critical"],
    ["a multi-select choice", "tags", "Low; Critical"],
    ["a lookup", "parentaccountid", "Contoso"],
    ["a date", "followup", "2026-11-07T09:00:00.000Z"],
    ["a two-option column", "donotemail", "false"],
    ["a number", "revenue", "1500"],
    ["text", "telephone1", "+90 212 000 00 00"],
  ])("gives the display text of %s", (_, column, text) => {
    expect(Field.getText(form(), column)).toBe(text);
  });

  it("gives the display text of a lookup record without a name as empty", () => {
    const formContext = createFormContextMock({ attributes: { ownerid: { value: [{ id: "{U1}", entityType: "systemuser" }] } } });

    expect(Field.getText(formContext, "ownerid")).toBe("");
  });

  it("finds no column for a control without one, such as a subgrid", () => {
    expect(Field.exists(form(), "notes_grid")).toBe(false);
  });

  it("gives no display text for an empty or missing column", () => {
    expect(Field.getText(form(), "emailaddress1")).toBeNull();
    expect(Field.getText(form(), "missing")).toBeNull();
  });

  it("makes a column required or optional and reports it", () => {
    const formContext = form();

    expect(Field.setRequired(formContext, { ...CONTACT, field: "telephone1" }, false)).toBe(true);
    expect(Field.isRequired(formContext, "telephone1")).toBe(false);
    expect(Field.getRequiredLevel(formContext, "telephone1")).toBe("none");
    Field.setRequired(formContext, "telephone1", true);
    expect(Field.isRequired(formContext, "telephone1")).toBe(true);
    expect(Field.isRequired(formContext, "missing")).toBeNull();
    expect(Field.setRequired(formContext, "missing", true)).toBe(false);
  });
});

describe("Control: label, focus and state", () => {
  it("changes the label only in the section when given a section target", () => {
    const formContext = form();

    expect(Control.setLabel(formContext, { tab: "SUMMARY_TAB", section: "DETAILS", field: "telephone1" }, "Mobile")).toBe(true);

    expect(Control.getLabel(formContext, "telephone1_1")).toBe("Mobile");
    expect(Control.getLabel(formContext, { ...CONTACT, field: "telephone1" })).toBe("telephone1");
    expect(Control.getLabel(formContext, "missing")).toBeNull();
    expect(Control.setLabel(formContext, "missing", "x")).toBe(false);
  });

  it("moves the focus to the first visible control", () => {
    const formContext = form();
    control(formContext, "telephone1").setVisible(false);

    expect(Control.setFocus(formContext, "telephone1")).toBe(true);

    expect(control(formContext, "telephone1").setFocus).not.toHaveBeenCalled();
    expect(control(formContext, "telephone1_1").setFocus).toHaveBeenCalledOnce();
    expect(Control.setFocus(formContext, "missing")).toBe(false);
  });

  it("reads visibility and disabled state through a target", () => {
    const formContext = form();
    Control.setDisabled(formContext, { tab: "SUMMARY_TAB", section: "DETAILS", field: "telephone1" }, true);
    Control.setVisible(formContext, { tab: "SUMMARY_TAB", section: "DETAILS", field: "telephone1" }, false);

    expect(Control.isDisabled(formContext, { tab: "SUMMARY_TAB", section: "DETAILS", field: "telephone1" })).toBe(true);
    expect(Control.isVisible(formContext, { tab: "SUMMARY_TAB", section: "DETAILS", field: "telephone1" })).toBe(false);
    expect(Control.isDisabled(formContext, { ...CONTACT, field: "telephone1" })).toBe(false);
    expect(Control.isVisible(formContext, "missing")).toBeNull();
    expect(Control.isDisabled(formContext, "missing")).toBeNull();
  });
});

describe("Control: choice options", () => {
  it("offers only the given values, in the column's order", () => {
    const formContext = form();

    expect(Control.setOptions(formContext, "priority", [3, 1])).toBe(true);

    expect(control<ChoiceControl>(formContext, "priority").offeredOptions.map((o) => o.text)).toEqual(["Low", "Critical"]);
  });

  it("removes values and offers all of them again", () => {
    const formContext = form();

    expect(Control.removeOptions(formContext, "priority", [1])).toBe(true);
    expect(control<ChoiceControl>(formContext, "priority").offeredOptions.map((o) => o.value)).toEqual([2, 3]);
    expect(Control.resetOptions(formContext, "priority")).toBe(true);
    expect(control<ChoiceControl>(formContext, "priority").offeredOptions.map((o) => o.value)).toEqual([1, 2, 3]);
  });

  it("does nothing for a control that is not a choice", () => {
    const formContext = form();

    expect(Control.setOptions(formContext, "telephone1", [1])).toBe(false);
    expect(Control.removeOptions(formContext, "missing", [1])).toBe(false);
    expect(Control.resetOptions(formContext, "telephone1")).toBe(false);
  });
});

describe("Control: lookup filter and view", () => {
  it("adds the filter when the user searches", () => {
    const formContext = form();

    expect(Control.setLookupFilter(formContext, "parentaccountid", ACTIVE_ONLY, "account")).toBe(true);

    expect(control<LookupControl>(formContext, "parentaccountid").runPreSearch()).toEqual([{ filter: ACTIVE_ONLY, entityType: "account" }]);
  });

  it("replaces and removes the filter with a single PreSearch handler", () => {
    const formContext = form();
    const lookup = control<LookupControl>(formContext, "parentaccountid");

    Control.setLookupFilter(formContext, "parentaccountid", ACTIVE_ONLY);
    Control.setLookupFilter(formContext, "parentaccountid", "<filter />");
    expect(lookup.runPreSearch()).toEqual([{ filter: "<filter />", entityType: undefined }]);

    Control.setLookupFilter(formContext, "parentaccountid", null);
    expect(lookup.runPreSearch()).toEqual([]);
    expect(lookup.preSearchHandlers).toHaveLength(1);
  });

  it("sets the default view", () => {
    const formContext = form();

    expect(Control.setLookupDefaultView(formContext, "parentaccountid", "{VIEW}")).toBe(true);
    expect((control(formContext, "parentaccountid") as unknown as Xrm.Controls.LookupControl).setDefaultView).toHaveBeenCalledWith("{VIEW}");
  });

  it("does nothing for a control that is not a lookup", () => {
    const formContext = form();

    expect(Control.setLookupFilter(formContext, "telephone1", ACTIVE_ONLY)).toBe(false);
    expect(Control.setLookupDefaultView(formContext, "telephone1", "{VIEW}")).toBe(false);
  });
});

describe("Tab", () => {
  it("expands, collapses and opens a tab", () => {
    const formContext = form();
    const tab = formContext.ui.tabs.get("SUMMARY_TAB")!;

    expect(Tab.setExpanded(formContext, "SUMMARY_TAB", false)).toBe(true);
    expect(tab.getDisplayState()).toBe("collapsed");
    Tab.setExpanded(formContext, "SUMMARY_TAB", true);
    expect(tab.getDisplayState()).toBe("expanded");
    expect(Tab.setFocus(formContext, "SUMMARY_TAB")).toBe(true);
    expect(tab.setFocus).toHaveBeenCalledOnce();
  });

  it("reports existence and visibility, and ignores missing tabs", () => {
    const formContext = form();
    Tab.setVisible(formContext, "SUMMARY_TAB", false);

    expect(Tab.exists(formContext, "SUMMARY_TAB")).toBe(true);
    expect(Tab.isVisible(formContext, "SUMMARY_TAB")).toBe(false);
    expect(Tab.exists(formContext, "NOPE")).toBe(false);
    expect(Tab.isVisible(formContext, "NOPE")).toBeNull();
    expect(Tab.setExpanded(formContext, "NOPE", true)).toBe(false);
    expect(Tab.setFocus(formContext, "NOPE")).toBe(false);
  });
});

describe("Section", () => {
  it("lists the columns placed in it, once each, without subgrids", () => {
    expect(Section.getFields(form(), "SUMMARY_TAB", "DETAILS")).toEqual(["telephone1", "priority"]);
    expect(Section.getFields(form(), "SUMMARY_TAB", "NOPE")).toEqual([]);
  });

  it("returns the values of its columns", () => {
    expect(Section.getValues(form(), "SUMMARY_TAB", "CONTACT")).toEqual({ telephone1: "+90 212 000 00 00", emailaddress1: null });
    expect(Section.getValues(form(), "NOPE", "CONTACT")).toEqual({});
  });

  it("sets the requirement level of its columns, except the given ones", () => {
    const formContext = form();

    expect(Section.setRequiredLevelAll(formContext, "SUMMARY_TAB", "CONTACT", "none", ["emailaddress1"])).toBe(true);

    expect(Field.getRequiredLevel(formContext, "telephone1")).toBe("none");
    expect(Field.getRequiredLevel(formContext, "emailaddress1")).toBe("required");
    expect(Section.setRequiredLevelAll(formContext, "SUMMARY_TAB", "NOPE", "none")).toBe(false);
  });

  it("lists its required columns that are empty", () => {
    const formContext = form();

    expect(Section.getMissingRequired(formContext, "SUMMARY_TAB", "CONTACT")).toEqual(["emailaddress1"]);
    Field.setValue(formContext, "emailaddress1", "help@contoso.com");
    expect(Section.getMissingRequired(formContext, "SUMMARY_TAB", "CONTACT")).toEqual([]);
    expect(Section.getMissingRequired(formContext, "SUMMARY_TAB", "NOPE")).toEqual([]);
  });

  it("counts empty text and an empty multi-select choice as missing", () => {
    const formContext = form();
    Field.setValue(formContext, "emailaddress1", "help@contoso.com");
    Field.setValue(formContext, "telephone1", "");
    expect(Section.getMissingRequired(formContext, "SUMMARY_TAB", "CONTACT")).toEqual(["telephone1"]);

    const tags = createFormContextMock({
      attributes: { tags: { value: [], requiredLevel: "required", attributeType: "multiselectoptionset", options: PRIORITY } },
      tabs: { T: { sections: { S: { controls: ["tags"] } } } },
    });
    expect(Section.getMissingRequired(tags, "T", "S")).toEqual(["tags"]);
  });

  it("reports existence and visibility", () => {
    const formContext = form();
    Section.setVisible(formContext, "SUMMARY_TAB", "CONTACT", false);

    expect(Section.exists(formContext, "SUMMARY_TAB", "CONTACT")).toBe(true);
    expect(Section.isVisible(formContext, "SUMMARY_TAB", "CONTACT")).toBe(false);
    expect(Section.exists(formContext, "SUMMARY_TAB", "NOPE")).toBe(false);
    expect(Section.isVisible(formContext, "SUMMARY_TAB", "NOPE")).toBeNull();
  });
});

describe("Grid", () => {
  it("refreshes a subgrid and counts its records", () => {
    const formContext = form();

    expect(Grid.refresh(formContext, "notes_grid")).toBe(true);
    expect((formContext.getControl("notes_grid") as unknown as Xrm.Controls.GridControl).refresh).toHaveBeenCalledOnce();
    expect(Grid.getRecordCount(formContext, "notes_grid")).toBe(4);
    expect(Grid.get(formContext, "notes_grid")).not.toBeNull();
  });

  it("ignores missing subgrids and controls that are not subgrids", () => {
    const formContext = form();

    expect(Grid.refresh(formContext, "missing")).toBe(false);
    expect(Grid.getRecordCount(formContext, "telephone1")).toBeNull();
    expect(Grid.get(formContext, "telephone1")).toBeNull();
  });
});
