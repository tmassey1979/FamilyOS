/**
 * Network reachability for offline-tolerant mobile (#52).
 * Uses NetInfo when available; otherwise infers from fetch failures.
 */
import NetInfo from '@react-native-community/netinfo';

type NetListener = (online: boolean) => void;

let online = true;
const listeners = new Set<NetListener>();
let started = false;

function setOnline(value: boolean) {
  if (online === value) return;
  online = value;
  listeners.forEach((l) => {
    try {
      l(online);
    } catch {
      /* ignore */
    }
  });
}

export function isOnline(): boolean {
  return online;
}

export function markOffline() {
  setOnline(false);
}

export function markOnline() {
  setOnline(true);
}

export function subscribeOnline(listener: NetListener): () => void {
  listeners.add(listener);
  listener(online);
  void ensureStarted();
  return () => {
    listeners.delete(listener);
  };
}

export async function ensureStarted(): Promise<void> {
  if (started) return;
  started = true;
  try {
    const state = await NetInfo.fetch();
    setOnline(state.isConnected !== false && state.isInternetReachable !== false);
    NetInfo.addEventListener((s) => {
      const next =
        s.isConnected !== false &&
        (s.isInternetReachable === null || s.isInternetReachable === true);
      setOnline(!!next);
    });
  } catch {
    // NetInfo unavailable — stay optimistic until a fetch fails
    setOnline(true);
  }
}

export function isNetworkError(err: unknown): boolean {
  if (!err) return false;
  if (typeof err === 'object' && err !== null) {
    const any = err as { name?: string; message?: string; status?: number };
    if (any.name === 'TypeError' && /network|fetch|failed/i.test(any.message ?? '')) return true;
    if (/network request failed/i.test(any.message ?? '')) return true;
    if (any.name === 'AbortError') return true;
  }
  return false;
}
