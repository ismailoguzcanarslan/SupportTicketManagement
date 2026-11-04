// Account main form. Bundled to dist/webresources/forms/account.js and exposed as Ogz.Forms.Account.
// Both handlers are registered in the form's event metadata (DeploymentHelper manifest), each with
// "Pass execution context" on:
//   OnLoad  -> Ogz.Forms.Account.onLoad  (UI rules and their OnChange handlers)
//   OnSave  -> Ogz.Forms.Account.onSave  (save checks; not also registered from onLoad)
//
// The exports of this file are the public API of the bundle.

export { onLoad } from "./onLoad.js";
export { onSave } from "./onSave.js";
