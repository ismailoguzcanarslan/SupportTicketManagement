// @vitest-environment node
import { describe, expect, it } from "vitest";
import { buildOptions, buildPages, findPages, pageHtml } from "../build.mjs";

const pages = findPages();

describe("dialog page build", () => {
  it("finds every page folder with an index.tsx", () => {
    expect(pages.map((page) => page.name)).toEqual(["reopenTicket"]);
    expect(pages[0]!.script.replace(/\\/g, "/")).toMatch(/\/dist\/webresources\/pages\/reopenTicket\.js$/);
    expect(pages[0]!.html.replace(/\\/g, "/")).toMatch(/\/dist\/webresources\/pages\/reopenTicket\.html$/);
  });

  it("finds no pages in a package without src/pages", () => {
    expect(findPages(pages[0]!.entryPoint)).toEqual([]);
  });

  it("writes an HTML page that loads the script next to it", () => {
    const html = pageHtml("reopenTicket");

    expect(html).toContain('<div id="root"></div>');
    expect(html).toContain('<script src="reopenTicket.js"></script>');
  });

  it("builds minified production bundles, and readable ones for development", () => {
    expect(buildOptions(pages[0]!)).toMatchObject({ minify: true, jsx: "automatic", define: { "process.env.NODE_ENV": '"production"' } });
    expect(buildOptions(pages[0]!, { dev: true })).toMatchObject({ minify: false, sourcemap: "inline" });
  });

  it("bundles React, Fluent UI and the page into one production script", async () => {
    const [result] = await buildPages(pages, { write: false });
    const code = result!.outputFiles![0]!.text;

    expect(code).toContain("ogz_ReopenSupportTicket");
    expect(code).not.toContain("react.development");
  }, 60_000);

  it("fails without pages", async () => {
    await expect(buildPages([])).rejects.toThrow("no pages found");
  });
});
