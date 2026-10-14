import { useEffect, useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import {
  ensureStarted,
  flushMutationQueue,
  isOnline,
  subscribeOnline,
  subscribeQueueCount,
  type QueuedMutation,
} from '../offline';
import { getAccessToken, getApiBaseUrl, getDevUser } from '../api/client';
import { colors, spacing, typography } from '../theme';

async function replayItem(item: QueuedMutation): Promise<void> {
  const headers: Record<string, string> = {
    Accept: 'application/json',
    ...(item.body ? { 'Content-Type': 'application/json' } : {}),
    ...(item.headers ?? {}),
  };
  const token = getAccessToken();
  const dev = getDevUser();
  if (token) headers.Authorization = `Bearer ${token}`;
  else if (dev) headers['X-Dev-User'] = dev;

  const res = await fetch(`${getApiBaseUrl()}${item.path}`, {
    method: item.method,
    headers,
    body: item.body ?? undefined,
  });
  if (!res.ok) {
    const err = new Error(`HTTP ${res.status}`);
    throw err;
  }
}

export function OfflineBanner() {
  const [online, setOnline] = useState(true);
  const [pending, setPending] = useState(0);
  const [syncing, setSyncing] = useState(false);
  const [hint, setHint] = useState<string | null>(null);

  useEffect(() => {
    void ensureStarted();
    const offNet = subscribeOnline(setOnline);
    const offQ = subscribeQueueCount(setPending);
    return () => {
      offNet();
      offQ();
    };
  }, []);

  useEffect(() => {
    if (!online || pending === 0) return;
    let cancelled = false;
    (async () => {
      setSyncing(true);
      try {
        const result = await flushMutationQueue(replayItem);
        if (!cancelled && result.flushed > 0) {
          setHint(`Synced ${result.flushed} change${result.flushed === 1 ? '' : 's'}`);
          setTimeout(() => setHint(null), 2500);
        }
      } finally {
        if (!cancelled) setSyncing(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [online, pending]);

  if (online && pending === 0 && !hint) return null;

  const label = !online
    ? pending > 0
      ? `Offline · ${pending} waiting to sync`
      : 'Offline · showing cached data'
    : syncing
      ? 'Syncing…'
      : hint
        ? hint
        : pending > 0
          ? `${pending} change${pending === 1 ? '' : 's'} pending`
          : null;

  if (!label) return null;

  return (
    <Pressable
      accessibilityRole="summary"
      onPress={() => {
        if (online && pending > 0 && !syncing) {
          setSyncing(true);
          void flushMutationQueue(replayItem).finally(() => setSyncing(false));
        }
      }}
      style={[styles.banner, !online ? styles.offline : styles.pending]}
    >
      <Text style={styles.text}>{label}</Text>
      {online && pending > 0 && !syncing ? (
        <Text style={styles.action}>Tap to sync</Text>
      ) : null}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  banner: {
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: spacing.sm,
  },
  offline: { backgroundColor: '#7C2D12' },
  pending: { backgroundColor: '#1E3A5F' },
  text: { ...typography.caption, color: colors.text, flex: 1 },
  action: { ...typography.caption, color: colors.primary, fontWeight: '700' },
});

// silence unused isOnline import if tree-shaken oddly
void isOnline;
