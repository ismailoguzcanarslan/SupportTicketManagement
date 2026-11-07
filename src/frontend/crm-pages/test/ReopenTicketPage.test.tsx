import { OgzThemeProvider, takeResult } from "@ogz/crm-ui";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { installXrmMock, uninstallXrmMock } from "../../crm-client/test/mocks/xrm.js";
import { REASON_MAX_LENGTH, ReopenTicketPage } from "../src/pages/reopenTicket/ReopenTicketPage.js";

const ID = "11111111-0000-0000-0000-000000000001";
const opened = `?data=${encodeURIComponent(JSON.stringify({ v: 1, requestId: "r1", page: "reopenTicket", input: { ticketId: ID } }))}`;

afterEach(() => {
  cleanup();
  uninstallXrmMock();
  sessionStorage.clear();
  vi.restoreAllMocks();
});

type Request = Record<string, unknown> & { getMetadata(): { operationName: string; boundParameter: string | null; parameterTypes: object } };

function show() {
  const xrm = installXrmMock();
  const closeWindow = vi.fn();
  render(
    <OgzThemeProvider>
      <ReopenTicketPage options={{ search: opened, closeWindow }} />
    </OgzThemeProvider>,
  );
  const sent = () => vi.mocked(xrm.WebApi.online.execute).mock.calls[0]![0] as Request;
  return { xrm, closeWindow, sent };
}

describe("Reopen Ticket page", () => {
  it("explains what reopening does and offers an optional reason", () => {
    show();

    expect(screen.getByRole("heading", { name: "Reopen ticket" })).toBeTruthy();
    expect(screen.getByText(/description is cleared/)).toBeTruthy();
    expect(screen.getByRole("textbox", { name: "Reason" }).getAttribute("maxlength")).toBe(String(REASON_MAX_LENGTH));
  });

  it("calls the Custom API on the ticket without a reason, reports success and closes", async () => {
    const { xrm, closeWindow, sent } = show();
    vi.mocked(xrm.WebApi.online.execute).mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: () => Promise.resolve({ ReopenedOn: "2026-11-07T10:00:00Z" }),
    } as unknown as Xrm.ExecuteResponse);

    await act(async () => fireEvent.click(screen.getByRole("button", { name: "Reopen" })));

    expect(sent().getMetadata().operationName).toBe("ogz_ReopenSupportTicket");
    expect(sent()["entity"]).toEqual({ entityType: "ogz_support_ticket", id: ID });
    expect(sent()["Reason"]).toBeUndefined();
    expect(takeResult("r1")).toEqual({ status: "succeeded", value: { reopenedOn: "2026-11-07T10:00:00Z" } });
    expect(closeWindow).toHaveBeenCalledOnce();
  });

  it("sends the trimmed reason", async () => {
    const { sent } = show();

    fireEvent.change(screen.getByRole("textbox", { name: "Reason" }), { target: { value: "  Customer called back  " } });
    await act(async () => fireEvent.click(screen.getByRole("button", { name: "Reopen" })));

    expect(sent()["Reason"]).toBe("Customer called back");
    expect(sent().getMetadata().parameterTypes).toMatchObject({ Reason: { typeName: "Edm.String" } });
  });

  it("still reports success when the Custom API returns no time", async () => {
    show();

    await act(async () => fireEvent.click(screen.getByRole("button", { name: "Reopen" })));

    const result = takeResult<{ reopenedOn: string }>("r1");
    expect(result.status).toBe("succeeded");
  });

  it("shows the server's reason and stays open so the user can retry or cancel", async () => {
    const { xrm, closeWindow } = show();
    vi.mocked(xrm.WebApi.online.execute).mockRejectedValueOnce({ message: "Only inactive tickets can be reopened." });

    await act(async () => fireEvent.click(screen.getByRole("button", { name: "Reopen" })));

    expect(screen.getByText("Only inactive tickets can be reopened.")).toBeTruthy();
    expect(closeWindow).not.toHaveBeenCalled();
    expect(takeResult("r1")).toEqual({ status: "cancelled" });

    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    expect(takeResult("r1")).toEqual({ status: "cancelled" });
    expect(closeWindow).toHaveBeenCalledOnce();
  });
});
