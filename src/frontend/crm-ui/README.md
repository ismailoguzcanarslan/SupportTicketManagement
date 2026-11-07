# @ogz/crm-ui

Shared building blocks for React pages on Dataverse, styled with **Fluent UI 2**
(`@fluentui/react-components`), the design system of model-driven apps. Consumed from source by
`crm-pages` (and later a code app); nothing is built here.

| Import | Contents |
| --- | --- |
| `@ogz/crm-ui/protocol` | No React. `defineDialog`, the dialog request/result protocol, `toErrorMessage`. Used by form scripts (`crm-client`). |
| `@ogz/crm-ui` | The protocol plus `OgzThemeProvider` (theme of every page), `DialogPage` (title, content, error bar, actions; busy, opened-without-input and finished states), `useDialog` (input, `run`, `succeed`, `cancel`, `fail`) and `mountPage`. |

How a dialog works:

1. `Dialogs.open(definition, input)` (crm-client) opens `ogz_/pages/<page>.html` with
   `Xrm.Navigation.navigateTo`, sending `{ v, requestId, page, input }` as the `data` parameter.
2. The page (`useDialog`) reads the input from its URL, does its work (e.g. a Custom API call) and ends
   with `succeed(value)`, `cancel()` or `fail(error)`: the result goes to `sessionStorage` under the
   request id, then the page calls `window.close()`. If the window stays open, `DialogPage` asks the
   user to close it.
3. When the dialog closes, `Dialogs.open` returns the stored result, or `cancelled` when there is none
   (closed with X).

Keep inputs small (ids, short values): they travel in the URL.
