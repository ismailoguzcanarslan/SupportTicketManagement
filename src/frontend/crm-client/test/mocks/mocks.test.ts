// Self-tests for the shared Xrm test doubles, so feature tests can rely on them.

import { describe, expect, it, vi } from "vitest";
import { createEventContextMock, createFormContextMock, FormType, installXrmMock } from "./index.js";

describe("createFormContextMock", () => {
  const createForm = () =>
    createFormContextMock({
      entityName: "ogz_support_ticket",
      entityId: "11111111-1111-1111-1111-111111111111",
      attributes: { ogz_title: { value: "Printer offline" } },
    });

  it("exposes entity identity and form type", () => {
    const formContext = createForm();

    expect(formContext.data.entity.getEntityName()).toBe("ogz_support_ticket");
    expect(formContext.data.entity.getId()).toBe("11111111-1111-1111-1111-111111111111");
    expect(formContext.ui.getFormType()).toBe(FormType.Update);
  });

  it("keeps attribute state and tracks dirtiness", () => {
    const formContext = createForm();
    const title = formContext.getAttribute("ogz_title");

    title?.setValue("Printer back online");

    expect(title?.getValue()).toBe("Printer back online");
    expect(formContext.data.entity.getIsDirty()).toBe(true);
    expect(vi.mocked(title!.setValue)).toHaveBeenCalledWith("Printer back online");
  });

  it("returns null for unknown attributes and controls, like the real form", () => {
    const formContext = createForm();

    expect(formContext.getAttribute("ogz_missing")).toBeNull();
    expect(formContext.getControl("ogz_missing")).toBeNull();
  });

  it("creates a control bound to each attribute", () => {
    const formContext = createForm();
    const control = formContext.getControl<Xrm.Controls.StandardControl>("ogz_title")!;

    control.setVisible(false);

    expect(control.getVisible()).toBe(false);
    expect(control.getAttribute()).toBe(formContext.getAttribute("ogz_title"));
    expect(formContext.getAttribute("ogz_title")?.controls.get("ogz_title")).toBe(control);
  });

  it("invokes OnChange handlers with an event context for the form", () => {
    const formContext = createForm();
    const title = formContext.getAttribute("ogz_title")!;
    const handler = vi.fn<Xrm.Events.ContextSensitiveHandler>();

    title.addOnChange(handler);
    title.fireOnChange();

    expect(handler).toHaveBeenCalledOnce();
    const eventContext = handler.mock.calls[0]![0];
    expect(eventContext.getFormContext()).toBe(formContext);
    expect(eventContext.getEventSource()).toBe(title);
  });
});

describe("createEventContextMock", () => {
  it("returns the given form context and stores shared variables", () => {
    const formContext = createFormContextMock();
    const eventContext = createEventContextMock(formContext);

    eventContext.setSharedVariable("key", 42);

    expect(eventContext.getFormContext()).toBe(formContext);
    expect(eventContext.getSharedVariable<number>("key")).toBe(42);
  });
});

describe("installXrmMock", () => {
  it("installs a global Xrm whose methods can be overridden per test", async () => {
    const xrm = installXrmMock();
    vi.mocked(xrm.WebApi.retrieveMultipleRecords).mockResolvedValueOnce({
      entities: [{ ogz_name: "Hardware" }],
    } as Xrm.RetrieveMultipleResult);

    const result = await Xrm.WebApi.retrieveMultipleRecords("ogz_ticket_category");

    expect(result.entities).toHaveLength(1);
    expect(Xrm.Utility.getGlobalContext().userSettings.languageId).toBe(1033);
  });

  it("is removed after each test", () => {
    expect((globalThis as { Xrm?: unknown }).Xrm).toBeUndefined();
  });
});
