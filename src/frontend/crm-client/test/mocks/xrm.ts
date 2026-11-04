// Reusable test double for the global `Xrm` object (Xrm.WebApi, Xrm.Navigation, Xrm.Utility, Xrm.App).
//
// Every method is a `vi.fn` with a harmless default result; override per test, e.g.
//   vi.mocked(xrm.WebApi.retrieveMultipleRecords).mockResolvedValueOnce({ entities: [...] });

import { vi } from "vitest";

export interface GlobalContextMockOptions {
  clientUrl?: string;
  userId?: string;
  userName?: string;
  languageId?: number;
  version?: string;
  appProperties?: Xrm.AppProperties;
}

export function createGlobalContextMock(options: GlobalContextMockOptions = {}): Xrm.GlobalContext {
  const userId = options.userId ?? "{00000000-0000-0000-0000-000000000001}";
  const userName = options.userName ?? "Test User";

  return {
    getClientUrl: vi.fn(() => options.clientUrl ?? "https://test.crm.dynamics.com"),
    getVersion: vi.fn(() => options.version ?? "9.2.0.0"),
    getCurrentAppUrl: vi.fn(() => options.clientUrl ?? "https://test.crm.dynamics.com"),
    getUserId: vi.fn(() => userId),
    getUserName: vi.fn(() => userName),
    getCurrentAppProperties: vi.fn(() =>
      Promise.resolve(options.appProperties ?? { appId: "{AAAAAAAA-0000-0000-0000-000000000001}", displayName: "Support Hub", uniqueName: "ogz_SupportHub" }),
    ),
    userSettings: {
      userId,
      userName,
      languageId: options.languageId ?? 1033,
    },
  } as unknown as Xrm.GlobalContext;
}

export function createXrmMock(globalContext: Xrm.GlobalContext = createGlobalContextMock()): Xrm.XrmStatic {
  return {
    WebApi: {
      createRecord: vi.fn((entityLogicalName: string) =>
        Promise.resolve({ entityType: entityLogicalName, id: "00000000-0000-0000-0000-000000000000" }),
      ),
      retrieveRecord: vi.fn(() => Promise.resolve({})),
      retrieveMultipleRecords: vi.fn(() => Promise.resolve({ entities: [] })),
      updateRecord: vi.fn((entityLogicalName: string, id: string) => Promise.resolve({ entityType: entityLogicalName, id })),
      deleteRecord: vi.fn((entityLogicalName: string, id: string) => Promise.resolve({ entityType: entityLogicalName, id })),
      isAvailableOffline: vi.fn(() => false),
    },
    Navigation: {
      openAlertDialog: vi.fn(() => Promise.resolve()),
      openConfirmDialog: vi.fn(() => Promise.resolve({ confirmed: false })),
      openErrorDialog: vi.fn(() => Promise.resolve()),
      openForm: vi.fn(() => Promise.resolve({ savedEntityReference: [] })),
      openUrl: vi.fn(),
      navigateTo: vi.fn(() => Promise.resolve()),
    },
    Utility: {
      getGlobalContext: vi.fn(() => globalContext),
      showProgressIndicator: vi.fn(),
      closeProgressIndicator: vi.fn(),
      getResourceString: vi.fn((_webResourceName: string, key: string) => key),
      lookupObjects: vi.fn(() => Promise.resolve([])),
      getEntityMetadata: vi.fn(() => Promise.resolve({})),
    },
    App: {
      addGlobalNotification: vi.fn(() => Promise.resolve("notification-id")),
      clearGlobalNotification: vi.fn(() => Promise.resolve()),
    },
  } as unknown as Xrm.XrmStatic;
}

/** Installs a fresh mock as the global `Xrm` and returns it. Removed again after each test (see setup.ts). */
export function installXrmMock(xrm: Xrm.XrmStatic = createXrmMock()): Xrm.XrmStatic {
  Reflect.set(globalThis, "Xrm", xrm);
  return xrm;
}

export function uninstallXrmMock(): void {
  Reflect.deleteProperty(globalThis, "Xrm");
}
