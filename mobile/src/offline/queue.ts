import { kvGet, kvSet } from './storage';

const QUEUE_KEY = 'familyos.mutationQueue.v1';

export type QueuedMutation = {
  id: string;
  path: string;
  method: string;
  body?: string | null;
  headers?: Record<string, string>;
  enqueuedAtUtc: string;
  attempts: number;
};

type Listener = (count: number) => void;
const listeners = new Set<Listener>();

function notify(count: number) {
  listeners.forEach((l) => {
    try {
      l(count);
    } catch {
      /* ignore */
    }
  });
}

export function subscribeQueueCount(listener: Listener): () => void {
  listeners.add(listener);
  void getQueue().then((q) => listener(q.length));
  return () => {
    listeners.delete(listener);
  };
}

export async function getQueue(): Promise<QueuedMutation[]> {
  try {
    const raw = await kvGet(QUEUE_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as QueuedMutation[];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

async function saveQueue(items: QueuedMutation[]): Promise<void> {
  await kvSet(QUEUE_KEY, JSON.stringify(items));
  notify(items.length);
}

function newId() {
  return `q_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 8)}`;
}

export async function enqueueMutation(input: {
  path: string;
  method: string;
  body?: string | null;
  headers?: Record<string, string>;
}): Promise<QueuedMutation> {
  const item: QueuedMutation = {
    id: newId(),
    path: input.path,
    method: input.method.toUpperCase(),
    body: input.body ?? null,
    headers: input.headers,
    enqueuedAtUtc: new Date().toISOString(),
    attempts: 0,
  };
  const q = await getQueue();
  q.push(item);
  // Cap queue length
  const trimmed = q.slice(-100);
  await saveQueue(trimmed);
  return item;
}

export async function removeFromQueue(id: string): Promise<void> {
  const q = await getQueue();
  await saveQueue(q.filter((x) => x.id !== id));
}

export async function bumpAttempt(id: string): Promise<void> {
  const q = await getQueue();
  const next = q.map((x) => (x.id === id ? { ...x, attempts: x.attempts + 1 } : x));
  await saveQueue(next);
}

export async function clearQueue(): Promise<void> {
  await saveQueue([]);
}
