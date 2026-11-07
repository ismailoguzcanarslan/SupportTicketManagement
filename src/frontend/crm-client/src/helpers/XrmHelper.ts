// Single entry point for the Client API helpers: `XrmHelper.Field.getValue(formContext, ...)`.
// Importing XrmHelper bundles all helper modules (a few KB). A form that imports individual
// modules (e.g. `import { Field } from "../helpers/field.js"`) bundles only those.
//
// Two styles, same behavior:
//   Field.setRequired(formContext, { tab: "SUMMARY_TAB", section: "CONTACT", field: "telephone1" }, true);
//   FormModel(formContext).section("SUMMARY_TAB", "CONTACT").field("telephone1").setRequired(true);

import { App } from "./app.js";
import { Control } from "./control.js";
import { Data } from "./data.js";
import { Diagnostics } from "./diagnostics.js";
import { Dialogs } from "./dialogs.js";
import { Event } from "./event.js";
import { Field } from "./field.js";
import { Form } from "./form.js";
import { FormModel } from "./formModel.js";
import { Grid } from "./grid.js";
import { Lookup } from "./lookup.js";
import { Section, Tab } from "./tab.js";
import { UI } from "./ui.js";

export const XrmHelper = { App, Control, Data, Diagnostics, Dialogs, Event, Field, Form, FormModel, Grid, Lookup, Section, Tab, UI } as const;

export { App, Control, Data, Diagnostics, Dialogs, Event, Field, Form, FormModel, Grid, Lookup, Section, Tab, UI };
export { Event as FormEvents };
export { FormType } from "./form.js";
export type { WebApiRecord } from "./data.js";
export type { FieldRef } from "./field.js";
export type { FieldModel, GridModel, SectionModel, TabModel } from "./formModel.js";
export type { ControlTarget, FieldTarget, SectionTarget } from "./target.js";
