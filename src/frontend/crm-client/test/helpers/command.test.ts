import { describe, expect, it, vi } from "vitest";
import type { DialogResult } from "@ogz/crm-ui/protocol";
import { type RecordCommand, runRecordCommand } from "../../src/helpers/command.js";
import { createFormContextMock, installXrmMock } from "../mocks/index.js";

const ID = "11111111-0000-0000-0000-000000000001";
const texts = {
  title: "Do It",
  notSaved: "Save first.",
  unsavedChanges: "Save your changes first.",
  confirm: "Sure?",
  progress: "Working...",
  failed: "It failed.",
};

/** A command whose parts are spies; `blocker` is what its check returns. */
function command(blocker: string | null = null) {
  return {
    texts,
    read: vi.fn((_id: string) => Promise.resolve({ stored: true })),
    check: vi.fn((_stored: { stored: boolean }, _id: string) => blocker),
    execute: vi.fn((_id: string, _stored: { stored: boolean }) => Promise.resolve()),
  } satisfies RecordCommand<{ stored: boolean }>;
}

function setup(options: { confirmed?: boolean; entityId?: string; dirty?: boolean } = {}) {
  const xrm = installXrmMock();
  vi.mocked(xrm.Navigation.openConfirmDialog).mockResolvedValue({ confirmed: options.confirmed ?? true });
  const formContext = createFormContextMock({ entityId: options.entityId ?? `{${ID.toUpperCase()}}` });
  vi.mocked(formContext.data.entity.getIsDirty).mockReturnValue(options.dirty ?? false);
  return { xrm, formContext };
}

describe("runRecordCommand", () => {
  it("reads, checks, asks, runs with a progress indicator and refreshes", async () => {
    const { xrm, formContext } = setup();
    const cmd = command();

    expect(await runRecordCommand(formContext, cmd)).toBe("done");

    expect(cmd.read).toHaveBeenCalledWith(ID);
    expect(cmd.check).toHaveBeenCalledWith({ stored: true }, ID);
    expect(xrm.Navigation.openConfirmDialog).toHaveBeenCalledWith({ text: "Sure?", title: "Do It" });
    expect(cmd.execute).toHaveBeenCalledWith(ID, { stored: true });
    expect(xrm.Utility.showProgressIndicator).toHaveBeenCalledWith("Working...");
    expect(xrm.Utility.closeProgressIndicator).toHaveBeenCalledOnce();
    expect(formContext.data.refresh).toHaveBeenCalledWith(false);
  });

  it.each([
    ["the record is not saved", { entityId: "" }, "Save first."],
    ["the form has unsaved changes", { dirty: true }, "Save your changes first."],
  ])("stops without reading when %s", async (_, options, message) => {
    const { xrm, formContext } = setup(options);
    const cmd = command();

    expect(await runRecordCommand(formContext, cmd)).toBe("blocked");

    expect(xrm.Navigation.openAlertDialog).toHaveBeenCalledWith({ text: message, title: "Do It" });
    expect(cmd.read).not.toHaveBeenCalled();
  });

  it("shows the reason of the check and stops before asking", async () => {
    const { xrm, formContext } = setup();
    const cmd = command("Not now.");

    expect(await runRecordCommand(formContext, cmd)).toBe("blocked");

    expect(xrm.Navigation.openAlertDialog).toHaveBeenCalledWith({ text: "Not now.", title: "Do It" });
    expect(xrm.Navigation.openConfirmDialog).not.toHaveBeenCalled();
    expect(cmd.execute).not.toHaveBeenCalled();
  });

  it("runs nothing when the user cancels", async () => {
    const { formContext } = setup({ confirmed: false });
    const cmd = command();

    expect(await runRecordCommand(formContext, cmd)).toBe("cancelled");

    expect(cmd.execute).not.toHaveBeenCalled();
    expect(formContext.data.refresh).not.toHaveBeenCalled();
  });

  it.each([
    ["an Error", new Error("Status transition not allowed."), "Status transition not allowed."],
    ["a Web API error object", { errorCode: -2147220891, message: "Only inactive tickets can be reopened." }, "Only inactive tickets can be reopened."],
    ["anything else", "Network error", "Network error"],
  ])("shows %s in the error dialog and still closes the progress indicator", async (_, error, details) => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    const { xrm, formContext } = setup();
    const cmd = command();
    cmd.execute.mockRejectedValueOnce(error);

    expect(await runRecordCommand(formContext, cmd)).toBe("failed");

    expect(xrm.Utility.closeProgressIndicator).toHaveBeenCalledOnce();
    expect(xrm.Navigation.openErrorDialog).toHaveBeenCalledWith({ message: "It failed.", details });
    expect(formContext.data.refresh).not.toHaveBeenCalled();
  });

  it("shows the error when reading fails", async () => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    const { xrm, formContext } = setup();
    const cmd = command();
    cmd.read.mockRejectedValueOnce(new Error("Record not found."));

    expect(await runRecordCommand(formContext, cmd)).toBe("failed");

    expect(xrm.Navigation.openErrorDialog).toHaveBeenCalledWith({ message: "It failed.", details: "Record not found." });
    expect(cmd.execute).not.toHaveBeenCalled();
  });
});

describe("runRecordCommand with a dialog", () => {
  const dialogCommand = (result: DialogResult<unknown>) =>
    ({
      texts: { title: "Do It", notSaved: "Save first.", unsavedChanges: "Save your changes first.", failed: "It failed." },
      read: vi.fn((_id: string) => Promise.resolve({ stored: true })),
      check: vi.fn(() => null),
      dialog: vi.fn((_id: string, _stored: { stored: boolean }) => Promise.resolve(result)),
    }) satisfies RecordCommand<{ stored: boolean }>;

  it("opens the dialog instead of asking, and refreshes when it succeeded", async () => {
    const { xrm, formContext } = setup();
    const cmd = dialogCommand({ status: "succeeded", value: 1 });

    expect(await runRecordCommand(formContext, cmd)).toBe("done");

    expect(cmd.dialog).toHaveBeenCalledWith(ID, { stored: true });
    expect(xrm.Navigation.openConfirmDialog).not.toHaveBeenCalled();
    expect(xrm.Utility.showProgressIndicator).not.toHaveBeenCalled();
    expect(formContext.data.refresh).toHaveBeenCalledOnce();
  });

  it("changes nothing when the dialog was cancelled", async () => {
    const { formContext } = setup();

    expect(await runRecordCommand(formContext, dialogCommand({ status: "cancelled" }))).toBe("cancelled");
    expect(formContext.data.refresh).not.toHaveBeenCalled();
  });

  it("shows the error of a failed dialog", async () => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    const { xrm, formContext } = setup();

    expect(await runRecordCommand(formContext, dialogCommand({ status: "failed", error: "Could not open." }))).toBe("failed");
    expect(xrm.Navigation.openErrorDialog).toHaveBeenCalledWith({ message: "It failed.", details: "Could not open." });
  });
});

describe("runRecordCommand without confirm and progress texts", () => {
  it("uses the title for both", async () => {
    const { xrm, formContext } = setup();
    const cmd = command();

    await runRecordCommand(formContext, { ...cmd, texts: { title: "Do It", notSaved: "n", unsavedChanges: "u", failed: "f" } });

    expect(xrm.Navigation.openConfirmDialog).toHaveBeenCalledWith({ text: "Do It", title: "Do It" });
    expect(xrm.Utility.showProgressIndicator).toHaveBeenCalledWith("Do It");
  });
});
