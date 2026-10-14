import { kvGet, kvSet, kvRemove } from './storage';

const PREFIX = 'familyos.cache.v1:';
const META = 'familyos.cache.meta.v1';

export type CacheEntry<T = unknown> = {
  path: string;
  data: T;
  savedAtUtc: string;
};

function keyFor(path: string) {
  // Strip query noise that is volatile (e.g. random) but keep meaningful filters
  return `${PREFIX}${path}`;
}

export async function cacheGet<T>(path: string): Promise<CacheEntry<T> | null> {
  try {
    const raw = await kvGet(keyFor(path));
    if (!raw) return null;
    return JSON.parse(raw) as CacheEntry<T>;
  } catch {
    return null;
  }
}

export async function cacheSet<T>(path: string, data: T): Promise<void> {
  const entry: CacheEntry<T> = {
    path,
    data,
    savedAtUtc: new Date().toISOString(),
  };
  try {
    await kvSet(keyFor(path), JSON.stringify(entry));
    const metaRaw = await kvGet(META);
    const meta: string[] = metaRaw ? (JSON.parse(metaRaw) as string[]) : [];
    if (!meta.includes(path)) {
      meta.push(path);
      // Cap tracked paths
      const trimmed = meta.slice(-80);
      await kvSet(META, JSON.stringify(trimmed));
    }
  } catch {
    /* best-effort */
  }
}

export async function cacheClearAll(): Promise<void> {
  try {
    const metaRaw = await kvGet(META);
    const meta: string[] = metaRaw ? (JSON.parse(metaRaw) as string[]) : [];
    for (const path of meta) {
      await kvRemove(keyFor(path));
    }
    await kvRemove(META);
  } catch {
    /* ignore */
  }
}
