/**
 * Lightweight key-value store for offline cache/queue.
 * Prefer AsyncStorage; fall back to in-memory if the native module is unavailable.
 */
import AsyncStorage from '@react-native-async-storage/async-storage';

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

function resolveStore(): Store {
  try {
    if (AsyncStorage?.getItem) {
      return {
        getItem: (k) => AsyncStorage.getItem(k),
        setItem: (k, v) => AsyncStorage.setItem(k, v),
        removeItem: (k) => AsyncStorage.removeItem(k),
      };
    }
  } catch {
    /* fall through */
  }
  return memoryStore;
}

const store = resolveStore();

export async function kvGet(key: string): Promise<string | null> {
  return store.getItem(key);
}

export async function kvSet(key: string, value: string): Promise<void> {
  await store.setItem(key, value);
}

export async function kvRemove(key: string): Promise<void> {
  await store.removeItem(key);
}
