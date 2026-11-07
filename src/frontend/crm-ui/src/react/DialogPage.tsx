// The frame of every dialog page: title, content, an error bar, and the action buttons at the bottom,
// laid out like a Dataverse dialog. It also covers the states every page has: busy, opened without
// input, and finished but still open.
//
//   <DialogPage dialog={dialog} title="Reopen ticket"
//     actions={<><Button appearance="primary" onClick={...}>Reopen</Button><Button onClick={dialog.cancel}>Cancel</Button></>}>
//     ...fields...
//   </DialogPage>

import { Button, MessageBar, MessageBarBody, Spinner, Text, makeStyles, tokens } from "@fluentui/react-components";
import type { ReactNode } from "react";
import type { DialogController } from "./useDialog.js";

const useStyles = makeStyles({
  page: {
    display: "flex",
    flexDirection: "column",
    height: "100%",
    boxSizing: "border-box",
    padding: tokens.spacingHorizontalXL,
    gap: tokens.spacingVerticalL,
    backgroundColor: tokens.colorNeutralBackground1,
  },
  body: {
    flexGrow: 1,
    overflowY: "auto",
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalM,
  },
  actions: {
    display: "flex",
    justifyContent: "flex-end",
    gap: tokens.spacingHorizontalS,
  },
});

export interface DialogPageProps {
  readonly dialog: Pick<DialogController<object, unknown>, "input" | "busy" | "error" | "phase" | "result" | "cancel" | "clearError">;
  readonly title: string;
  /** Buttons at the bottom; the primary action first. Hidden once the dialog ended. */
  readonly actions: ReactNode;
  readonly children: ReactNode;
  /** Shown when the dialog ended but its window did not close. */
  readonly doneMessage?: string;
}

export function DialogPage({ dialog, title, actions, children, doneMessage }: DialogPageProps) {
  const styles = useStyles();

  if (dialog.input === null) {
    return (
      <main className={styles.page}>
        <Text as="h1" size={500} weight="semibold">{title}</Text>
        <MessageBar intent="error">
          <MessageBarBody>This page can only be opened from the app.</MessageBarBody>
        </MessageBar>
      </main>
    );
  }

  if (dialog.phase === "closeManually") {
    const succeeded = dialog.result?.status === "succeeded";
    return (
      <main className={styles.page}>
        <Text as="h1" size={500} weight="semibold">{title}</Text>
        <MessageBar intent={succeeded ? "success" : "info"}>
          <MessageBarBody>
            {succeeded ? (doneMessage ?? "Done.") : "Nothing was changed."} You can close this window.
          </MessageBarBody>
        </MessageBar>
      </main>
    );
  }

  return (
    <main className={styles.page} aria-busy={dialog.busy}>
      <Text as="h1" size={500} weight="semibold">{title}</Text>
      <div className={styles.body}>
        {dialog.error !== null && (
          <MessageBar intent="error">
            <MessageBarBody>{dialog.error}</MessageBarBody>
          </MessageBar>
        )}
        {children}
      </div>
      <footer className={styles.actions}>
        {dialog.busy && <Spinner size="tiny" label="Working..." />}
        {dialog.phase === "open" ? actions : <Button disabled>Closing...</Button>}
      </footer>
    </main>
  );
}
