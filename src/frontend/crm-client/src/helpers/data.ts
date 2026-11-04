// CRUD through Xrm.WebApi. Ids may be passed with or without braces.

import { getXrm, normalizeId, toPromise } from "./xrm.js";

/** Column values of a Web API record, e.g. { ogz_ticket_title: "Printer offline" }. */
export type WebApiRecord = Record<string, unknown>;

export const Data = {
  /** Creates a record and returns its id (without braces). */
  async create(entityLogicalName: string, record: WebApiRecord): Promise<string> {
    const response = await toPromise(getXrm().WebApi.createRecord(entityLogicalName, record));
    return normalizeId(response.id);
  },

  /** Retrieves one record. `options` is an OData query string such as "?$select=name". */
  retrieve<T extends WebApiRecord = WebApiRecord>(entityLogicalName: string, id: string, options?: string): Promise<T> {
    return toPromise(getXrm().WebApi.retrieveRecord<T>(entityLogicalName, normalizeId(id), options));
  },

  /** Retrieves records matching an OData query string or FetchXML ("?fetchXml=..."). */
  retrieveMultiple<T extends WebApiRecord = WebApiRecord>(
    entityLogicalName: string,
    options?: string,
    maxPageSize?: number,
  ): Promise<Xrm.RetrieveMultipleResult<T>> {
    return toPromise(getXrm().WebApi.retrieveMultipleRecords<T>(entityLogicalName, options, maxPageSize));
  },

  async update(entityLogicalName: string, id: string, changes: WebApiRecord): Promise<void> {
    await toPromise(getXrm().WebApi.updateRecord(entityLogicalName, normalizeId(id), changes));
  },

  async delete(entityLogicalName: string, id: string): Promise<void> {
    await toPromise(getXrm().WebApi.deleteRecord(entityLogicalName, normalizeId(id)));
  },
} as const;
