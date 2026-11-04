// Runtime registration of OnSave/OnChange handlers. The initial OnLoad handler is registered in the
// form's metadata (form editor or DeploymentHelper); everything else is registered from OnLoad here.
//
// Each handler is registered at most once per form/column, so calling OnLoad again (e.g. after a
// form refresh or switching forms) never stacks duplicate handlers.

type SaveHandler = Xrm.Events.SaveEventHandler;
type ChangeHandler = Xrm.Events.Attribute.ChangeEventHandler;

// Keyed by the Client API object the handler is attached to; entries disappear with the form.
const registered = new WeakMap<object, Set<unknown>>();

function track(owner: object, handler: unknown): boolean {
  let handlers = registered.get(owner);
  if (handlers === undefined) {
    handlers = new Set();
    registered.set(owner, handlers);
  }
  if (handlers.has(handler)) return false;
  handlers.add(handler);
  return true;
}

function untrack(owner: object, handler: unknown): boolean {
  return registered.get(owner)?.delete(handler) ?? false;
}

export const Event = {
  /** Returns false when the handler was already registered on this form. */
  registerOnSave(formContext: Xrm.FormContext, handler: SaveHandler): boolean {
    const entity = formContext.data.entity;
    if (!track(entity, handler)) return false;
    entity.addOnSave(handler);
    return true;
  },

  unregisterOnSave(formContext: Xrm.FormContext, handler: SaveHandler): void {
    const entity = formContext.data.entity;
    if (untrack(entity, handler)) entity.removeOnSave(handler);
  },

  /** Returns false when the column is not on the form or the handler was already registered. */
  registerOnChange(formContext: Xrm.FormContext, attributeName: string, handler: ChangeHandler): boolean {
    const attribute = formContext.getAttribute(attributeName);
    if (attribute === null || !track(attribute, handler)) return false;
    attribute.addOnChange(handler);
    return true;
  },

  unregisterOnChange(formContext: Xrm.FormContext, attributeName: string, handler: ChangeHandler): void {
    const attribute = formContext.getAttribute(attributeName);
    if (attribute !== null && untrack(attribute, handler)) attribute.removeOnChange(handler);
  },
} as const;
