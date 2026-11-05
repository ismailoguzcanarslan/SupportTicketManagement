// Calls Custom APIs (and other actions/functions) without hand-writing the getMetadata() object that
// Xrm.WebApi.online.execute needs:
//
//   await executeCustomApi({ name: "ogz_ReopenSupportTicket", boundTo: { entityName: "ogz_support_ticket", id } });
//   const { Total } = await executeCustomApi<{ Total: number }>({
//     name: "ogz_CountOpenTickets",
//     kind: "function",
//     parameters: { CategoryName: Parameter.string("Hardware") },
//   });
//
// A Custom API that fails (e.g. a plug-in throws InvalidPluginExecutionException) rejects with the
// plug-in's message.

import { Data } from "../helpers/data.js";
import { normalizeId } from "../helpers/xrm.js";

/** Values of getMetadata().parameterTypes[...].structuralProperty. */
const StructuralProperty = { PrimitiveType: 1, EntityType: 5 } as const;

export interface CustomApiParameter {
  readonly value: unknown;
  /** Edm type name, e.g. "Edm.String". */
  readonly typeName: string;
  readonly structuralProperty: number;
}

const primitive = (typeName: string) => (value: unknown): CustomApiParameter => ({
  value,
  typeName,
  structuralProperty: StructuralProperty.PrimitiveType,
});

/** Builders for request parameters, by the Custom API parameter type. */
export const Parameter = {
  string: primitive("Edm.String") as (value: string) => CustomApiParameter,
  boolean: primitive("Edm.Boolean") as (value: boolean) => CustomApiParameter,
  integer: primitive("Edm.Int32") as (value: number) => CustomApiParameter,
  decimal: primitive("Edm.Decimal") as (value: number) => CustomApiParameter,
  dateTime: primitive("Edm.DateTimeOffset") as (value: Date) => CustomApiParameter,
  guid: primitive("Edm.Guid") as (value: string) => CustomApiParameter,
} as const;

export interface CustomApiCall {
  /** Unique name of the Custom API, e.g. "ogz_ReopenSupportTicket". */
  readonly name: string;
  /** "action" (default) changes data; "function" only reads (IsFunction = Yes). */
  readonly kind?: "action" | "function";
  /** The record of a Custom API bound to a table (binding type Entity). Omit for an unbound one. */
  readonly boundTo?: { readonly entityName: string; readonly id: string };
  /** Request parameters by their unique name. Optional parameters can be left out. */
  readonly parameters?: Readonly<Record<string, CustomApiParameter>>;
}

/**
 * Runs a Custom API and returns its response properties by name. A Custom API without response properties
 * returns an empty object.
 */
export async function executeCustomApi<TResponse extends object = Record<string, unknown>>(call: CustomApiCall): Promise<TResponse> {
  const parameterTypes: Record<string, { typeName: string; structuralProperty: number }> = {};
  const request: Record<string, unknown> = {};

  if (call.boundTo !== undefined) {
    request["entity"] = { entityType: call.boundTo.entityName, id: normalizeId(call.boundTo.id) };
    parameterTypes["entity"] = { typeName: `mscrm.${call.boundTo.entityName}`, structuralProperty: StructuralProperty.EntityType };
  }
  for (const [name, parameter] of Object.entries(call.parameters ?? {})) {
    request[name] = parameter.value;
    parameterTypes[name] = { typeName: parameter.typeName, structuralProperty: parameter.structuralProperty };
  }
  request["getMetadata"] = () => ({
    boundParameter: call.boundTo === undefined ? null : "entity",
    operationType: call.kind === "function" ? 1 : 0,
    operationName: call.name,
    parameterTypes,
  });

  const response = await Data.execute(request);
  if (response.status === 204) return {} as TResponse;

  const body = (await response.json()) as Record<string, unknown>;
  // "@odata.context" and similar annotations are not response properties.
  return Object.fromEntries(Object.entries(body).filter(([key]) => !key.startsWith("@"))) as TResponse;
}
