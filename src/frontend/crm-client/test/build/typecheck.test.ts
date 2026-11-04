// Runs the TypeScript compiler with the package's own tsconfig.json, the same check `npm run build`
// performs before bundling.

import * as ts from "typescript";
import { describe, expect, it } from "vitest";
import { findEntries } from "../../build.mjs";

const packageDir = findEntries()[0]!.entryPoint.replace(/[\\/]src[\\/](forms|commands)[\\/].+$/, "").replace(/\\/g, "/");

function loadConfig(configPath = `${packageDir}/tsconfig.json`): ts.ParsedCommandLine {
  const { config, error } = ts.readConfigFile(configPath, ts.sys.readFile);
  if (error) throw new Error(ts.flattenDiagnosticMessageText(error.messageText, "\n"));
  return ts.parseJsonConfigFileContent(config, ts.sys, configPath.slice(0, configPath.lastIndexOf("/")));
}

const messages = (diagnostics: readonly ts.Diagnostic[]) =>
  diagnostics.map((d) => ts.flattenDiagnosticMessageText(d.messageText, "\n"));

/** Type checks one in-memory file with the given options (no broken file is kept on disk). */
function checkSource(source: string, options: ts.CompilerOptions): readonly ts.Diagnostic[] {
  const fileName = `${packageDir}/src/__virtual__/check.ts`;
  const host = ts.createCompilerHost(options);
  const getSourceFile = host.getSourceFile;
  host.getSourceFile = (name, languageVersion, onError, shouldCreate) =>
    name === fileName ? ts.createSourceFile(name, source, languageVersion) : getSourceFile.call(host, name, languageVersion, onError, shouldCreate);
  const fileExists = host.fileExists;
  host.fileExists = (name) => name === fileName || fileExists.call(host, name);
  return ts.getPreEmitDiagnostics(ts.createProgram([fileName], options, host));
}

describe("TypeScript type checking", () => {
  it("passes for all sources with the strict project configuration", () => {
    const config = loadConfig();
    const program = ts.createProgram(config.fileNames, config.options);

    expect(config.options.strict).toBe(true);
    expect(config.fileNames.some((f) => f.endsWith("src/forms/supportTicket/index.ts"))).toBe(true);
    expect(messages(ts.getPreEmitDiagnostics(program))).toEqual([]);
  }, 60_000);

  it("reports invalid TypeScript", () => {
    const diagnostics = checkSource('export const count: number = "not a number";\n', loadConfig().options);

    expect(diagnostics.map((d) => d.code)).toContain(2322);
  }, 60_000);

  it("gives sources and tests the Xrm typings, so editors resolve the Xrm namespace in test files too", () => {
    for (const config of [loadConfig(), loadConfig(`${packageDir}/test/tsconfig.json`)]) {
      expect(config.options.types).toEqual(["xrm"]);
    }

    const diagnostics = checkSource("export const level: Xrm.Attributes.RequirementLevel = \"required\";\n", loadConfig(`${packageDir}/test/tsconfig.json`).options);
    expect(messages(diagnostics)).toEqual([]);
  }, 60_000);

  it("includes every test file and the sources in test/tsconfig.json", () => {
    const testConfig = loadConfig(`${packageDir}/test/tsconfig.json`);

    expect(testConfig.fileNames.some((f) => f.endsWith("test/mocks/xrm.ts"))).toBe(true);
    expect(testConfig.fileNames.some((f) => f.endsWith("test/build/bundle.test.ts"))).toBe(true);
    expect(testConfig.fileNames.some((f) => f.endsWith("src/forms/supportTicket/index.ts"))).toBe(true);
  });
});
