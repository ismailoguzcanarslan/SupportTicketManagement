import { act } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { connectXrm, mountPage } from "../src/index.js";

afterEach(() => {
  Reflect.deleteProperty(globalThis, "Xrm");
  document.body.innerHTML = "";
});

const fakeWindow = (parent: object) => {
  const win = { parent } as unknown as Window;
  return win;
};

describe("connectXrm", () => {
  it("copies the parent window's Xrm", () => {
    const xrm = { Navigation: {} };

    expect(connectXrm(fakeWindow({ Xrm: xrm }))).toBe(true);
    expect(Reflect.get(globalThis, "Xrm")).toBe(xrm);
  });

  it("keeps an Xrm that is already there", () => {
    const own = { own: true };
    Reflect.set(globalThis, "Xrm", own);

    expect(connectXrm(fakeWindow({ Xrm: {} }))).toBe(true);
    expect(Reflect.get(globalThis, "Xrm")).toBe(own);
  });

  it("finds none without a parent, or when the parent has none", () => {
    const top = {} as { parent?: unknown };
    top.parent = top;

    expect(connectXrm(top as unknown as Window)).toBe(false);
    expect(connectXrm(fakeWindow({}))).toBe(false);
  });

  it("finds none when the parent cannot be read", () => {
    const parent = new Proxy({}, { get: () => { throw new Error("cross-origin"); } });

    expect(connectXrm(fakeWindow(parent))).toBe(false);
  });

  it("uses this window by default", () => {
    expect(connectXrm()).toBe(false);
  });
});

describe("mountPage", () => {
  it("renders the page into #root inside the theme", async () => {
    document.body.innerHTML = '<div id="root"></div>';

    await act(async () => mountPage(<p>Hello</p>));

    expect(document.getElementById("root")!.textContent).toBe("Hello");
    expect(document.querySelector(".fui-FluentProvider")).not.toBeNull();
  });

  it("needs a #root element", () => {
    expect(() => mountPage(<p />)).toThrow("no element with id 'root'");
  });
});
