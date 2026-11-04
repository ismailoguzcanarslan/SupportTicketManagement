// Model-driven app context.

import { getXrm, normalizeId, toPromise } from "./xrm.js";

export const App = {
  getProperties(): Promise<Xrm.AppProperties> {
    return toPromise(getXrm().Utility.getGlobalContext().getCurrentAppProperties());
  },

  async getAppId(): Promise<string | null> {
    const { appId } = await App.getProperties();
    return appId ? normalizeId(appId) : null;
  },

  /** Display name of the current app, falling back to its unique name. */
  async getAppName(): Promise<string | null> {
    const { displayName, uniqueName } = await App.getProperties();
    return displayName ?? uniqueName ?? null;
  },

  /** Base URL of the environment, e.g. "https://contoso.crm4.dynamics.com". */
  getClientUrl(): string {
    return getXrm().Utility.getGlobalContext().getClientUrl();
  },
} as const;
