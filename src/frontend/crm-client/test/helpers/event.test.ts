import { describe, expect, it, vi } from "vitest";
import { Event } from "../../src/helpers/event.js";
import { createFormContextMock } from "../mocks/index.js";

describe("Event registration", () => {
  it("registers an OnSave handler only once per form", () => {
    const formContext = createFormContextMock();
    const onSave = vi.fn();

    expect(Event.registerOnSave(formContext, onSave)).toBe(true);
    expect(Event.registerOnSave(formContext, onSave)).toBe(false);

    expect(formContext.data.entity.addOnSave).toHaveBeenCalledOnce();
  });

  it("can register the same handler again after unregistering", () => {
    const formContext = createFormContextMock();
    const onSave = vi.fn();

    Event.registerOnSave(formContext, onSave);
    Event.unregisterOnSave(formContext, onSave);

    expect(formContext.data.entity.removeOnSave).toHaveBeenCalledWith(onSave);
    expect(Event.registerOnSave(formContext, onSave)).toBe(true);
  });

  it("registers OnChange once and skips columns that are not on the form", () => {
    const formContext = createFormContextMock({ attributes: { name: {} } });
    const onChange = vi.fn();

    expect(Event.registerOnChange(formContext, "name", onChange)).toBe(true);
    expect(Event.registerOnChange(formContext, "name", onChange)).toBe(false);
    expect(Event.registerOnChange(formContext, "missing", onChange)).toBe(false);

    formContext.getAttribute("name")!.fireOnChange();
    expect(onChange).toHaveBeenCalledOnce();
  });

  it("keeps registrations of different forms apart", () => {
    const first = createFormContextMock();
    const second = createFormContextMock();
    const onSave = vi.fn();

    expect(Event.registerOnSave(first, onSave)).toBe(true);
    expect(Event.registerOnSave(second, onSave)).toBe(true);
  });
});
