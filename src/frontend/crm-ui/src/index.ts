// Shared React + Fluent UI 2 building blocks for pages (dialogs now, a code app later). Pages import from
// "@ogz/crm-ui"; code that only opens dialogs imports "@ogz/crm-ui/protocol", which has no React.

export * from "./protocol/index.js";
export { DialogPage, type DialogPageProps } from "./react/DialogPage.js";
export { connectXrm, mountPage } from "./react/mount.js";
export { OgzThemeProvider, ogzTheme } from "./react/theme.js";
export { useDialog, type DialogController, type DialogPhase, type UseDialogOptions } from "./react/useDialog.js";
