// Builds the real form bundles in memory and runs them the way a Dynamics 365 form loads a
// Web Resource: as a classic (non-module) script.

import { describe, expect, it } from "vitest";
import { buildEntries, buildOptions, findEntries, requireEntries, type BuildEntry } from "../../build.mjs";
import { createEventContextMock, createFormContextMock } from "../mocks/index.js";

const entries = findEntries();
const account = entries.find((e) => e.globalName === "Ogz.Forms.Account")!;
const supportTicket = entries.find((e) => e.globalName === "Ogz.Forms.SupportTicket")!;

// Package folder, derived from a real entry so no Node path APIs are needed.
const packageDir = account.entryPoint.replace(/[\\/]src[\\/]forms[\\/]account[\\/]index\.ts$/, "");
const fixture = (name: string) => `${packageDir}/test-fixtures/${name}`;

async function bundle(entry: BuildEntry): Promise<string> {
  const [result] = await buildEntries([entry], { write: false });
  return result!.outputFiles![0]!.text;
}

type Namespace = { Forms: Record<string, Record<string, unknown>> };

/** Runs the script as a classic script body with an optional pre-existing `Ogz` and returns `Ogz`. */
function run(code: string, existing?: Namespace): Namespace {
  return new Function("Ogz", `${code}\nreturn Ogz;`)(existing) as Namespace;
}

describe("Web Resource bundles", () => {
  it("has one deterministic output per entry point under dist/webresources", () => {
    const outputs = entries.map((e) => e.outfile.replace(/\\/g, "/"));

    expect(outputs.some((o) => o.endsWith("dist/webresources/forms/account.js"))).toBe(true);
    expect(outputs.some((o) => o.endsWith("dist/webresources/forms/supportTicket.js"))).toBe(true);
  });

  it.each([["account"], ["supportTicket"]])("bundles %s as a self-contained classic script", async (name) => {
    const code = await bundle(name === "account" ? account : supportTicket);

    expect(code).not.toMatch(/\bimport\s*[{*("']|\bexport\s|\brequire\(/);
    expect(code).not.toContain("vitest");
  });

  it("exposes only the form handlers under Ogz.Forms.Account and leaks no globals", async () => {
    const globalsBefore = new Set(Object.keys(globalThis));

    const ogz = run(await bundle(account));

    expect(Object.keys(ogz.Forms["Account"]!).sort()).toEqual(["onLoad", "onSave"]);
    expect(Object.keys(globalThis).filter((key) => !globalsBefore.has(key))).toEqual([]);
  });

  it("exposes Ogz.Forms.SupportTicket.onLoad, and the bundled rules work without any module loader", async () => {
    const ogz = run(await bundle(supportTicket));
    const onLoad = ogz.Forms["SupportTicket"]!["onLoad"] as (context: Xrm.Events.EventContext) => void;
    const formContext = createFormContextMock({
      attributes: {
        ogz_category: { value: null, attributeType: "lookup" },
        statuscode: { value: 882100005, attributeType: "optionset" }, // Cancelled
        ogz_description: { attributeType: "memo", requiredLevel: "none" },
      },
    });

    onLoad(createEventContextMock(formContext));

    expect(Object.keys(ogz.Forms["SupportTicket"]!)).toEqual(["CATEGORY_NOTIFICATION_ID", "onLoad", "onSave"]);
    expect(formContext.ui.setFormNotification).toHaveBeenCalledWith(expect.stringContaining("Select a ticket category"), "INFO", "ogz_support_ticket_category_missing");
    expect(formContext.getAttribute("ogz_description")!.getRequiredLevel()).toBe("required");

    const status = formContext.getAttribute("statuscode")!;
    status.setValue(1); // New
    status.fireOnChange();
    expect(formContext.getAttribute("ogz_description")!.getRequiredLevel()).toBe("none");
  });

  it("inlines the generated status reason values into the Support Ticket bundle", async () => {
    const code = await bundle(supportTicket);

    expect(code).toContain("882100005");
    expect(code).not.toContain("ogz_support_ticket_statuscode");
    expect(code).not.toContain("In_Process");
  });

  it("keeps bundles independent: each contains only its own form code", async () => {
    const accountCode = await bundle(account);
    const ticketCode = await bundle(supportTicket);

    expect(accountCode).not.toContain("Select a ticket category");
    expect(ticketCode).not.toContain("Phone is the preferred contact method");
    expect(ticketCode).toContain("Ogz.Forms.SupportTicket");
    expect(ticketCode).not.toContain("Ogz.Forms.Account");
  });

  it("does not overwrite other bundles in the shared Ogz namespace", async () => {
    const ogz = run(await bundle(supportTicket), run(await bundle(account)));

    expect(ogz.Forms["Account"]).toBeDefined();
    expect(ogz.Forms["SupportTicket"]).toBeDefined();
  });

  it("produces identical output on every build", async () => {
    expect(await bundle(supportTicket)).toBe(await bundle(supportTicket));
  });

  it("inlines Choice values instead of shipping every generated enum", async () => {
    const code = await bundle(account);

    expect(code).not.toContain("account_industrycode");
    expect(code).not.toContain("Preferred_Customer");
  });

  it("fails when there are no entry points", () => {
    const none = findEntries(fixture("no-entries"));

    expect(none).toEqual([]);
    expect(() => requireEntries(none)).toThrow("no entry points");
  });

  it("takes a folder's index.ts as the entry point, with the same output and global as a file", () => {
    expect(supportTicket.entryPoint.replace(/\\/g, "/")).toMatch(/\/src\/forms\/supportTicket\/index\.ts$/);
    expect(supportTicket.outfile.replace(/\\/g, "/")).toMatch(/\/dist\/webresources\/forms\/supportTicket\.js$/);
    expect(entries.map((e) => e.globalName)).toEqual(["Ogz.Forms.Account", "Ogz.Forms.SupportTicket"]);
  });

  it("refuses a file and a folder that would both produce the same bundle", () => {
    expect(() => findEntries(fixture("duplicate-entry"))).toThrow("are both entry points for 'sample'");
  });

  it("fails when an entry point cannot be bundled", async () => {
    const broken = findEntries(fixture("broken-import"));

    expect(broken).toHaveLength(1);
    await expect(buildEntries(broken, { write: false })).rejects.toThrow(/missing-runtime/);
  });

  it("uses browser-safe options for every entry", () => {
    for (const entry of entries) {
      const options = buildOptions(entry);
      expect(options).toMatchObject({ bundle: true, format: "iife", platform: "browser", target: "es2020", globalName: entry.globalName });
    }
  });
});
