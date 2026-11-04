# Support Ticket Management

A support ticket management system built on **Microsoft Dataverse / Dynamics 365**.
It is a long-term learning and portfolio project.

| | |
| --- | --- |
| Dataverse solution (unique name) | `OguzLabsSupportTicketManagement` |
| Publisher prefix | `ogz` |
| Core tables | `ogz_support_ticket`, `ogz_ticket_category` |

## Architecture

```
                         Dataverse metadata
                                │
                SupportTicket.EntityGenerator  (.NET 10, dev-time)
                 │                                  │
      crm-entities (TS constants)        SupportTicket.Entities (net462, early-bound)
                 │                                  │
      crm-client: forms ─ helpers ─ rules   SupportTicket.BusinessLogic (net462)
                 │ esbuild                          │
      dist/webresources/*.js            SupportTicket.Base ── SupportTicket.Plugins (net462)
                 │                                  │ plug-in package (.nupkg)
                 └──────── SupportTicket.DeploymentHelper (.NET 10, DEV only) ────────► Dataverse
```

Plug-in flow: Dataverse event → plug-in class (registration attributes) → `BasePlugin` → business logic → Dataverse services.

## Repository layout

```
.
├── src/
│   ├── frontend/                          npm workspaces (TypeScript)
│   │   ├── crm-entities/                  generated TS entity definitions
│   │   └── crm-client/                    Web Resources: forms, helpers, rules
│   ├── dataverse/                         code that runs inside Dataverse (net462)
│   │   ├── SupportTicket.Base/            BasePlugin, PluginContext, registration attributes
│   │   ├── SupportTicket.BusinessLogic/   business rules, no plug-in infrastructure
│   │   ├── SupportTicket.Entities/        generated C# early-bound entities
│   │   └── SupportTicket.Plugins/         thin plug-in entry points; builds the plug-in package
│   └── tools/                             developer tools (.NET 10)
│       ├── SupportTicket.Tools.Dataverse/ shared Dataverse connection (OGZ_DATAVERSE_* variables)
│       ├── SupportTicket.EntityGenerator/
│       └── SupportTicket.DeploymentHelper/
├── tests/
│   ├── SupportTicket.PluginTests/          Base, BusinessLogic and plug-in tests (net462)
│   ├── SupportTicket.DeploymentHelperTests/ planners, manifest, FormXML, discovery (offline)
│   └── SupportTicket.EntityGeneratorTests/ offline generator tests + fixtures
├── SupportTicketManagement.sln
├── package.json · vitest.config.mts · tsconfig.base.json
├── Directory.Build.props · Directory.Packages.props · global.json · coverage.runsettings
```

### Project references

| Project | References |
| --- | --- |
| SupportTicket.Base | `Microsoft.CrmSdk.CoreAssemblies` |
| SupportTicket.BusinessLogic | Entities (deliberately not Base) |
| SupportTicket.Plugins | Base, BusinessLogic, Entities (packed into its plug-in package) |
| SupportTicket.EntityGenerator | Tools.Dataverse, `Microsoft.PowerPlatform.Dataverse.Client` |
| SupportTicket.DeploymentHelper | Tools.Dataverse, `Microsoft.PowerPlatform.Dataverse.Client`, `System.Reflection.MetadataLoadContext` |

DeploymentHelper does not reference Base: it reads the registration attributes from the built
plug-in DLL by name, so its .NET 10 Dataverse client never mixes with the net462 SDK.

### Reserved for later phases (not scaffolded yet)

| Location | Content |
| --- | --- |
| `src/apps/team-lead-app/` | React + TypeScript Power Apps Code App for Team Leads |
| `src/azure/` | Azure Functions and Azure integration services (DeploymentHelper option 3) |
| `pipelines/` | Azure DevOps YAML pipelines |

## Generated vs. handwritten code

Generated code lives **only** in `src/frontend/crm-entities/src/generated/` and
`src/dataverse/SupportTicket.Entities/GeneratedCode/`, and is never edited by hand.

## Target frameworks

| Project | Target | Why |
| --- | --- | --- |
| Base, BusinessLogic, Entities, Plugins | `net462` | The Dataverse sandbox only runs .NET Framework 4.6.2 plug-ins. |
| SupportTicket.PluginTests | `net462` | Same framework surface as the plug-ins. |
| Tools, DeploymentHelper, their tests | `net10.0` | Developer tools, never deployed to Dataverse. |

## Plug-in development

### Packaging

The plug-in code spans four assemblies. A classic plug-in assembly cannot bring dependent DLLs,
so `SupportTicket.Plugins` is built as a **plug-in package** (`Microsoft.PowerApps.MSBuild.Plugin`).
Every build produces `bin/<Configuration>/ogz_SupportTicketPlugins.<Version>.nupkg`. It contains
only `SupportTicket.Plugins/Base/BusinessLogic/Entities.dll`. The Dataverse SDK and its dependencies
are excluded, because the sandbox provides them. Package assemblies are not signed (not required for
plug-in packages). The package is rebuilt whenever one of those DLLs changes.

**Version policy** (see `src/dataverse/Directory.Build.props`):

| Version | Rule |
| --- | --- |
| Package `<Version>` (`1.0.0`) | **Never change it.** Dataverse does not allow changing a package's name or version after creation; updates only replace the content. DeploymentHelper blocks an update with a different version. |
| `AssemblyVersion` (`1.0.0.0`) | Fixed for all four assemblies; the build fails if a project changes it. Dataverse does not need it to change, and a stable identity avoids binding mismatches. |
| `FileVersion` | Free to change to identify a build. |

Before uploading, DeploymentHelper checks the built package:
- **Contents:** package id, no NuGet dependencies, `lib/net462` only, and no Dataverse SDK DLLs.
- **Assemblies:** each under 16 MB, no custom workflow activities, no use of SDK dependencies left out of the package, and no signed assembly depending on an unsigned one.
- **Freshness:** the packaged `SupportTicket.Plugins.dll` is byte-identical to the DLL whose attributes are deployed.
- **Removed types:** Dataverse rejects an update that removes a plug-in class that still has steps, so the tool blocks that case too.

### Declaring registrations

```csharp
[PluginStep(MessageName.Create, ogz_support_ticket.EntityLogicalName, PluginStage.PreValidation,
    executionOrder: 1, async: false)]
[PluginStep(MessageName.Update, ogz_support_ticket.EntityLogicalName, PluginStage.PreValidation,
    executionOrder: 1, async: false, FilteringAttributes = ogz_support_ticket.Fields.ogz_ticket_title)]
public sealed class SupportTicketValidationPlugin : BasePlugin { ... }

// Custom API / Custom Action (the message must already exist in Dataverse):
[ActionStep("ogz_ProcessSupportTicket", "ogz_support_ticket", PluginStage.PostOperation, executionOrder: 1)]
// Make the plug-in the Custom API's main handler instead of a step. The table must match the
// Custom API's binding (null for an unbound API); stage, images and filtering do not apply:
[ActionStep("ogz_ProcessSupportTicket", null, PluginStage.PostOperation, IsMainOperation = true)]
```

Optional named properties: `FilteringAttributes`, `PreImageName`/`PreImageAttributes`,
`PostImageName`/`PostImageAttributes`, `Name`, `Description`, `DeleteAsyncOperationIfSuccessful`.
Attribute lists are comma-separated; empty means all attributes. `PluginStep` always needs a table;
only `ActionStep` can be unbound. There is no run-as user setting: user ids differ per environment,
so steps run as the calling user and the tool leaves a manually set run-as user untouched.

### BasePlugin and business logic

```csharp
public sealed class SupportTicketValidationPlugin : BasePlugin
{
    private static readonly SupportTicketBusinessLogic BusinessLogic = new();

    protected override void ExecutePlugin(PluginContext context)
    {
        var ticket = context.GetTarget<ogz_support_ticket>();      // early-bound Target
        var result = BusinessLogic.ValidateNewTicket(ticket);      // pure, unit-tested on its own
        if (!result.IsValid) throw new InvalidPluginExecutionException(string.Join(" ", result.Errors));
    }
}
```

`PluginContext` provides the execution context, tracing, user and system organization services
(created lazily), `InputParameters`/`OutputParameters`, `Target`/`TargetReference`,
`GetPreImage<T>`/`GetPostImage<T>`, `Depth` and `CorrelationId`. `BasePlugin` traces one entry line,
rethrows `InvalidPluginExecutionException` unchanged, and wraps unexpected errors in one that carries
the correlation id. Exceptions are never swallowed, so synchronous steps still roll back. Plug-in
instances are shared between threads: keep no per-execution state in fields.

## Frontend: forms, rules, helpers and services

The frontend separates **what a form does** (per table) from **how it is done** (shared, generic):

```
crm-client/src/
├── forms/<table>/          what this form does — table-specific, the only place with field names and messages
│   ├── index.ts            public API of the bundle: exports onLoad and onSave
│   ├── onLoad.ts           FormRules.initialize(formContext, [...formRules, SaveRules.revalidateOnChange(saveRules)])
│   ├── onSave.ts           SaveRules.handle(executionContext, saveRules)
│   └── rules/
│       ├── formRules.ts    list of form rules (OnLoad + OnChange)
│       └── saveRules.ts    list of save checks (OnSave)
├── rules/                  how rules work — generic, no table or field names
│   ├── FormRule.ts         FormRule shape { dependsOn, apply } + conditions (isEmpty, hasValue, matches)
│   ├── Rules.ts            ready-made form rules
│   ├── FormRules.ts        engine: applies form rules and registers their OnChange handlers once
│   ├── SaveRule.ts         SaveRule shape { id, fields, severity, validate } + ready-made save checks
│   ├── SaveRules.ts        engine: runs save checks, shows/clears notifications, cancels the save
│   └── index.ts            the single import point for forms
├── helpers/                safe wrappers around the Xrm Client API (XrmHelper)
├── services/               Web API access (EntityService)
└── commands/               command bar entry points (none yet)
```

`onLoad.ts`, `onSave.ts` and `index.ts` look the same for every table and rarely change: a new
requirement is one more entry in `rules/formRules.ts` or `rules/saveRules.ts`, for example:

```ts
// forms/supportTicket/rules/formRules.ts
FieldNotificationRule({
  field: ogz_support_ticketFields.ogz_priority,
  showWhen: ogz_support_ticket_ogz_priority.Critical,
  target: ogz_support_ticketFields.ogz_description,
  message: "Critical tickets need a detailed description.",
  id: "ogz_support_ticket_critical_description_hint",
}),

// forms/supportTicket/rules/saveRules.ts
MinTextLengthRule({ field: ogz_support_ticketFields.ogz_ticket_title, minLength: 3, message: "Title must contain at least 3 characters." }),
```

When no ready-made rule fits, add a generic one to `rules/Rules.ts` (form rules) or
`rules/SaveRule.ts` (save checks), with everything table-specific passed in as parameters.

- **Form rules** (`Rules.ts`): `FormNotificationRule`, `FieldNotificationRule`, `FieldRequiredRule`
  (restores each column's original required level when the condition ends), `FieldVisibilityRule`,
  `FieldDisabledRule`, `SectionSpecificVisibilityRule`. Conditions are a value, a list of values, a
  predicate, or `isEmpty`/`hasValue`. Control rules accept a column/control name or a structured
  `{ tab, section, field }` target, resolved against the real form layout; a missing target is skipped.
  `FieldNotificationRule` defaults to a RECOMMENDATION (`addNotification`), which never blocks saving.
- **Save checks** (`SaveRule.ts`): `MinTextLengthRule`, `RequiredWhenRule`, `NotWhitespaceRule`.
  Severity `"error"` (default) shows an ERROR on the form and the field and cancels the save;
  `"info"` shows an INFO notification and a RECOMMENDATION and lets the save go on. A rule that throws
  always blocks the save. Errors are re-checked when their columns change and cleared once fixed,
  because Dataverse blocks every save while a field shows an error notification.
- **Several forms per table:** `selectRuleSet(formContext, { default, byFormId })` picks the rule set
  registered for the form being shown.
- **Helpers** (`helpers/`, also as `XrmHelper`): `Data` (Xrm.WebApi CRUD), `Form`, `App`, `Field`,
  `Control`, `Lookup`, `Tab`, `Section`, `Event`, `UI`. Missing columns, controls, tabs and sections
  are ignored and reported through `false`/`null` return values.
- **Services** (`services/EntityService.ts`): `EntityService<T>("table")` with `create`, `retrieve`,
  `retrieveMultiple` (`select`/`filter`/`orderBy`/`top`), `update` and `delete`; `T` can be a generated
  record type. Asynchronous, so not usable inside save checks.
- **Events:** OnLoad and OnSave are registered in the form metadata through the Deployment Helper
  manifest (see below); OnChange handlers are registered by `FormRules`, never twice.
- **Bundling** (`crm-client/build.mjs`, esbuild): every `src/forms/<name>/index.ts` (or
  `src/forms/<name>.ts`, same for `src/commands`) becomes one self-contained IIFE in
  `dist/webresources/forms|commands/<name>.js`, including every module it imports. Only the entry's
  exports become public, under `Ogz.Forms.<Name>` / `Ogz.Commands.<Name>`. `Xrm` is the platform's
  global and is never bundled. `crm-entities` is bundled from source, so Choice values are inlined and
  unused enums dropped.

## Deployment Helper (DEV only)

```bash
dotnet build SupportTicketManagement.sln -c Release
```
```bash
npm run build
```
```bash
dotnet run --project src/tools/SupportTicket.DeploymentHelper -c Release -- --dry-run
```

```
====================================
 SupportTicket Deployment Helper
====================================

1 - Deploy Web Resources
2 - Deploy Plug-ins & Actions
3 - Deploy Azure Functions (Coming Soon)
0 - Exit
```

**Setup:**
- **Connection:** both tools use the same environment variables, read by
  `SupportTicket.Tools.Dataverse`:

  | Variable | Required | Meaning |
  | --- | --- | --- |
  | `OGZ_DATAVERSE_URL` | yes | Environment URL (https) |
  | `OGZ_DATAVERSE_CLIENT_ID` | yes | Application (client) id for the interactive Microsoft sign-in |
  | `OGZ_DATAVERSE_AUTH_TYPE` | no | Only `Interactive` (default) is supported |
  | `OGZ_DATAVERSE_REDIRECT_URI` | no | Defaults to `http://localhost` |
  | `OGZ_DATAVERSE_TENANT_ID`, `OGZ_DATAVERSE_CLIENT_SECRET` | no | Read but not used by the interactive flow |

- **DEV only, and what that means:** `targetEnvironment` must be `DEV`, and there is no way to
  choose another environment. Every write needs you to type `DEV` after the tool has shown the
  connected environment's name, URL and organization id. The tool **cannot prove** that
  `OGZ_DATAVERSE_URL` is your DEV environment; check the printed environment before confirming.
- No URLs, credentials or secrets are stored in files.

**Main solution:** everything is deployed into `solutionUniqueName` (`OguzLabsSupportTicketManagement`).
Before planning, the tool checks that the solution exists (it never creates one), is unmanaged, is
not the Default or another system solution, and belongs to the `ogz` publisher.

| Component | How it joins the solution |
| --- | --- |
| Plug-in package | Created in the solution, or added if it already exists elsewhere |
| Plug-in assemblies and types | Owned by the package; not added separately |
| Steps | Created in the solution, or added (with their images) if they exist but are not in it |
| Step images | Created in the solution; otherwise included through their step |
| Custom APIs | Never added: they are not owned by this deployment |
| Web Resources | Created in the solution, or added if they exist but are not in it |
| Forms (only those listed in `forms`) | Added if not in the solution yet |

Components already in the solution are never added again.

**Every option:**
1. Option 1 first builds the TypeScript: `npm run typecheck`, then `npm run build`, at the
   repository root (5-minute timeout each, no npm install or update). Any type or esbuild error, a
   missing `npm`, or a manifest bundle that this build did not write stops the run before
   Dataverse is touched, so a stale bundle is never deployed. This also happens in `--dry-run`.
2. Validates the manifest (`deployment.manifest.json`) and, for plug-ins, the package and attributes.
3. Connects, shows the environment, and validates the main solution.
4. Prints the plan: `[Create]`/`[Update]`/`[Unchanged]`/`[Blocked]` per component, then `[Solution]`
   lines for every planned solution membership change.
5. Applies changes only after you type `DEV`.
6. Reads Dataverse again and verifies that every deployed component exists and is in the solution.
   Any problem is printed as `VERIFICATION FAILED`.

`--dry-run` stops after step 4: local builds and read-only queries only. Nothing runs when the
console starts.

- **1 - Web Resources:**
  - Creates missing Web Resources in the solution, updates changed ones (byte comparison), skips
    unchanged ones, and publishes only what changed.
  - Updates a display name only when the manifest sets `displayName`; without it, the name in
    Dataverse is left alone.
  - Then registers form libraries and handlers for the `forms` entries and adds those forms to the
    solution, after a second confirmation.
- **2 - Plug-ins & Actions:**
  - Uploads or updates the plug-in package and reads `PluginStep`/`ActionStep` attributes from the
    built DLL.
  - Creates or updates steps and images, matched by plug-in type + message + table + stage.
  - Links Custom API main handlers only after a separate confirmation.
  - Messages that do not exist are *blocked*, never created. Obsolete steps and images are reported,
    never deleted.
- **3 - Azure Functions:** placeholder. Implement `AzureFunctionsDeploymentTarget` to enable it;
  new menu options are new `IDeploymentTarget` classes registered in `Program.cs`.

### Form OnLoad registration

Uploading a Web Resource does not register it on a form. A `forms` entry tells the tool which
library and handlers to add. There are two ways to choose the target forms:

**Choose interactively (no form id):** name the table and form type. On every run, also in
`--dry-run`, the tool lists the active forms of that type and asks which to modify, by number:
`1`, or `1,2` for several, or Enter for none. Nothing is selected automatically, even when only
one form exists, and the choice applies to that run only. The selected forms are printed as
`[Selected]` lines before the plan.

```json
"forms": [
  {
    "entity": "ogz_support_ticket",
    "formType": "main",
    "library": "ogz_/forms/supportTicket.js",
    "events": [
      { "event": "onload", "function": "Ogz.Forms.SupportTicket.onLoad", "passExecutionContext": true },
      { "event": "onsave", "function": "Ogz.Forms.SupportTicket.onSave", "passExecutionContext": true }
    ]
  }
]
```

**Fixed form (for automation):** add `"formId": "<form-guid>"` instead of `formType`. The id is
printed next to each form in the interactive list.

The tool never looks forms up by name. It checks that the form belongs to the table and is
customizable, then adds only the missing `<Library>` and `<Handler>` nodes. Everything else in the
FormXML is left unchanged, and a second run changes nothing. It refuses FormXML it does not
understand. The form is then added to the solution and its table is published. Only `onload` and
`onsave` are supported here; OnChange belongs in the form script.

## Testing

All automated tests are offline: none connects to Dataverse, opens a sign-in or waits for console
input (the test runner closes stdin; the generator menu and the deployment console both exit at end
of input).

| Command (repo root) | What it does |
| --- | --- |
| `npm run test` | All frontend tests (`crm-entities`, `crm-client`): XrmHelper, rules, form scripts, bundles, type check. |
| `npm run test:watch` | Re-runs affected frontend tests on change. |
| `npm run test:coverage` | Frontend tests with V8 coverage to `coverage/`. |
| `npm run typecheck` | Strict type check of sources and tests. |
| `npm run build` | Type check, then esbuild bundles to `src/frontend/crm-client/dist/webresources`. |
| `dotnet test tests/SupportTicket.PluginTests` | BusinessLogic, BasePlugin/PluginContext, registration attributes, plug-ins (net462, Windows). |
| `dotnet test tests/SupportTicket.DeploymentHelperTests` | Manifest, build runner, planning, solution handling, FormXML, safety, Web Resource pipeline end to end (in memory). Builds SupportTicket.Plugins first. |
| `dotnet test tests/SupportTicket.EntityGeneratorTests` | Generator menu parsing and contracts (offline). |
| `dotnet test <project> --collect "Code Coverage" --settings coverage.runsettings --results-directory TestResults/<project>` | C# coverage (Cobertura) per project. |

Run all safe automated tests (frontend, then each .NET test project):

```bash
npm run test && dotnet test tests/SupportTicket.PluginTests && dotnet test tests/SupportTicket.DeploymentHelperTests && dotnet test tests/SupportTicket.EntityGeneratorTests
```

- Frontend tests live in each package's `test/` folder, outside `src/`, so they never reach a bundle.
  Inputs that must fail (broken import, type error, no entries) are in `crm-client/test-fixtures/`.
- Shared `Xrm`/`formContext` mocks are in `src/frontend/crm-client/test/mocks`. They support
  attributes, controls, tabs, sections, the form selector, OnSave/OnChange and a global `Xrm`.
- `tests/SupportTicket.PluginTests/Infrastructure/PluginTestContext` wires up one pipeline execution.
- `tests/SupportTicket.DeploymentHelperTests/Fakes/InMemoryDataverse` stands in for Dataverse at the
  repository boundary and records every write, so tests prove what a run did or did not change.

## Secrets

No connection strings, client secrets or tokens are committed. Credentials belong in environment
variables (`OGZ_DATAVERSE_*`), `dotnet user-secrets` or `pac auth` profiles.
