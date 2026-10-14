/**
 * Lightweight key-value store for offline cache/queue.
 * Prefer AsyncStorage; fall back to in-memory if the native module is unavailable.
 */

type Store = {
  getItem: (key: string) => Promise<string | null>;
  setItem: (key: string, value: string) => Promise<void>;
  removeItem: (key: string) => Promise<void>;
};

const memory = new Map<string, string>();

const memoryStore: Store = {
  getItem: async (key) => memory.get(key) ?? null,
  setItem: async (key, value) => {
    memory.set(key, value);
  },
  removeItem: async (key) => {
    memory.delete(key);
  },
};

let store: Store = memoryStore;
let resolved = false;

async function resolveStore(): Promise<Store> {
  if (resolved) return store;
  resolved = true;
  try {
    // Dynamic import so typecheck works even if types lag; Expo bundles the module.
    const mod = await import('@react-native-async-storage/async-storage');
    const AsyncStorage = mod.default;
    if (AsyncStorage?.getItem) {
      store = {
        getItem: (k) => AsyncStorage.getItem(k),
        setItem: (k, v) => AsyncStorage.setItem(k, v),
        removeItem: (k) => AsyncStorage.removeItem(k),
      };
    }
  } catch {
    store = memoryStore;
  }
  return store;
}

export async function kvGet(key: string): Promise<string | null> {
  const s = await resolveStore();
  return s.getItem(key);
}

export async function kvSet(key: string, value: string): Promise<void> {
  const s = await resolveStore();
  await s.setItem(key, value);
}

export async function kvRemove(key: string): Promise<void> {
  const s = await resolveStore();
  await s.removeItem(key);
}
