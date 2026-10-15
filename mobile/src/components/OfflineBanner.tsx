import { useEffect, useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import {
  ensureStarted,
  flushMutationQueue,
  getQueue,
  isOnline,
  subscribeOnline,
  subscribeQueueCount,
} from '../offline';
import { getAccessToken, getApiBaseUrl, getDevUser } from '../api/client';
import { colors, spacing, typography } from '../theme';

/**
 * Top-of-app banner when offline or when mutations are queued (#52).
 */
export function OfflineBanner() {
  const [online, setOnline] = useState(true);
  const [queued, setQueued] = useState(0);
  const [flushing, setFlushing] = useState(false);

  useEffect(() => {
    void ensureStarted();
    const unsubNet = subscribeOnline(setOnline);
    const unsubQ = subscribeQueueCount(setQueued);
    void getQueue().then((q) => setQueued(q.length));
    return () => {
      unsubNet();
      unsubQ();
    };
  }, []);

  // Auto-flush when we come back online
  useEffect(() => {
    if (!online || queued === 0) return;
    void doFlush();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [online]);

  const doFlush = async () => {
    if (flushing) return;
    setFlushing(true);
    try {
      await flushMutationQueue(async (item) => {
        const headers: Record<string, string> = {
          Accept: 'application/json',
          ...(item.body ? { 'Content-Type': 'application/json' } : {}),
        };
        const token = getAccessToken();
        if (token) headers.Authorization = `Bearer ${token}`;
        else if (getDevUser()) headers['X-Dev-User'] = getDevUser()!;

        const res = await fetch(`${getApiBaseUrl()}${item.path}`, {
          method: item.method,
          headers,
          body: item.body ?? undefined,
        });
        if (!res.ok) {
          const err = new Error(`HTTP ${res.status}`);
          throw err;
        }
      });
      setQueued((await getQueue()).length);
    } catch {
      /* banner stays */
    } finally {
      setFlushing(false);
    }
  };

  if (online && queued === 0) return null;

  const label = !online
    ? queued > 0
      ? `Offline · ${queued} change${queued === 1 ? '' : 's'} saved`
      : 'Offline · showing last known data'
    : flushing
      ? `Syncing ${queued}…`
      : `${queued} waiting to sync · Tap to retry`;

  return (
    <Pressable onPress={() => void doFlush()} disabled={!online || queued === 0}>
      <View style={[styles.bar, !online ? styles.offline : styles.queued]}>
        <Text style={styles.text}>{label}</Text>
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  bar: {
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
    alignItems: 'center',
  },
  offline: { backgroundColor: '#92400e' },
  queued: { backgroundColor: colors.primary },
  text: { ...typography.caption, color: '#fff', fontWeight: '600' },
});
