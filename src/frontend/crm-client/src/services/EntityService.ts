// Generic Web API access for one table. Bind a table once, then read and write its records without
// repeating the table name or hand-writing OData query strings:
//
//   const categories = EntityService<ogz_ticket_category>("ogz_ticket_category");
//   const active = await categories.retrieveMultiple({ select: ["ogz_category_name"], filter: "statecode eq 0", orderBy: "ogz_category_name" });
//
// Table-specific services (e.g. "the team of a category") belong in their own files next to this
// one and use it; forms call those services, not Xrm.WebApi.
//
// Every method is asynchronous. SaveRules are synchronous, so a save check cannot wait for these
// calls; use them in OnLoad, OnChange or command handlers.

import { Data, type WebApiRecord } from "../helpers/data.js";

/** The parts of an OData query this service builds; anything else goes in `options` as a raw string. */
export interface RecordQuery {
  /** Columns to return ($select). Lookups are read as "_<column>_value". */
  readonly select?: readonly string[];
  /** OData filter expression ($filter), e.g. "statecode eq 0". */
  readonly filter?: string;
  /** Sort expression ($orderby), e.g. "createdon desc". */
  readonly orderBy?: string;
  /** Maximum number of records ($top). */
  readonly top?: number;
}

/** Builds "?$select=...&$filter=..." from a query; "" when the query is empty. */
export function toQueryString(query: RecordQuery = {}): string {
  const parts: string[] = [];
  if (query.select !== undefined && query.select.length > 0) parts.push(`$select=${query.select.join(",")}`);
  if (query.filter) parts.push(`$filter=${query.filter}`);
  if (query.orderBy) parts.push(`$orderby=${query.orderBy}`);
  if (query.top !== undefined) parts.push(`$top=${query.top}`);
  return parts.length > 0 ? `?${parts.join("&")}` : "";
}

export interface EntityService<T extends object> {
  readonly entityLogicalName: string;
  /** Creates a record and returns its id (without braces). */
  create(record: Partial<T>): Promise<string>;
  /** One record by id; only `select` columns when given. */
  retrieve(id: string, select?: readonly string[]): Promise<T>;
  /** The records matching the query (first page; see `maxPageSize`). */
  retrieveMultiple(query?: RecordQuery, maxPageSize?: number): Promise<T[]>;
  /** Writes only the given columns. */
  update(id: string, changes: Partial<T>): Promise<void>;
  delete(id: string): Promise<void>;
}

/**
 * A service for one table, e.g. EntityService<ogz_ticket_category>("ogz_ticket_category"). `T`
 * describes the record's columns; the generated @ogz/crm-entities interfaces fit.
 */
export function EntityService<T extends object = WebApiRecord>(entityLogicalName: string): EntityService<T> {
  // Data works with plain column maps; the records are passed through unchanged.
  const asRecord = (record: Partial<T>): WebApiRecord => record as WebApiRecord;
  return {
    entityLogicalName,
    create: (record) => Data.create(entityLogicalName, asRecord(record)),
    retrieve: async (id, select) =>
      (await Data.retrieve(entityLogicalName, id, toQueryString(select === undefined ? {} : { select }) || undefined)) as T,
    retrieveMultiple: async (query, maxPageSize) =>
      (await Data.retrieveMultiple(entityLogicalName, toQueryString(query) || undefined, maxPageSize)).entities as T[],
    update: (id, changes) => Data.update(entityLogicalName, id, asRecord(changes)),
    delete: (id) => Data.delete(entityLogicalName, id),
  };
}
