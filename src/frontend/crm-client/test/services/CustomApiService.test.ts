import { describe, expect, it, vi } from "vitest";
import { executeCustomApi, Parameter } from "../../src/services/CustomApiService.js";
import { installXrmMock } from "../mocks/index.js";

const ID = "11111111-0000-0000-0000-000000000001";

type Request = Record<string, unknown> & { getMetadata(): Record<string, unknown> };
const sentRequest = (xrm: Xrm.XrmStatic): Request => vi.mocked(xrm.WebApi.online.execute).mock.calls[0]![0] as Request;
const jsonResponse = (body: object) => ({ ok: true, status: 200, json: () => Promise.resolve(body) }) as unknown as Xrm.ExecuteResponse;

describe("executeCustomApi", () => {
  it("calls a bound action on the record, with the table as the bound parameter", async () => {
    const xrm = installXrmMock();

    await executeCustomApi({ name: "ogz_ReopenSupportTicket", boundTo: { entityName: "ogz_support_ticket", id: `{${ID.toUpperCase()}}` } });

    const request = sentRequest(xrm);
    expect(request["entity"]).toEqual({ entityType: "ogz_support_ticket", id: ID });
    expect(request.getMetadata()).toEqual({
      boundParameter: "entity",
      operationType: 0,
      operationName: "ogz_ReopenSupportTicket",
      parameterTypes: { entity: { typeName: "mscrm.ogz_support_ticket", structuralProperty: 5 } },
    });
  });

  it("calls an unbound function with typed parameters", async () => {
    const xrm = installXrmMock();
    const on = new Date("2026-11-05T11:30:00Z");

    await executeCustomApi({
      name: "ogz_CountTickets",
      kind: "function",
      parameters: {
        Category: Parameter.string("Hardware"),
        OnlyOpen: Parameter.boolean(true),
        Top: Parameter.integer(5),
        MinCost: Parameter.decimal(10.5),
        Since: Parameter.dateTime(on),
        OwnerId: Parameter.guid(ID),
      },
    });

    const request = sentRequest(xrm);
    expect(request["Category"]).toBe("Hardware");
    expect(request["Since"]).toBe(on);
    expect(request["entity"]).toBeUndefined();
    expect(request.getMetadata()).toEqual({
      boundParameter: null,
      operationType: 1,
      operationName: "ogz_CountTickets",
      parameterTypes: {
        Category: { typeName: "Edm.String", structuralProperty: 1 },
        OnlyOpen: { typeName: "Edm.Boolean", structuralProperty: 1 },
        Top: { typeName: "Edm.Int32", structuralProperty: 1 },
        MinCost: { typeName: "Edm.Decimal", structuralProperty: 1 },
        Since: { typeName: "Edm.DateTimeOffset", structuralProperty: 1 },
        OwnerId: { typeName: "Edm.Guid", structuralProperty: 1 },
      },
    });
  });

  it("returns an empty object when the Custom API has no response properties", async () => {
    installXrmMock();

    expect(await executeCustomApi({ name: "ogz_DoSomething" })).toEqual({});
  });

  it("returns the response properties without OData annotations", async () => {
    const xrm = installXrmMock();
    vi.mocked(xrm.WebApi.online.execute).mockResolvedValueOnce(
      jsonResponse({ "@odata.context": "https://test/api/data/v9.2/$metadata#...", ReopenedOn: "2026-11-05T11:30:00Z" }),
    );

    const response = await executeCustomApi<{ ReopenedOn: string }>({ name: "ogz_ReopenSupportTicket" });

    expect(response).toEqual({ ReopenedOn: "2026-11-05T11:30:00Z" });
  });

  it("rejects with the error of the Custom API", async () => {
    const xrm = installXrmMock();
    vi.mocked(xrm.WebApi.online.execute).mockRejectedValueOnce({ message: "Only inactive tickets can be reopened." });

    await expect(executeCustomApi({ name: "ogz_ReopenSupportTicket" })).rejects.toEqual({ message: "Only inactive tickets can be reopened." });
  });
});
