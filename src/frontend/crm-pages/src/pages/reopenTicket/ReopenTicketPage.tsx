// Reopen Ticket dialog: asks for an optional reason and calls the ogz_ReopenSupportTicket Custom API, which
// reopens the ticket (Active / New, description cleared) and adds a timeline note with the reason. The
// command that opened it refreshes the form once it reports success.

import { Button, Field, Text, Textarea } from "@fluentui/react-components";
import { ReopenSupportTicketApi } from "@ogz/crm-client/constants/customApis";
import { ReopenTicketDialog } from "@ogz/crm-client/dialogs/definitions";
import { executeCustomApi, Parameter } from "@ogz/crm-client/services/CustomApiService";
import { DialogPage, useDialog, type UseDialogOptions } from "@ogz/crm-ui";
import { useState } from "react";

export const REASON_MAX_LENGTH = 500;

export function ReopenTicketPage({ options }: { readonly options?: UseDialogOptions }) {
  const dialog = useDialog(ReopenTicketDialog, options);
  const [reason, setReason] = useState("");

  const reopen = () =>
    dialog.run(async () => {
      const trimmed = reason.trim();
      const response = await executeCustomApi<{ ReopenedOn?: string }>({
        name: ReopenSupportTicketApi.name,
        boundTo: { entityName: ReopenSupportTicketApi.boundEntity, id: dialog.input!.ticketId },
        ...(trimmed === "" ? {} : { parameters: { [ReopenSupportTicketApi.parameters.reason]: Parameter.string(trimmed) } }),
      });
      dialog.succeed({ reopenedOn: response.ReopenedOn ?? new Date().toISOString() });
    });

  return (
    <DialogPage
      dialog={dialog}
      title="Reopen ticket"
      doneMessage="The ticket was reopened."
      actions={
        <>
          <Button appearance="primary" disabled={dialog.busy} onClick={() => void reopen()}>
            Reopen
          </Button>
          <Button disabled={dialog.busy} onClick={dialog.cancel}>
            Cancel
          </Button>
        </>
      }
    >
      <Text>
        The ticket becomes active again with status New, and its description is cleared. A note on the timeline
        records who reopened it and when.
      </Text>
      <Field label="Reason" hint="Optional. Added to the timeline note.">
        <Textarea
          value={reason}
          maxLength={REASON_MAX_LENGTH}
          resize="vertical"
          disabled={dialog.busy}
          onChange={(_, data) => setReason(data.value)}
        />
      </Field>
    </DialogPage>
  );
}
