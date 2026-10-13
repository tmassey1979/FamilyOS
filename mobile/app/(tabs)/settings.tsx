import { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator, Platform, Pressable, RefreshControl, ScrollView, StyleSheet, Switch, Text, View,
} from 'react-native';
import { useRouter } from 'expo-router';
import * as SecureStore from 'expo-secure-store';
import { Badge, Button, Card, EmptyState, SectionTitle } from '../../src/components/ui';
import {
  familyApi,
  meApi,
  notificationsApi,
  type DeviceRegistrationDto,
  type MeDto,
  type NotificationDto,
  type QuietHoursDto,
} from '../../src/api/client';
import {
  isBiometricUnlockEnabled,
  setBiometricUnlockEnabled,
  signOut,
} from '../../src/auth/keycloak';
import { colors, spacing, typography } from '../../src/theme';

const DEVICE_TOKEN_KEY = 'familyos.device.localToken';

async function getOrCreateLocalDeviceToken(): Promise<string> {
  const existing = await SecureStore.getItemAsync(DEVICE_TOKEN_KEY);
  if (existing) return existing;
  const token = `local-${Platform.OS}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
  await SecureStore.setItemAsync(DEVICE_TOKEN_KEY, token);
  return token;
}

/**
 * Settings hub — account, family, security, notifications, quiet hours, devices (#37 #46 #50 #27).
 * Tap a notification to mark read and deep-link to the related tab (#9 #55).
 */
export default function SettingsScreen() {
  const router = useRouter();
  const [me, setMe] = useState<MeDto | null>(null);
  const [familyName, setFamilyName] = useState<string>('');
  const [memberCount, setMemberCount] = useState(0);
  const [bio, setBio] = useState(false);
  const [quiet, setQuiet] = useState<QuietHoursDto | null>(null);
  const [quietOn, setQuietOn] = useState(false);
  const [quietStart, setQuietStart] = useState('22:00');
  const [quietEnd, setQuietEnd] = useState('07:00');
  const [notifs, setNotifs] = useState<NotificationDto[]>([]);
  const [devices, setDevices] = useState<DeviceRegistrationDto[]>([]);
  const [localToken, setLocalToken] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setRefreshing(true);
    setError(null);
    try {
      const [who, fam, mems, bioOn, qh, tok] = await Promise.all([
        meApi.get().catch(() => null),
        familyApi.get().catch(() => null),
        familyApi.members().catch(() => []),
        isBiometricUnlockEnabled(),
        meApi.getQuietHours().catch(() => null),
        getOrCreateLocalDeviceToken(),
      ]);
      setMe(who);
      setFamilyName(fam?.name ?? '');
      setMemberCount(mems.filter((m) => m.isActive !== false).length);
      setBio(bioOn);
      setLocalToken(tok);
      if (qh) {
        setQuiet(qh);
        setQuietOn(qh.enabled);
        if (qh.startLabel) setQuietStart(qh.startLabel);
        if (qh.endLabel) setQuietEnd(qh.endLabel);
      }
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Unable to load settings');
    } finally {
      setRefreshing(false);
    }
  }, []);

  const loadNotifs = useCallback(async () => {
    try {
      setNotifs(await notificationsApi.mine());
    } catch {
      setNotifs([]);
    }
  }, []);

  const loadDevices = useCallback(async () => {
    try {
      setDevices(await meApi.myDevices());
    } catch {
      setDevices([]);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    void loadNotifs();
  }, [loadNotifs]);

  useEffect(() => {
    void loadDevices();
  }, [loadDevices]);

  const onToggleBio = async (on: boolean) => {
    setBio(on);
    try {
      await setBiometricUnlockEnabled(on);
    } catch {
      setBio(!on);
      setError('Could not update biometric preference');
    }
  };

  const parseHm = (s: string): number | null => {
    const m = /^(\d{1,2}):(\d{2})$/.exec(s.trim());
    if (!m) return null;
    const h = Number(m[1]);
    const min = Number(m[2]);
    if (h > 23 || min > 59) return null;
    return h * 60 + min;
  };

  const saveQuietHours = async (enabled: boolean) => {
    setQuietOn(enabled);
    try {
      const start = parseHm(quietStart);
      const end = parseHm(quietEnd);
      if (enabled && (start == null || end == null)) {
        setError('Quiet hours need HH:MM start and end (e.g. 22:00 and 07:00)');
        setQuietOn(!enabled);
        return;
      }
      const qh = await meApi.setQuietHours({
        enabled,
        startMinutes: enabled ? start : null,
        endMinutes: enabled ? end : null,
      });
      setQuiet(qh);
      setError(null);
    } catch (e: unknown) {
      setQuietOn(!enabled);
      setError((e as { error?: string })?.error ?? 'Could not save quiet hours');
    }
  };

  const registerThisDevice = async () => {
    setBusy(true);
    setError(null);
    try {
      const token = localToken ?? (await getOrCreateLocalDeviceToken());
      setLocalToken(token);
      await meApi.registerDevice({
        token,
        platform: Platform.OS,
        deviceName: `${Platform.OS} device`,
      });
      await loadDevices();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not register device');
    } finally {
      setBusy(false);
    }
  };

  const unregisterThisDevice = async () => {
    if (!localToken) return;
    setBusy(true);
    setError(null);
    try {
      await meApi.unregisterDevice(localToken);
      await loadDevices();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not unregister device');
    } finally {
      setBusy(false);
    }
  };

  const onSignOut = async () => {
    setBusy(true);
    try {
      if (localToken) {
        try {
          await meApi.unregisterDevice(localToken);
        } catch {
          /* best-effort */
        }
      }
      await signOut();
      router.replace('/login');
    } catch {
      setError('Sign out failed');
    } finally {
      setBusy(false);
    }
  };

  const openNotification = async (n: NotificationDto) => {
    if (!n.isRead) {
      try {
        await notificationsApi.markRead(n.id);
        setNotifs((prev) =>
          prev.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)),
        );
      } catch {
        /* still navigate */
      }
    }

    const kind = (n.relatedEntityType ?? '').toLowerCase();
    if (kind === 'request') {
      router.push('/(tabs)/requests');
    } else if (kind === 'task') {
      router.push('/(tabs)/tasks');
    } else if (kind === 'procurement' || kind === 'product' || kind === 'cart') {
      router.push('/(tabs)/family');
    } else if (kind === 'calendar' || kind === 'calendarevent') {
      router.push('/(tabs)/calendar');
    }
    // else stay on Settings (general notices)
  };

  const thisDeviceRegistered = localToken
    ? devices.some((d) => d.isActive)
    : false;

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={
        <RefreshControl
          refreshing={refreshing}
          onRefresh={() => {
            void load();
            void loadNotifs();
            void loadDevices();
          }}
        />
      }
    >
      <SectionTitle>Account</SectionTitle>
      {error ? <Text style={styles.hint}>{error}</Text> : null}
      {refreshing && !me ? <ActivityIndicator color={colors.primary} /> : null}
      <Card style={styles.card}>
        <Text style={typography.heading}>{me?.role ? `Signed in as ${me.role}` : 'Not signed in'}</Text>
        {me?.memberId ? (
          <Text style={typography.caption}>Member · {me.memberId.slice(0, 8)}…</Text>
        ) : null}
        {me?.familyId ? (
          <Text style={typography.caption}>Family · {me.familyId.slice(0, 8)}…</Text>
        ) : null}
        {me?.externalIdentityId ? (
          <Text style={typography.caption}>Id · {me.externalIdentityId}</Text>
        ) : null}
        {me?.role ? <Badge label={me.role} tone={me.role === 'Owner' ? 'success' : 'default'} /> : null}
      </Card>

      <SectionTitle>Household</SectionTitle>
      <Card style={styles.card}>
        <Text style={typography.heading}>{familyName || 'Family'}</Text>
        <Text style={typography.caption}>
          {memberCount} active member{memberCount === 1 ? '' : 's'}
        </Text>
        <Text style={typography.caption}>
          Manage members and shopping from the Family tab.
        </Text>
      </Card>

      <SectionTitle>Security</SectionTitle>
      <Card style={styles.card}>
        <View style={styles.rowBetween}>
          <View style={{ flex: 1, paddingRight: spacing.sm }}>
            <Text style={typography.body}>Biometric unlock</Text>
            <Text style={typography.caption}>
              Require fingerprint / Face ID before using a stored session.
            </Text>
          </View>
          <Switch
            value={bio}
            onValueChange={(v) => void onToggleBio(v)}
            trackColor={{ true: colors.primary, false: colors.border }}
          />
        </View>
      </Card>

      <SectionTitle>This device</SectionTitle>
      <Card style={styles.card}>
        <Text style={typography.body}>Platform · {Platform.OS}</Text>
        <Text style={typography.caption}>
          Registers a local device id for push foundation (#27). Expo push tokens can replace this later.
        </Text>
        {localToken ? (
          <Text style={typography.caption}>Token · {localToken.slice(0, 24)}…</Text>
        ) : null}
        <View style={styles.row}>
          <Button
            title={busy ? 'Working…' : thisDeviceRegistered ? 'Refresh registration' : 'Register this device'}
            onPress={() => void registerThisDevice()}
          />
          {thisDeviceRegistered ? (
            <Button title="Unregister" variant="secondary" onPress={() => void unregisterThisDevice()} />
          ) : null}
        </View>
        {devices.length === 0 ? (
          <Text style={typography.caption}>No active devices on server yet.</Text>
        ) : (
          devices.map((d) => (
            <View key={d.id} style={{ marginTop: spacing.xs }}>
              <Text style={typography.caption}>
                {d.platform} · {d.deviceName ?? 'unnamed'} · {d.isActive ? 'active' : 'off'}
              </Text>
              <Text style={typography.caption}>
                Last seen {new Date(d.lastSeenAtUtc).toLocaleString()}
              </Text>
            </View>
          ))
        )}
      </Card>

      <SectionTitle>In-app notifications</SectionTitle>
      <View style={styles.row}>
        <Button
          title="Mark all read"
          variant="secondary"
          onPress={() => void notificationsApi.markAllRead().then(loadNotifs).catch(() => {})}
        />
        <Button title="Refresh" variant="secondary" onPress={() => void loadNotifs()} />
      </View>
      {notifs.length === 0 ? (
        <EmptyState title="No notifications" subtitle="Approvals and assignments will show here." />
      ) : (
        notifs.slice(0, 15).map((n) => (
          <Pressable key={n.id} onPress={() => void openNotification(n)}>
            <Card style={{ marginBottom: spacing.sm, gap: spacing.xs, opacity: n.isRead ? 0.65 : 1 }}>
              <Text style={typography.heading}>{n.title}</Text>
              <Text style={typography.caption}>{n.body}</Text>
              <View style={styles.row}>
                <Badge label={n.type} />
                {!n.isRead ? <Badge label="Unread" tone="warning" /> : null}
                {n.relatedEntityType ? <Badge label={n.relatedEntityType} /> : null}
              </View>
              <Text style={typography.caption}>{new Date(n.createdAtUtc).toLocaleString()}</Text>
              {n.relatedEntityType ? (
                <Text style={[typography.caption, { color: colors.primary }]}>
                  Tap to open {n.relatedEntityType} →
                </Text>
              ) : null}
            </Card>
          </Pressable>
        ))
      )}

      <SectionTitle>Quiet hours</SectionTitle>
      <Card style={styles.card}>
        <View style={styles.rowBetween}>
          <View style={{ flex: 1, paddingRight: spacing.sm }}>
            <Text style={typography.body}>Do not disturb</Text>
            <Text style={typography.caption}>
              Suppress non-urgent alerts overnight (server-side).
            </Text>
          </View>
          <Switch
            value={quietOn}
            onValueChange={(v) => void saveQuietHours(v)}
            trackColor={{ true: colors.primary, false: colors.border }}
          />
        </View>
        <Text style={typography.caption}>Start (HH:MM)</Text>
        <Text
          style={[typography.body, styles.timeChip]}
          onPress={() => {
            const presets = ['21:00', '22:00', '23:00', '00:00'];
            const i = presets.indexOf(quietStart);
            setQuietStart(presets[(i + 1) % presets.length]);
          }}
        >
          {quietStart}  (tap to cycle)
        </Text>
        <Text style={typography.caption}>End (HH:MM)</Text>
        <Text
          style={[typography.body, styles.timeChip]}
          onPress={() => {
            const presets = ['06:00', '07:00', '08:00', '09:00'];
            const i = presets.indexOf(quietEnd);
            setQuietEnd(presets[(i + 1) % presets.length]);
          }}
        >
          {quietEnd}  (tap to cycle)
        </Text>
        {quietOn ? (
          <Button title="Save window" variant="secondary" onPress={() => void saveQuietHours(true)} />
        ) : null}
        {quiet?.startLabel && quiet?.endLabel ? (
          <Text style={typography.caption}>
            Active window: {quiet.startLabel} – {quiet.endLabel}
          </Text>
        ) : null}
      </Card>

      <SectionTitle>Session</SectionTitle>
      <Card style={styles.card}>
        <Button title={busy ? 'Signing out…' : 'Sign out'} onPress={() => void onSignOut()} />
        <Pressable onPress={() => router.push('/(tabs)/family')}>
          <Text style={[typography.caption, { marginTop: spacing.sm, color: colors.primary }]}>
            Open Family settings →
          </Text>
        </Pressable>
      </Card>

      <Text style={[typography.caption, { marginTop: spacing.lg, opacity: 0.6 }]}>
        Family OS · Settings hub
      </Text>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  content: { padding: spacing.md, paddingBottom: spacing.xl * 2, gap: spacing.sm },
  card: { marginBottom: spacing.md, gap: spacing.sm },
  row: { flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap', marginBottom: spacing.sm },
  rowBetween: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  hint: { ...typography.caption, color: colors.danger, marginBottom: spacing.sm },
  timeChip: {
    padding: spacing.sm,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.bg,
  },
});
