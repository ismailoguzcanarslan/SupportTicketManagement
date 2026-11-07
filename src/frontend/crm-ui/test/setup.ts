// jsdom has no ResizeObserver; Fluent UI components (e.g. MessageBar) use it to adapt their layout.
class ResizeObserverStub {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}

if (globalThis.ResizeObserver === undefined) {
  Reflect.set(globalThis, "ResizeObserver", ResizeObserverStub);
}
