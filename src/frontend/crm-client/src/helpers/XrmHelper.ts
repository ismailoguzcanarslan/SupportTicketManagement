// Single entry point for the Client API helpers: `XrmHelper.Field.getValue(formContext, ...)`.
// Importing XrmHelper bundles all helper modules (a few KB). A form that imports individual
// modules (e.g. `import { Field } from "../helpers/field.js"`) bundles only those.

import { App } from "./app.js";
import { Control } from "./control.js";
import { Data } from "./data.js";
import { Event } from "./event.js";
import { Field } from "./field.js";
import { Form } from "./form.js";
import { Lookup } from "./lookup.js";
import { Section, Tab } from "./tab.js";
import { UI } from "./ui.js";

export const XrmHelper = { App, Control, Data, Event, Field, Form, Lookup, Section, Tab, UI } as const;

export { App, Control, Data, Event, Field, Form, Lookup, Section, Tab, UI };
export { Event as FormEvents };
export { FormType } from "./form.js";
export type { WebApiRecord } from "./data.js";
