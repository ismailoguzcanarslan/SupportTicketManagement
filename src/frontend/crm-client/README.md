# @ogz/crm-client

TypeScript sources for the Support Agent Model-driven App's JavaScript Web Resources.

| Folder | Purpose |
| --- | --- |
| `src/forms/<table>/` | One folder per form: `index.ts` (bundle entry, exports `onLoad`/`onSave`), `onLoad.ts`, `onSave.ts`, and `rules/formRules.ts` + `rules/saveRules.ts` with the table's rules. |
| `src/rules/` | Generic rule engine: form rules (`FormRule`, `Rules`, `FormRules`) and save checks (`SaveRule`, `SaveRules`). No table or field names. |
| `src/helpers/` | `XrmHelper`: Data, Form, App, Field, Control, Lookup, Tab, Section, Grid, Event, UI, the fluent `FormModel`, and `runRecordCommand` (`command.ts`) for command buttons. |
| `src/services/` | Web API access: `EntityService<T>("table")` and `executeCustomApi` on top of `XrmHelper.Data`. |
| `src/commands/` | Command bar entry points, one folder per table (`supportTicket/`: resolve, reopen). |
| `src/constants/` | Handwritten constants (publisher prefix, solution name, ...). |
| `src/types/` | Handwritten shared types. Entity types come from `@ogz/crm-entities`. |

New requirement: add an entry to the table's `rules/formRules.ts` or `rules/saveRules.ts`. Only when
no ready-made rule fits, add a generic one to `src/rules/Rules.ts` or `src/rules/SaveRule.ts`.

## Form helpers

Every helper takes a column/control name or a `{ tab, section, field }` target. Value, requirement
level, dirty state and submit mode belong to the column (one per form, however often it is placed); a
section target only says which column. Visibility, disabled, label, focus, notifications, choice options
and lookup filters belong to a control, so a section target changes only the control in that section.
Anything not on the form is ignored: setters return `false`, getters `null`.

```ts
// Static helpers (used by the rule engine)
Field.setRequired(formContext, { tab: "SUMMARY_TAB", section: "CONTACT", field: "telephone1" }, true);
Control.setOptions(formContext, Fields.ogz_priority, [Priority.High, Priority.Critical]);

// The same through the fluent FormModel (handler code)
const form = FormModel(formContext);
form.field(Fields.ogz_priority).getText();                       // "Critical"
const contact = form.tab("SUMMARY_TAB").section("CONTACT");
contact.field("telephone1").setRequired(true).setLabel("Mobile").focus();
contact.getMissingRequired();                                    // ["telephone1"]
form.field(Fields.ogz_category).setLookupFilter(`<filter>...</filter>`, "ogz_ticket_category");
form.grid("notes_grid").refresh();
```

Rules re-run after a save and after a refresh (data OnLoad), not only on form load. `FieldDisabledRule`
and `FieldRequiredRule` restore what the form designer configured when their condition no longer
matches, so they never unlock a field that is read-only on the form or anything on a read-only form.
A `SaveRule` can list the `saveModes` it runs on (e.g. not on `SaveMode.Deactivate`); "info" rules skip
AutoSave by default.

Rules and helpers skip whatever is not on the form. To see what was skipped (e.g. a mistyped tab or
section name), run `localStorage.setItem("ogz_diagnostics", "1")` in the browser console and reload the
form; `[ogz]` warnings appear in the console.

## Build

`build.mjs` uses **esbuild** to bundle every entry point, together with everything it imports, into
one self-contained IIFE. An entry point is a folder with an `index.ts` (or a single `.ts` file)
directly under `src/forms` or `src/commands`. `tsc` runs first as a type check only (`noEmit`).

| Source | Output | Global name |
| --- | --- | --- |
| `src/forms/account/index.ts` | `dist/webresources/forms/account.js` | `Ogz.Forms.Account` |
| `src/forms/supportTicket/index.ts` | `dist/webresources/forms/supportTicket.js` | `Ogz.Forms.SupportTicket` |

Exported functions of an entry become members of its global name, so a form event is registered in
Dataverse as e.g. `Ogz.Forms.Account.onLoad` ("Pass execution context"). Helpers and rules stay
private to the bundle. Web Resource names: `ogz_/forms/<name>.js`, `ogz_/commands/<name>.js` (see
`deployment.manifest.json`). Renaming a form folder renames its global and breaks existing form
event registrations.

- `npm run build`: type check + minified production bundles.
- `npm run build:dev`: type check + unminified bundles with inline source maps.
- `npm run watch`: rebuild on change (no type check).

## Tests

Tests live in `test/` (outside `src/`, so they are never bundled). Shared `Xrm` and `formContext`
test doubles are in `test/mocks`, including `simulateUserSave` (a save as Dataverse runs it) and
`rewrapFormContext`. `test/build/bundle.test.ts` builds the real bundles and checks their global
namespace. Run `npm test`, `npm run test:watch` or `npm run test:coverage`.

Run one file or the tests whose name contains a text (case-sensitive):

```bash
npx vitest run test/forms/supportTicket.test.ts -t "OnSave validation"
```

In PowerShell with script execution disabled, use `npx.cmd` / `npm.cmd`.
