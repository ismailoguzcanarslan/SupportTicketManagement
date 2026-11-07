// The contract between code that opens a dialog page (a form script or command in crm-client) and the
// page itself (crm-pages). No React and no Xrm here, so both sides can import it.
//
// Opening:  Dialogs.open(definition, input) sends { v, requestId, page, input } as the `data` string of
//           Xrm.Navigation.navigateTo; Dataverse adds it to the page URL as ?data=...
// Closing:  the page writes its result to sessionStorage under the request id and closes itself.
//           navigateTo resolves when the dialog closes (it returns no value for web resources), and the
//           opener takes the result from sessionStorage. No result means the user closed the dialog.
//
// sessionStorage works as the channel because the dialog and the form script run in the same browser tab
// and on the same origin (the environment URL).

/** What a dialog page takes and returns. Create one with defineDialog and share it. */
export interface DialogDefinition<TInput extends object, TResult> {
  /** Folder name of the page in crm-pages, e.g. "reopenTicket". */
  readonly page: string;
  /** HTML web resource name, e.g. "ogz_/pages/reopenTicket.html". */
  readonly webResource: string;
  readonly title?: string;
  /** Pixels. */
  readonly width?: number;
  readonly height?: number;
  /** "center" (default) or "side" (a panel on the far side). */
  readonly position?: "center" | "side";
  /** Carries the input and result types; never set. */
  readonly types?: { readonly input: TInput; readonly result: TResult };
}

export function defineDialog<TInput extends object, TResult = void>(options: {
  page: string;
  title?: string;
  width?: number;
  height?: number;
  position?: "center" | "side";
}): DialogDefinition<TInput, TResult> {
  return { ...options, webResource: `ogz_/pages/${options.page}.html` };
}

export type DialogInput<D> = D extends DialogDefinition<infer I, unknown> ? I : never;
export type DialogValue<D> = D extends DialogDefinition<object, infer R> ? R : never;

/** How a dialog ended. "cancelled" also covers a dialog closed with its X button. */
export type DialogResult<T> =
  | { readonly status: "succeeded"; readonly value: T }
  | { readonly status: "cancelled" }
  | { readonly status: "failed"; readonly error: string };

/** What travels to the page in the `data` parameter. */
export interface DialogRequest<TInput> {
  readonly v: 1;
  readonly requestId: string;
  readonly page: string;
  readonly input: TInput;
}

const RESULT_PREFIX = "ogz_dialog_result_";

/**
 * Inputs travel in the page URL, which browsers limit to a few thousand characters: pass ids and short
 * values, not records or long texts.
 */
export const MAX_ENCODED_REQUEST_LENGTH = 1800;

const newRequestId = (): string =>
  typeof globalThis.crypto?.randomUUID === "function"
    ? globalThis.crypto.randomUUID()
    : `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;

export function createRequest<TInput extends object>(
  definition: DialogDefinition<TInput, unknown>,
  input: TInput,
  requestId: string = newRequestId(),
): DialogRequest<TInput> {
  return { v: 1, requestId, page: definition.page, input };
}

/** The `data` string for navigateTo. Throws when the input is too long for a URL. */
export function encodeRequest(request: DialogRequest<object>): string {
  const data = JSON.stringify(request);
  if (encodeURIComponent(data).length > MAX_ENCODED_REQUEST_LENGTH) {
    throw new Error(`Dialog '${request.page}': the input is too long for the page URL. Pass ids, not records.`);
  }
  return data;
}

/**
 * The request a page was opened with, read from its URL (`?data=...`); null when it was opened without
 * one or for another page. `search` defaults to the page's location.search.
 */
export function readRequest<TInput extends object>(
  definition: DialogDefinition<TInput, unknown>,
  search: string = globalThis.location?.search ?? "",
): DialogRequest<TInput> | null {
  const data = new URLSearchParams(search).get("data");
  if (data === null) return null;
  try {
    const request = JSON.parse(data) as Partial<DialogRequest<TInput>>;
    if (request.v !== 1 || request.page !== definition.page || typeof request.requestId !== "string" || typeof request.input !== "object" || request.input === null) {
      return null;
    }
    return request as DialogRequest<TInput>;
  } catch {
    return null;
  }
}

function sessionStore(): Storage | null {
  try {
    return globalThis.sessionStorage ?? null;
  } catch {
    // Storage can be blocked (privacy settings, sandboxed frames).
    return null;
  }
}

/** Stores the page's result for the opener. Returns false when sessionStorage is not available. */
export function writeResult(requestId: string, result: DialogResult<unknown>, storage: Storage | null = sessionStore()): boolean {
  if (storage === null) return false;
  storage.setItem(RESULT_PREFIX + requestId, JSON.stringify(result));
  return true;
}

/** Reads and removes the result of a request; "cancelled" when the page left none. */
export function takeResult<T>(requestId: string, storage: Storage | null = sessionStore()): DialogResult<T> {
  const key = RESULT_PREFIX + requestId;
  const stored = storage?.getItem(key) ?? null;
  if (stored === null) return { status: "cancelled" };
  storage!.removeItem(key);
  try {
    return JSON.parse(stored) as DialogResult<T>;
  } catch {
    return { status: "failed", error: "The dialog returned an unreadable result." };
  }
}

/** An Error, a Web API error object ({ message }) or anything else, as text for the user. */
export function toErrorMessage(error: unknown): string {
  if (error instanceof Error) return error.message;
  if (typeof error === "object" && error !== null && typeof (error as { message?: unknown }).message === "string") {
    return (error as { message: string }).message;
  }
  return String(error);
}
