// The reusable status-driven rules, with the real ogz_support_ticket status reason values from
// crm-entities and a generic target column. The Support Ticket form's own use of them is tested in
// test/forms/supportTicket.test.ts.

import { ogz_support_ticket_statuscode, ogz_support_ticketFields } from "@ogz/crm-entities";
import { describe, expect, it } from "vitest";
import { FieldRequiredRule, FormNotificationRule, FormRules, hasValue, isEmpty } from "../../src/rules/index.js";
import { createFormContextMock } from "../mocks/index.js";

const DESCRIPTION = "test_description";
const { statuscode } = ogz_support_ticketFields;

const form = (status: number, descriptionLevel: Xrm.Attributes.RequirementLevel = "none") =>
  createFormContextMock({
    entityName: "ogz_support_ticket",
    attributes: {
      [statuscode]: { value: status, attributeType: "optionset" },
      [DESCRIPTION]: { requiredLevel: descriptionLevel },
    },
  });

const closingStatusRule = FieldRequiredRule({
  targets: DESCRIPTION,
  field: statuscode,
  requiredWhen: [ogz_support_ticket_statuscode.Resolved, ogz_support_ticket_statuscode.Cancelled],
});

const setStatus = (formContext: Xrm.FormContext, value: number) => {
  const status = formContext.getAttribute(statuscode)!;
  status.setValue(value);
  status.fireOnChange();
};

const level = (formContext: Xrm.FormContext) => formContext.getAttribute(DESCRIPTION)!.getRequiredLevel();

describe("status-driven required rule", () => {
  it.each([ogz_support_ticket_statuscode.Resolved, ogz_support_ticket_statuscode.Cancelled])("makes the column required for status %s", (status) => {
    const formContext = form(status);

    FormRules.initialize(formContext, [closingStatusRule]);

    expect(level(formContext)).toBe("required");
  });

  it.each([
    ogz_support_ticket_statuscode.New,
    ogz_support_ticket_statuscode.Triage,
    ogz_support_ticket_statuscode.Assigned,
    ogz_support_ticket_statuscode.In_Process,
    ogz_support_ticket_statuscode.Closed,
  ])("leaves the column at its original level for status %s", (status) => {
    const formContext = form(status, "recommended");

    FormRules.initialize(formContext, [closingStatusRule]);

    expect(level(formContext)).toBe("recommended");
  });

  it("restores the form's original level, not 'none', when the status changes back", () => {
    const formContext = form(ogz_support_ticket_statuscode.New, "recommended");
    FormRules.initialize(formContext, [closingStatusRule]);

    setStatus(formContext, ogz_support_ticket_statuscode.Resolved);
    expect(level(formContext)).toBe("required");

    setStatus(formContext, ogz_support_ticket_statuscode.Assigned);
    expect(level(formContext)).toBe("recommended");
  });

  it("remembers the original level even when the form opens in a matching status", () => {
    const formContext = form(ogz_support_ticket_statuscode.Cancelled, "recommended");
    FormRules.initialize(formContext, [closingStatusRule]);

    setStatus(formContext, ogz_support_ticket_statuscode.In_Process);

    expect(level(formContext)).toBe("recommended");
  });

  it("ignores targets that are not on the form", () => {
    const formContext = createFormContextMock({ attributes: { [statuscode]: { value: ogz_support_ticket_statuscode.Resolved } } });

    expect(() => FormRules.initialize(formContext, [closingStatusRule])).not.toThrow();
  });
});

describe("FormNotificationRule", () => {
  const rule = FormNotificationRule({ field: statuscode, showWhen: ogz_support_ticket_statuscode.Cancelled, message: "Cancelled", id: "n1", level: "WARNING" });

  it("shows the notification with the configured level and clears it when the condition ends", () => {
    const formContext = form(ogz_support_ticket_statuscode.Cancelled);
    FormRules.initialize(formContext, [rule]);

    expect(formContext.ui.setFormNotification).toHaveBeenCalledWith("Cancelled", "WARNING", "n1");

    setStatus(formContext, ogz_support_ticket_statuscode.New);
    expect(formContext.ui.clearFormNotification).toHaveBeenCalledWith("n1");
  });

  it("defaults to an informational notification", () => {
    const formContext = form(ogz_support_ticket_statuscode.New);
    FormRules.initialize(formContext, [FormNotificationRule({ field: statuscode, showWhen: hasValue, message: "m", id: "n2" })]);

    expect(formContext.ui.setFormNotification).toHaveBeenCalledWith("m", "INFO", "n2");
  });
});

describe("isEmpty / hasValue", () => {
  it.each([[null], [undefined], [""], [[]]])("treats %j as empty", (value) => {
    expect(isEmpty(value)).toBe(true);
    expect(hasValue(value)).toBe(false);
  });

  it.each([[0], [false], ["x"], [[{ id: "1" }]]])("treats %j as a value", (value) => {
    expect(isEmpty(value)).toBe(false);
  });
});
