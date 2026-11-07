// State of a dialog page: its input, a busy flag and error for its work, and how it ends.
//
//   const dialog = useDialog(ReopenTicketDialog);
//   dialog.input?.ticketId                      // typed; null when the page was not opened by Dialogs.open
//   dialog.run(async () => { ...; dialog.succeed(value); })   // busy while running, errors shown
//   dialog.cancel()
//
// Ending writes the result for the opener and closes the window. Closing a web resource dialog from
// inside is not documented by Microsoft; when the window is still open shortly after, phase becomes
// "closeManually" and DialogPage asks the user to close it. The result is already stored, so the opener
// gets it either way.

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { readRequest, toErrorMessage, writeResult, type DialogDefinition, type DialogResult } from "../protocol/dialog.js";

/** "open": working; "closing": result stored, closing the window; "closeManually": the window stayed open. */
export type DialogPhase = "open" | "closing" | "closeManually";

export interface DialogController<TInput, TResult> {
  /** The input sent by the opener; null when the page was opened without one (e.g. directly by URL). */
  readonly input: TInput | null;
  readonly busy: boolean;
  /** Error of the last run, shown by DialogPage; the user can try again or cancel. */
  readonly error: string | null;
  readonly phase: DialogPhase;
  /** How the dialog ended, once it did. */
  readonly result: DialogResult<TResult> | null;
  /** Runs the page's work: busy meanwhile, a thrown error becomes `error` instead of closing the dialog. */
  run(action: () => Promise<void>): Promise<void>;
  succeed(value: TResult): void;
  cancel(): void;
  /** Ends the dialog as failed, e.g. when it cannot work at all; usually show `error` and let the user retry. */
  fail(error: unknown): void;
  clearError(): void;
}

export interface UseDialogOptions {
  /** Default: location.search. */
  readonly search?: string;
  /** Default: sessionStorage. */
  readonly storage?: Storage | null;
  /** Default: window.close(). */
  readonly closeWindow?: () => void;
  /** Milliseconds to wait for the window to close before asking the user. Default 500. */
  readonly closeCheckDelay?: number;
}

export function useDialog<TInput extends object, TResult>(
  definition: DialogDefinition<TInput, TResult>,
  options: UseDialogOptions = {},
): DialogController<TInput, TResult> {
  const request = useMemo(() => readRequest(definition, options.search), [definition, options.search]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [phase, setPhase] = useState<DialogPhase>("open");
  const [result, setResult] = useState<DialogResult<TResult> | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  useEffect(() => () => clearTimeout(timer.current), []);

  const { storage, closeWindow, closeCheckDelay } = options;
  const end = useCallback(
    (ended: DialogResult<TResult>) => {
      if (request !== null) writeResult(request.requestId, ended, storage);
      setResult(ended);
      setPhase("closing");
      try {
        (closeWindow ?? (() => globalThis.close()))();
      } catch {
        // Ignored: the check below asks the user to close the window.
      }
      timer.current = setTimeout(() => setPhase("closeManually"), closeCheckDelay ?? 500);
    },
    [request, storage, closeWindow, closeCheckDelay],
  );

  const run = useCallback(async (action: () => Promise<void>) => {
    setBusy(true);
    setError(null);
    try {
      await action();
    } catch (caught) {
      setError(toErrorMessage(caught));
    } finally {
      setBusy(false);
    }
  }, []);

  return {
    input: request?.input ?? null,
    busy,
    error,
    phase,
    result,
    run,
    succeed: (value) => end({ status: "succeeded", value }),
    cancel: () => end({ status: "cancelled" }),
    fail: (failure) => end({ status: "failed", error: toErrorMessage(failure) }),
    clearError: () => setError(null),
  };
}
