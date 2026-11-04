// Support Ticket (ogz_support_ticket) main form. Bundled to dist/webresources/forms/supportTicket.js
// and exposed as Ogz.Forms.SupportTicket. Both handlers are registered in the form's event metadata
// (DeploymentHelper manifest), each with "Pass execution context" on:
//   OnLoad  -> Ogz.Forms.SupportTicket.onLoad  (UI rules and their OnChange handlers)
//   OnSave  -> Ogz.Forms.SupportTicket.onSave  (save validation; not also registered from onLoad,
//                                              so it never runs twice)
//
// Nothing here writes data or calls the Web API. The exports of this file are the public API of the
// bundle; everything else stays private.

export { onLoad } from "./onLoad.js";
export { onSave } from "./onSave.js";
export { CATEGORY_NOTIFICATION_ID } from "./rules/formRules.js";
