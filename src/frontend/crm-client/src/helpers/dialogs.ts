// Opens a dialog page (an HTML web resource built by crm-pages) and tells how it ended:
//
//   const result = await Dialogs.open(ReopenTicketDialog, { ticketId: id });
//   if (result.status === "succeeded") ...result.value...
//   // "cancelled": closed with Cancel or X; "failed": could not open, or the page ended with an error
//
// navigateTo resolves when the dialog closes but returns no value for web resources; the page leaves its
// result in sessionStorage under the request id (see @ogz/crm-ui/protocol).

import {
  createRequest,
  encodeRequest,
  takeResult,
  toErrorMessage,
  type DialogDefinition,
  type DialogResult,
} from "@ogz/crm-ui/protocol";
import { getXrm, toPromise } from "./xrm.js";

export const Dialogs = {
  async open<TInput extends object, TResult>(
    definition: DialogDefinition<TInput, TResult>,
    input: TInput,
  ): Promise<DialogResult<TResult>> {
    const request = createRequest(definition, input);
    try {
      const pageInput: Xrm.Navigation.PageInputHtmlWebResource = {
        pageType: "webresource",
        webresourceName: definition.webResource,
        data: encodeRequest(request),
      };
      const options: Xrm.Navigation.NavigationOptions = {
        target: 2,
        position: definition.position === "side" ? 2 : 1,
        ...(definition.width === undefined ? {} : { width: definition.width }),
        ...(definition.height === undefined ? {} : { height: definition.height }),
        ...(definition.title === undefined ? {} : { title: definition.title }),
      };
      await toPromise(getXrm().Navigation.navigateTo(pageInput, options));
    } catch (error) {
      return { status: "failed", error: toErrorMessage(error) };
    }
    return takeResult<TResult>(request.requestId);
  },
} as const;
