import { ogz_support_ticket_ogz_priority as Priority, ogz_support_ticket_statuscode as Status, ogz_support_ticketFields as Fields } from "@ogz/crm-entities";
import { describe, expect, it } from "vitest";
import { CATEGORY_NOTIFICATION_ID, onLoad, onSave } from "../../src/forms/supportTicket/index.js";
import {
  createEventContextMock,
  createFormContextMock,
  createSaveEventContextMock,
  installXrmMock,
  rewrapFormContext,
  simulateUserSave,
  type AttributeMockOptions,
} from "../mocks/index.js";

const HARDWARE: Xrm.LookupValue = { id: "{11111111-0000-0000-0000-000000000001}", entityType: "ogz_ticket_category", name: "Hardware" };

const ticketForm = (options: { category?: Xrm.LookupValue[] | null; status?: Status; descriptionLevel?: Xrm.Attributes.RequirementLevel } = {}) => {
  const attributes: Record<string, AttributeMockOptions> = {
    [Fields.ogz_ticket_title]: { value: "Printer offline" },
    [Fields.ogz_category]: { value: options.category === undefined ? [HARDWARE] : options.category, attributeType: "lookup" },
    [Fields.statuscode]: { value: options.status ?? Status.New, attributeType: "optionset" },
    [Fields.ogz_description]: { value: null, attributeType: "memo", requiredLevel: options.descriptionLevel ?? "none" },
  };
  return createFormContextMock({ entityName: "ogz_support_ticket", attributes });
};

type NotificationMap = Map<string, { message: string; level: Xrm.FormNotificationLevel }>;
const notifications = (formContext: Xrm.FormContext) => (formContext.ui as unknown as { notifications: NotificationMap }).notifications;
const descriptionLevel = (formContext: Xrm.FormContext) => formContext.getAttribute(Fields.ogz_description)!.getRequiredLevel();
const load = (formContext: Xrm.FormContext) => onLoad(createEventContextMock(formContext));

const change = (formContext: Xrm.FormContext, column: string, value: unknown) => {
  const attribute = formContext.getAttribute(column)!;
  attribute.setValue(value as Xrm.Attributes.AttributeValues);
  attribute.fireOnChange();
};

const OTHER_STATUSES = [Status.New, Status.Triage, Status.Assigned, Status.In_Process, Status.Closed];

describe("Support Ticket form: category notification", () => {
  it("shows an informational notification when no category is selected", () => {
    const formContext = ticketForm({ category: null });

    load(formContext);

    expect(notifications(formContext).get(CATEGORY_NOTIFICATION_ID)).toEqual({
      message: expect.stringContaining("Select a ticket category"),
      level: "INFO",
    });
  });

  it("shows no notification when a category is already selected", () => {
    const formContext = ticketForm();

    load(formContext);

    expect(notifications(formContext).has(CATEGORY_NOTIFICATION_ID)).toBe(false);
  });

  it("removes the notification when a category is selected, and shows it again when cleared", () => {
    const formContext = ticketForm({ category: null });
    load(formContext);

    change(formContext, Fields.ogz_category, [HARDWARE]);
    expect(notifications(formContext).has(CATEGORY_NOTIFICATION_ID)).toBe(false);

    change(formContext, Fields.ogz_category, null);
    expect(notifications(formContext).has(CATEGORY_NOTIFICATION_ID)).toBe(true);
  });
});

describe("Support Ticket form: description required for closing statuses", () => {
  it.each([["Resolved", Status.Resolved], ["Cancelled", Status.Cancelled]])("makes Description required when the form opens as %s", (_, status) => {
    const formContext = ticketForm({ status });

    load(formContext);

    expect(descriptionLevel(formContext)).toBe("required");
  });

  it.each(OTHER_STATUSES.map((s) => [Status[s], s] as const))("keeps Description optional for %s", (_, status) => {
    const formContext = ticketForm({ status });

    load(formContext);

    expect(descriptionLevel(formContext)).toBe("none");
  });

  it.each([["Resolved", Status.Resolved], ["Cancelled", Status.Cancelled]])("makes Description required when Status Reason changes to %s", (_, status) => {
    const formContext = ticketForm({ status: Status.In_Process });
    load(formContext);

    change(formContext, Fields.statuscode, status);

    expect(descriptionLevel(formContext)).toBe("required");
  });

  it("restores the original required level when the status changes away from a closing status", () => {
    const formContext = ticketForm({ status: Status.Assigned, descriptionLevel: "recommended" });
    load(formContext);

    change(formContext, Fields.statuscode, Status.Resolved);
    expect(descriptionLevel(formContext)).toBe("required");

    change(formContext, Fields.statuscode, Status.In_Process);
    expect(descriptionLevel(formContext)).toBe("recommended");
  });

  it("restores the original level even when the form opened as Cancelled", () => {
    const formContext = ticketForm({ status: Status.Cancelled, descriptionLevel: "recommended" });
    load(formContext);

    change(formContext, Fields.statuscode, Status.New);

    expect(descriptionLevel(formContext)).toBe("recommended");
  });

  it("keeps the category notification and the description rule independent", () => {
    const formContext = ticketForm({ category: null, status: Status.New });
    load(formContext);

    change(formContext, Fields.statuscode, Status.Resolved);
    expect(notifications(formContext).has(CATEGORY_NOTIFICATION_ID)).toBe(true);

    change(formContext, Fields.ogz_category, [HARDWARE]);
    expect(descriptionLevel(formContext)).toBe("required");
    expect(notifications(formContext).has(CATEGORY_NOTIFICATION_ID)).toBe(false);
  });
});

describe("Support Ticket form: OnLoad and event handling", () => {
  it("registers exactly one OnChange handler per rule column", () => {
    const formContext = ticketForm();

    load(formContext);

    expect(formContext.getAttribute(Fields.ogz_category)!.addOnChange).toHaveBeenCalledOnce();
    expect(formContext.getAttribute(Fields.statuscode)!.addOnChange).toHaveBeenCalledOnce();
    expect(formContext.getAttribute(Fields.ogz_description)!.addOnChange).not.toHaveBeenCalled();
    // Title: re-checks its save rule, so a corrected title clears the error that would block saving.
    expect(formContext.getAttribute(Fields.ogz_ticket_title)!.addOnChange).toHaveBeenCalledOnce();
  });

  it("replaces its handlers when onLoad runs again, so each change is handled once", () => {
    const formContext = ticketForm({ category: null });
    const category = formContext.getAttribute(Fields.ogz_category)!;
    const status = formContext.getAttribute(Fields.statuscode)!;

    load(formContext);
    load(formContext);
    change(formContext, Fields.ogz_category, [HARDWARE]);

    expect(category.addOnChange).toHaveBeenCalledTimes(2);
    expect(category.removeOnChange).toHaveBeenCalledOnce();
    expect(status.removeOnChange).toHaveBeenCalledOnce();
    expect(formContext.ui.clearFormNotification).toHaveBeenCalledTimes(1);
  });

  it("does nothing and does not fail when the rule columns are not on the form", () => {
    const formContext = createFormContextMock({ entityName: "ogz_support_ticket" });

    expect(() => load(formContext)).not.toThrow();
    expect(formContext.ui.setFormNotification).not.toHaveBeenCalled();
  });

  it("skips the description rule when Description is missing, and still applies the category rule", () => {
    const formContext = createFormContextMock({
      entityName: "ogz_support_ticket",
      attributes: {
        [Fields.ogz_category]: { value: null, attributeType: "lookup" },
        [Fields.statuscode]: { value: Status.Resolved, attributeType: "optionset" },
      },
    });

    expect(() => load(formContext)).not.toThrow();
    expect(notifications(formContext).has(CATEGORY_NOTIFICATION_ID)).toBe(true);
  });

  it("makes no Web API calls and changes no data", () => {
    const xrm = installXrmMock();
    const formContext = ticketForm({ category: null });

    load(formContext);
    change(formContext, Fields.ogz_category, [HARDWARE]);
    change(formContext, Fields.statuscode, Status.Resolved);

    for (const operation of Object.values(xrm.WebApi)) expect(operation).not.toHaveBeenCalled();
    expect(formContext.getAttribute(Fields.ogz_description)!.setValue).not.toHaveBeenCalled();
    expect(formContext.getAttribute(Fields.ogz_ticket_title)!.setValue).not.toHaveBeenCalled();
    expect(formContext.data.save).not.toHaveBeenCalled();
  });
});

describe("Support Ticket form: OnSave validation", () => {
  const titleForm = (title: string | null) =>
    createFormContextMock({
      entityName: "ogz_support_ticket",
      attributes: { [Fields.ogz_ticket_title]: { value: title, label: "Title", requiredLevel: "required" } },
    });

  const saveTicket = (formContext: Xrm.FormContext) => {
    const context = createSaveEventContextMock(formContext);
    onSave(context);
    return context.getEventArgs();
  };

  const MESSAGE = "Title must contain at least 3 characters.";
  const NOTIFICATION_ID = "ogz_save_ogz_ticket_title_min_length";

  it.each([
    ["exactly 3 characters", "abc"],
    ["3 characters with leading/trailing spaces", "  abc  "],
    ["3 characters with tabs and line breaks around", "\tabc\n"],
    ["a longer title", "Printer offline"],
    ["3 characters with an inner space", "a b"],
  ])("saves a title with %s", (_, title) => {
    const formContext = titleForm(title);

    const args = saveTicket(formContext);

    expect(args.preventDefault).not.toHaveBeenCalled();
    expect(formContext.ui.setFormNotification).not.toHaveBeenCalled();
  });

  it.each([
    ["whitespace only", "   "],
    ["a single space", " "],
    ["tabs and spaces only", "\t \t"],
    ["1 character", "a"],
    ["2 characters", "ab"],
    ["2 characters padded with spaces", "  ab   "],
  ])("blocks a title with %s, with one clear message", (_, title) => {
    const formContext = titleForm(title);

    const args = saveTicket(formContext);

    expect(args.preventDefault).toHaveBeenCalledOnce();
    expect(formContext.ui.setFormNotification).toHaveBeenCalledOnce();
    expect(formContext.ui.setFormNotification).toHaveBeenCalledWith(MESSAGE, "ERROR", NOTIFICATION_ID);
    expect(formContext.getControl<Xrm.Controls.StandardControl>(Fields.ogz_ticket_title)!.setNotification).toHaveBeenCalledWith(MESSAGE, NOTIFICATION_ID);
  });

  it.each([["null", null], ["empty", ""]])("leaves a %s title to Dataverse's required validation and the plug-in", (_, title) => {
    const formContext = titleForm(title);

    const args = saveTicket(formContext);

    expect(args.preventDefault).not.toHaveBeenCalled();
    expect(formContext.ui.setFormNotification).not.toHaveBeenCalled();
    expect(formContext.getAttribute(Fields.ogz_ticket_title)!.getRequiredLevel()).toBe("required");
  });

  it("shows the message once when saving a short title repeatedly", () => {
    const formContext = titleForm("ab");

    saveTicket(formContext);
    saveTicket(formContext);

    expect([...notifications(formContext).keys()]).toEqual([NOTIFICATION_ID]);
  });

  it("clears the title error once the title is fixed", () => {
    const formContext = titleForm(" ");
    saveTicket(formContext);

    formContext.getAttribute(Fields.ogz_ticket_title)!.setValue("Printer offline");
    const args = saveTicket(formContext);

    expect(args.preventDefault).not.toHaveBeenCalled();
    expect(notifications(formContext).size).toBe(0);
  });

  // Regression for DEV: after a blocked save, correcting the title on the same form must clear both
  // notifications and let the next save through.
  describe("recovering on the same form after a blocked save", () => {
    const titleControl = (formContext: Xrm.FormContext) =>
      formContext.getControl(Fields.ogz_ticket_title) as unknown as { notifications: Map<string, string> };
    const type = (formContext: Xrm.FormContext, title: string) => change(formContext, Fields.ogz_ticket_title, title);
    const openNewTicket = (title: string) => {
      const formContext = titleForm(title);
      load(formContext);
      return formContext;
    };

    it("AB -> save blocked -> ABC -> save succeeds, with both notifications cleared", () => {
      const formContext = openNewTicket("AB");

      expect(simulateUserSave(formContext, onSave)).toEqual({ handlerRan: true, saved: false });
      expect(notifications(formContext).get(NOTIFICATION_ID)).toEqual({ message: MESSAGE, level: "ERROR" });
      expect(titleControl(formContext).notifications.get(NOTIFICATION_ID)).toBe(MESSAGE);

      type(formContext, "ABC");
      expect(notifications(formContext).size).toBe(0);
      expect(titleControl(formContext).notifications.size).toBe(0);

      expect(simulateUserSave(formContext, onSave)).toEqual({ handlerRan: true, saved: true });
      expect(notifications(formContext).size).toBe(0);
      expect(titleControl(formContext).notifications.size).toBe(0);
    });

    it("handles repeated invalid/valid transitions without duplicate notifications", () => {
      const formContext = openNewTicket("AB");

      for (const [title, saved] of [["AB", false], ["VPN connection problem", true], ["A", false], ["  x ", false], ["ABC", true]] as const) {
        type(formContext, title);
        expect(simulateUserSave(formContext, onSave).saved, title).toBe(saved);
        expect([...notifications(formContext).keys()], title).toEqual(saved ? [] : [NOTIFICATION_ID]);
        expect([...titleControl(formContext).notifications.keys()], title).toEqual(saved ? [] : [NOTIFICATION_ID]);
      }
    });

    it("keeps the error while the corrected title is still too short", () => {
      const formContext = openNewTicket("A");
      simulateUserSave(formContext, onSave);

      type(formContext, "AB");

      expect(titleControl(formContext).notifications.get(NOTIFICATION_ID)).toBe(MESSAGE);
      expect(simulateUserSave(formContext, onSave).saved).toBe(false);
    });

    it("OnSave alone clears the stale errors, even with a new formContext object and no OnChange", () => {
      const formContext = titleForm("AB");
      onSave(createSaveEventContextMock(rewrapFormContext(formContext)));

      formContext.getAttribute(Fields.ogz_ticket_title)!.setValue("ABC");
      const args = saveTicket(rewrapFormContext(formContext));

      expect(args.preventDefault).not.toHaveBeenCalled();
      expect(notifications(formContext).size).toBe(0);
      expect(titleControl(formContext).notifications.size).toBe(0);
    });
  });

  it("is not registered again from onLoad (it runs from the form's OnSave event)", () => {
    const formContext = ticketForm();

    load(formContext);

    expect(formContext.data.entity.addOnSave).not.toHaveBeenCalled();
  });

  describe("Support Ticket form: critical description hint", () => {
    const HINT_ID = "ogz_support_ticket_critical_description_hint";

    const ticketWithPriority = (priority: Priority | null) =>
      createFormContextMock({
        entityName: "ogz_support_ticket",
        attributes: {
          [Fields.ogz_priority]: { value: priority, attributeType: "optionset" },
          [Fields.ogz_description]: { value: null, attributeType: "memo" },
        },
      });

    const descriptionHints = (formContext: Xrm.FormContext) =>
      (formContext.getControl(Fields.ogz_description) as unknown as { notifications: Map<string, string> }).notifications;

    it("shows the hint next to Description when the form opens as Critical", () => {
      const formContext = ticketWithPriority(Priority.Critical);
      load(formContext);
      expect(descriptionHints(formContext).has(HINT_ID)).toBe(true);
    });
  });
});
