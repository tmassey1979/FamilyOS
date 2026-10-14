export { cacheGet, cacheSet, cacheClearAll, type CacheEntry } from './cache';
export {
  enqueueMutation,
  getQueue,
  removeFromQueue,
  bumpAttempt,
  clearQueue,
  subscribeQueueCount,
  type QueuedMutation,
} from './queue';
export {
  isOnline,
  markOffline,
  markOnline,
  subscribeOnline,
  ensureStarted,
  isNetworkError,
} from './network';

import { getQueue, removeFromQueue, bumpAttempt, type QueuedMutation } from './queue';
import { isOnline, markOnline, markOffline, ensureStarted } from './network';

export type FlushResult = {
  flushed: number;
  failed: number;
  remaining: number;
};

type FlushExecutor = (item: QueuedMutation) => Promise<void>;

let flushing = false;

/**
 * Replay queued mutations. Caller supplies the actual HTTP executor
 * so we stay decoupled from the API client's auth headers.
 */
export async function flushMutationQueue(executor: FlushExecutor): Promise<FlushResult> {
  await ensureStarted();
  if (flushing) return { flushed: 0, failed: 0, remaining: (await getQueue()).length };
  flushing = true;
  let flushed = 0;
  let failed = 0;
  try {
    const items = await getQueue();
    for (const item of items) {
      if (!isOnline()) break;
      try {
        await executor(item);
        await removeFromQueue(item.id);
        flushed += 1;
        markOnline();
      } catch (e) {
        await bumpAttempt(item.id);
        failed += 1;
        // Stop on hard network failure; keep going on 4xx (drop after many attempts)
        const msg = e && typeof e === 'object' && 'message' in e ? String((e as Error).message) : '';
        if (/network/i.test(msg)) {
          markOffline();
          break;
        }
        if (item.attempts >= 4) {
          await removeFromQueue(item.id);
        }
      }
    }
  } finally {
    flushing = false;
  }
  const remaining = (await getQueue()).length;
  return { flushed, failed, remaining };
}
