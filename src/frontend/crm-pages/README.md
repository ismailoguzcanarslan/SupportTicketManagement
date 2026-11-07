# @ogz/crm-pages

Dialog pages built with React 18 and Fluent UI 2 (via `@ogz/crm-ui`), deployed as HTML Web Resources.

| Source | Web Resources |
| --- | --- |
| `src/pages/reopenTicket/index.tsx` | `ogz_/pages/reopenTicket.html` + `ogz_/pages/reopenTicket.js` |

New page:

1. Add its definition (input and result types) to `crm-client/src/dialogs/definitions.ts`.
2. Add `src/pages/<name>/<Name>Page.tsx` using `useDialog(definition)` and `DialogPage`, and
   `src/pages/<name>/index.tsx` with `mountPage(<NamePage />)`.
3. Add both Web Resources to `deployment.manifest.json`.
4. Open it from a form script or command with `Dialogs.open(definition, input)`.

`npm run build` bundles each page into one minified script (React, Fluent UI and the page) and writes
its HTML. Pages use `crm-client` services (e.g. `executeCustomApi`); `mountPage` copies the app's `Xrm`
from the parent window, so they work as in form scripts.
