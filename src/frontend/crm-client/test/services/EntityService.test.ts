import type { ogz_ticket_category } from "@ogz/crm-entities";
import { describe, expect, it, vi } from "vitest";
import { EntityService, toQueryString } from "../../src/services/EntityService.js";
import { installXrmMock } from "../mocks/index.js";

const ID = "11111111-0000-0000-0000-000000000001";

describe("toQueryString", () => {
  it("builds the OData options that are given, in a fixed order", () => {
    expect(toQueryString({ select: ["ogz_category_name", "statecode"], filter: "statecode eq 0", orderBy: "ogz_category_name", top: 5 }))
      .toBe("?$select=ogz_category_name,statecode&$filter=statecode eq 0&$orderby=ogz_category_name&$top=5");
    expect(toQueryString({ filter: "statecode eq 0" })).toBe("?$filter=statecode eq 0");
  });

  it("is empty for an empty query", () => {
    expect(toQueryString()).toBe("");
    expect(toQueryString({ select: [] })).toBe("");
  });
});

describe("EntityService", () => {
  // The generated record interfaces can describe the columns.
  const categories = () => EntityService<ogz_ticket_category>("ogz_ticket_category");

  it("creates a record in its table and returns the id without braces", async () => {
    const xrm = installXrmMock();
    vi.mocked(xrm.WebApi.createRecord).mockResolvedValueOnce({ entityType: "ogz_ticket_category", id: `{${ID.toUpperCase()}}` });

    expect(await categories().create({ ogz_category_name: "Hardware" })).toBe(ID);
    expect(xrm.WebApi.createRecord).toHaveBeenCalledWith("ogz_ticket_category", { ogz_category_name: "Hardware" });
  });

  it("retrieves one record, with only the selected columns when given", async () => {
    const xrm = installXrmMock();
    vi.mocked(xrm.WebApi.retrieveRecord).mockResolvedValue({ ogz_category_name: "Hardware" });

    expect(await categories().retrieve(`{${ID}}`, ["ogz_category_name"])).toEqual({ ogz_category_name: "Hardware" });
    await categories().retrieve(ID);

    expect(xrm.WebApi.retrieveRecord).toHaveBeenNthCalledWith(1, "ogz_ticket_category", ID, "?$select=ogz_category_name");
    expect(xrm.WebApi.retrieveRecord).toHaveBeenNthCalledWith(2, "ogz_ticket_category", ID, undefined);
  });

  it("retrieves the matching records of the first page", async () => {
    const xrm = installXrmMock();
    vi.mocked(xrm.WebApi.retrieveMultipleRecords).mockResolvedValueOnce({
      entities: [{ ogz_category_name: "Hardware" }, { ogz_category_name: "Network" }],
    } as Xrm.RetrieveMultipleResult);

    const records = await categories().retrieveMultiple({ select: ["ogz_category_name"], filter: "statecode eq 0" }, 50);

    expect(records.map((r) => r.ogz_category_name)).toEqual(["Hardware", "Network"]);
    expect(xrm.WebApi.retrieveMultipleRecords).toHaveBeenCalledWith("ogz_ticket_category", "?$select=ogz_category_name&$filter=statecode eq 0", 50);
  });

  it("passes no options when retrieving without a query", async () => {
    const xrm = installXrmMock();

    expect(await categories().retrieveMultiple()).toEqual([]);
    expect(xrm.WebApi.retrieveMultipleRecords).toHaveBeenCalledWith("ogz_ticket_category", undefined, undefined);
  });

  it("updates only the given columns, and deletes by id", async () => {
    const xrm = installXrmMock();

    await categories().update(`{${ID}}`, { ogz_category_name: "Hardware & Peripherals" });
    await categories().delete(ID);

    expect(xrm.WebApi.updateRecord).toHaveBeenCalledWith("ogz_ticket_category", ID, { ogz_category_name: "Hardware & Peripherals" });
    expect(xrm.WebApi.deleteRecord).toHaveBeenCalledWith("ogz_ticket_category", ID);
  });

  it("knows its table", () => {
    expect(categories().entityLogicalName).toBe("ogz_ticket_category");
  });
});
