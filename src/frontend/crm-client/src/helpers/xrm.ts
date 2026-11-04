// Internal access to the Dataverse runtime. The global `Xrm` is provided by the model-driven app and
// is never bundled.

export function getXrm(): Xrm.XrmStatic {
  const xrm = (globalThis as { Xrm?: Xrm.XrmStatic }).Xrm;
  if (xrm === undefined) {
    throw new Error("The Dataverse Client API (Xrm) is not available in this context.");
  }
  return xrm;
}

/** Converts the Client API's promise-like results into native promises (async/await, Promise.all). */
export function toPromise<T>(promiseLike: Xrm.Async.PromiseLike<T>): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    promiseLike.then(resolve, reject);
  });
}

/** "{ABC-...}" or "ABC-..." -> "abc-...", the form Xrm.WebApi and comparisons expect. */
export function normalizeId(id: string): string {
  return id.replace(/[{}]/g, "").toLowerCase();
}
