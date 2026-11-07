import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Button } from "@fluentui/react-components";
import { DialogPage, OgzThemeProvider, defineDialog, takeResult, useDialog, type UseDialogOptions } from "../src/index.js";

const Sample = defineDialog<{ name: string }, { done: boolean }>({ page: "sample" });
const opened = `?data=${encodeURIComponent(JSON.stringify({ v: 1, requestId: "r1", page: "sample", input: { name: "Ada" } }))}`;

afterEach(() => {
  cleanup();
  sessionStorage.clear();
  vi.useRealTimers();
});

/** A page that shows its input, works with `work` and ends the dialog. */
function SamplePage({ options, work }: { options: UseDialogOptions; work: () => Promise<void> }) {
  const dialog = useDialog(Sample, options);
  return (
    <DialogPage
      dialog={dialog}
      title="Sample"
      doneMessage="Saved."
      actions={
        <>
          <Button appearance="primary" onClick={() => void dialog.run(async () => { await work(); dialog.succeed({ done: true }); })}>
            OK
          </Button>
          <Button onClick={dialog.cancel}>Cancel</Button>
          <Button onClick={() => dialog.fail(new Error("gave up"))}>Fail</Button>
          <Button onClick={dialog.clearError}>Clear</Button>
        </>
      }
    >
      <span>Hello {dialog.input?.name}</span>
    </DialogPage>
  );
}

const show = (options: Partial<UseDialogOptions> = {}, work: () => Promise<void> = async () => {}) => {
  const closeWindow = vi.fn();
  render(
    <OgzThemeProvider>
      <SamplePage options={{ search: opened, closeWindow, ...options }} work={work} />
    </OgzThemeProvider>,
  );
  return { closeWindow };
};

describe("DialogPage with useDialog", () => {
  it("shows the title, the input and the actions", () => {
    show();

    expect(screen.getByRole("heading", { name: "Sample" })).toBeTruthy();
    expect(screen.getByText("Hello Ada")).toBeTruthy();
    expect(screen.getByRole("button", { name: "OK" })).toBeTruthy();
  });

  it("succeeds: stores the result for the opener and closes the window", async () => {
    const { closeWindow } = show();

    await act(async () => fireEvent.click(screen.getByRole("button", { name: "OK" })));

    expect(takeResult("r1")).toEqual({ status: "succeeded", value: { done: true } });
    expect(closeWindow).toHaveBeenCalledOnce();
    expect(screen.getByRole("button", { name: "Closing..." })).toBeTruthy();
  });

  it("cancels: stores 'cancelled' and closes", () => {
    const { closeWindow } = show();

    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));

    expect(takeResult("r1")).toEqual({ status: "cancelled" });
    expect(closeWindow).toHaveBeenCalledOnce();
  });

  it("fails: stores the error", () => {
    show();

    fireEvent.click(screen.getByRole("button", { name: "Fail" }));

    expect(takeResult("r1")).toEqual({ status: "failed", error: "gave up" });
  });

  it("shows an error of the work and lets the user try again", async () => {
    let attempts = 0;
    const { closeWindow } = show({}, async () => {
      attempts++;
      if (attempts === 1) throw { message: "Only inactive tickets can be reopened." };
    });

    await act(async () => fireEvent.click(screen.getByRole("button", { name: "OK" })));
    expect(screen.getByText("Only inactive tickets can be reopened.")).toBeTruthy();
    expect(closeWindow).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "Clear" }));
    expect(screen.queryByText("Only inactive tickets can be reopened.")).toBeNull();

    await act(async () => fireEvent.click(screen.getByRole("button", { name: "OK" })));
    expect(closeWindow).toHaveBeenCalledOnce();
  });

  it("is busy while the work runs", async () => {
    let finish: () => void = () => {};
    show({}, () => new Promise<void>((resolve) => (finish = resolve)));

    await act(async () => fireEvent.click(screen.getByRole("button", { name: "OK" })));
    expect(screen.getByRole("main").getAttribute("aria-busy")).toBe("true");
    expect(screen.getByText("Working...")).toBeTruthy();

    await act(async () => finish());
    expect(screen.getByRole("main").getAttribute("aria-busy")).toBe("false");
  });

  it("asks the user to close the window when it stays open", async () => {
    vi.useFakeTimers();
    show({ closeCheckDelay: 100 });

    await act(async () => fireEvent.click(screen.getByRole("button", { name: "OK" })));
    act(() => vi.advanceTimersByTime(100));

    expect(screen.getByText(/Saved\. You can close this window\./)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "OK" })).toBeNull();
  });

  it("says nothing changed when a cancelled dialog stays open, and survives a failing close", () => {
    vi.useFakeTimers();
    show({
      closeCheckDelay: 50,
      closeWindow: () => {
        throw new Error("not allowed");
      },
    });

    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    act(() => vi.advanceTimersByTime(50));

    expect(screen.getByText(/Nothing was changed\. You can close this window\./)).toBeTruthy();
  });

  it("uses the default message and window.close() by default", () => {
    vi.useFakeTimers();
    const close = vi.spyOn(window, "close").mockImplementation(() => {});
    function Plain() {
      const dialog = useDialog(Sample, { search: opened });
      return (
        <DialogPage dialog={dialog} title="Plain" actions={<Button onClick={() => dialog.succeed({ done: true })}>OK</Button>}>
          body
        </DialogPage>
      );
    }
    render(<Plain />);

    fireEvent.click(screen.getByRole("button", { name: "OK" }));
    act(() => vi.advanceTimersByTime(500));

    expect(close).toHaveBeenCalledOnce();
    expect(screen.getByText(/^Done\. You can close this window\./)).toBeTruthy();
  });

  it("explains when the page was opened without input", () => {
    show({ search: "" });

    expect(screen.getByText("This page can only be opened from the app.")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "OK" })).toBeNull();
  });

  it("stores no result when opened without input", () => {
    const storage = { setItem: vi.fn() } as unknown as Storage;
    function NoInput() {
      const dialog = useDialog(Sample, { search: "", storage, closeWindow: () => {} });
      return <button onClick={dialog.cancel}>end</button>;
    }
    render(<NoInput />);

    fireEvent.click(screen.getByRole("button", { name: "end" }));

    expect(storage.setItem).not.toHaveBeenCalled();
  });
});
