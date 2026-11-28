import { IDBFactory as MemoryIDBFactory, IDBKeyRange as MemoryIDBKeyRange } from "fake-indexeddb";

function defineGlobal(name: "indexedDB" | "IDBKeyRange", value: unknown): void {
  Object.defineProperty(globalThis, name, {
    configurable: true,
    enumerable: false,
    value,
    writable: true,
  });
}

export function installMemoryIndexedDb(): IDBFactory {
  const factory = new MemoryIDBFactory();
  defineGlobal("indexedDB", factory);
  defineGlobal("IDBKeyRange", MemoryIDBKeyRange);
  return factory;
}

export function removeMemoryIndexedDb(): void {
  defineGlobal("indexedDB", undefined);
}
