// Command bar buttons of the Support Ticket (ogz_support_ticket) main form. Bundled to
// dist/webresources/commands/supportTicket.js and exposed as Ogz.Commands.SupportTicket.
//
// Every button uses the same library and its own function, with PrimaryControl (the form's
// formContext) as its only parameter. Command Designer (JavaScript action):
//   Library   -> ogz_/commands/supportTicket.js
//   Resolve Ticket -> Ogz.Commands.SupportTicket.resolve
//   Reopen Ticket  -> Ogz.Commands.SupportTicket.reopen   (calls the ogz_ReopenSupportTicket Custom API)
//
// Each command lives in its own folder (what to read, check and run + pure validation); the shared flow is
// runRecordCommand (helpers/command.ts). A new command is a new folder and one export line here. The
// exports of this file are the public API of the bundle; everything else stays private.

export { reopen } from "./reopen/reopen.js";
export { resolve } from "./resolve/resolve.js";
