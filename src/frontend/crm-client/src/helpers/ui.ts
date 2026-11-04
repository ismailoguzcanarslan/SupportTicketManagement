// Dialogs and notifications through Xrm.Navigation and formContext.ui.

import { getXrm, toPromise } from "./xrm.js";

export const UI = {
  showAlert(text: string, title?: string): Promise<void> {
    const strings: Xrm.Navigation.AlertStrings = title === undefined ? { text } : { text, title };
    return toPromise(getXrm().Navigation.openAlertDialog(strings)).then(() => undefined);
  },

  /** Resolves to true when the user confirms. */
  async showConfirm(text: string, title?: string): Promise<boolean> {
    const strings: Xrm.Navigation.ConfirmStrings = title === undefined ? { text } : { text, title };
    const result = await toPromise(getXrm().Navigation.openConfirmDialog(strings));
    return result.confirmed;
  },

  showError(message: string, details?: string): Promise<void> {
    const options: Xrm.Navigation.ErrorDialogOptions = details === undefined ? { message } : { message, details };
    return toPromise(getXrm().Navigation.openErrorDialog(options)).then(() => undefined);
  },

  setFormNotification(formContext: Xrm.FormContext, message: string, level: Xrm.FormNotificationLevel, uniqueId: string): boolean {
    return formContext.ui.setFormNotification(message, level, uniqueId);
  },

  clearFormNotification(formContext: Xrm.FormContext, uniqueId: string): boolean {
    return formContext.ui.clearFormNotification(uniqueId);
  },

  showProgress(message: string): void {
    getXrm().Utility.showProgressIndicator(message);
  },

  hideProgress(): void {
    getXrm().Utility.closeProgressIndicator();
  },
} as const;
