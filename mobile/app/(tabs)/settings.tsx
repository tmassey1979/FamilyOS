import { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator, Linking, Platform, Pressable, RefreshControl, ScrollView, StyleSheet, Switch, Text, View,
} from 'react-native';
import { useRouter } from 'expo-router';
import { Badge, Button, Card, EmptyState, SectionTitle } from '../../src/components/ui';
import {
  familyApi,
  meApi,
  notificationsApi,
  type DeviceRegistrationDto,
  type GoogleCalendarStatusDto,
  type MeDto,
  type NotificationDto,
  type QuietHoursDto,
} from '../../src/api/client';
import { invitesApi, type InviteDto } from '../../src/api/onboarding';
import { copyInviteCode, shareInviteCode } from '../../src/inviteShare';
import {
  isBiometricUnlockEnabled,
  setBiometricUnlockEnabled,
  signOut,
} from '../../src/auth/keycloak';
import { getStoredDeviceToken, resolveDevicePushToken } from '../../src/push/registerPush';
import { colors, spacing, typography } from '../../src/theme';

export default function SettingsScreen() {
  const router = useRouter();
  const [me, setMe] = useState<MeDto | null>(null);
  const [familyName, setFamilyName] = useState('');
  const [memberCount, setMemberCount] = useState(0);
  const [bio, setBio] = useState(false);
  const [quiet, setQuiet] = useState<QuietHoursDto | null>(null);
  const [quietOn, setQuietOn] = useState(false);
  const [quietStart, setQuietStart] = useState('22:00');
  const [quietEnd, setQuietEnd] = useState('07:00');
  const [notifs, setNotifs] = useState<NotificationDto[]>([]);
  const [devices, setDevices] = useState<DeviceRegistrationDto[]>([]);
  const [localToken, setLocalToken] = useState<string | null>(null);
  const [gcal, setGcal] = useState<GoogleCalendarStatusDto | null>(null);
  const [invites, setInvites] = useState<InviteDto[]>([]);
  const [lastInviteCode, setLastInviteCode] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [busy, setBusy] = useState(false);

  const canInvite = me?.role === 'Owner' || me?.role === 'Adult';

  const loadInvites = useCallback(async () => {
    if (!canInvite) {
      setInvites([]);
      return;
    }
    try {
      setInvites(await invitesApi.list());
    } catch {
      setInvites([]);
    }
  }, [canInvite]);

  const load = useCallback(async () => {
    setRefreshing(true);
    setError(null);
    try {
      const [who, fam, mems, bioOn, qh, tok, gc] = await Promise.all([
        meApi.get().catch(() => null),
        familyApi.get().catch(() => null),
        familyApi.members().catch(() => []),
        isBiometricUnlockEnabled(),
        meApi.getQuietHours().catch(() => null),
        getStoredDeviceToken(),
        meApi.googleCalendarStatus().catch(() => null),
      ]);
      setMe(who);
      setFamilyName(fam?.name ?? '');
      setMemberCount(mems.filter((m) => m.isActive !== false).length);
      setBio(bioOn);
      setLocalToken(tok);
      setGcal(gc);
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
  useEffect(() => {
    void loadInvites();
  }, [loadInvites]);

  const createInvite = async (role: string) => {
    setBusy(true);
    setError(null);
    try {
      const inv = await invitesApi.create({ role });
      setLastInviteCode(inv.code);
      await loadInvites();
      await copyInviteCode(inv.code);
      await shareInviteCode(inv.code, familyName || undefined);
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not create invite');
    } finally {
      setBusy(false);
    }
  };

  const revokeInvite = async (id: string) => {
    setBusy(true);
    try {
      await invitesApi.revoke(id);
      await loadInvites();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not revoke');
    } finally {
      setBusy(false);
    }
  };

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
        setError('Quiet hours need HH:MM start and end');
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
      const resolved = await resolveDevicePushToken();
      setLocalToken(resolved.token);
      await meApi.registerDevice({
        token: resolved.token,
        platform: resolved.platform,
        deviceName: resolved.deviceName,
      });
      if (!resolved.isExpoPush) {
        setError('Registered local device id (no Expo push token yet).');
      }
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
    try {
      await meApi.unregisterDevice(localToken);
      await loadDevices();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not unregister');
    } finally {
      setBusy(false);
    }
  };

  const connectGoogleCalendar = async () => {
    setBusy(true);
    try {
      const { url } = await meApi.googleCalendarAuthUrl();
      await Linking.openURL(url);
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not start Google Calendar');
    } finally {
      setBusy(false);
    }
  };

  const disconnectGoogleCalendar = async () => {
    setBusy(true);
    try {
      await meApi.disconnectGoogleCalendar();
      setGcal(await meApi.googleCalendarStatus().catch(() => null));
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not disconnect');
    } finally {
      setBusy(false);
    }
  };

  const syncGoogleCalendar = async () => {
    setBusy(true);
    try {
      await meApi.syncGoogleCalendar();
      setGcal(await meApi.googleCalendarStatus().catch(() => null));
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Sync failed');
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
          /* */
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
        setNotifs((prev) => prev.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)));
      } catch {
        /* */
      }
    }
    const kind = (n.relatedEntityType ?? '').toLowerCase();
    if (kind === 'request') router.push('/(tabs)/requests');
    else if (kind === 'task') router.push('/(tabs)/tasks');
    else if (kind === 'procurement' || kind === 'product' || kind === 'cart') router.push('/(tabs)/family');
    else if (kind === 'calendar' || kind === 'calendarevent') router.push('/(tabs)/calendar');
  };

  const thisDeviceRegistered = localToken ? devices.some((d) => d.isActive) : false;
  const isExpoPushToken =
    !!localToken &&
    (localToken.startsWith('ExponentPushToken') || localToken.startsWith('ExpoPushToken'));

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
            void loadInvites();
          }}
        />
      }
    >
      <SectionTitle>Account</SectionTitle>
      {error ? <Text style={styles.hint}>{error}</Text> : null}
      {refreshing && !me ? <ActivityIndicator color={colors.primary} /> : null}
      <Card style={styles.card}>
        <Text style={typography.heading}>{me?.role ? `Signed in as ${me.role}` : 'Not signed in'}</Text>
        {me?.memberId ? <Text style={typography.caption}>Member · {me.memberId.slice(0, 8)}…</Text> : null}
        {me?.familyId ? <Text style={typography.caption}>Family · {me.familyId.slice(0, 8)}…</Text> : null}
        {me?.role ? <Badge label={me.role} tone={me.role === 'Owner' ? 'success' : 'default'} /> : null}
      </Card>

      <SectionTitle>Household</SectionTitle>
      <Card style={styles.card}>
        <Text style={typography.heading}>{familyName || 'Family'}</Text>
        <Text style={typography.caption}>
          {memberCount} active member{memberCount === 1 ? '' : 's'}
        </Text>
      </Card>

      {canInvite ? (
        <>
          <SectionTitle>Invite family</SectionTitle>
          <Card style={styles.card}>
            <Text style={typography.caption}>
              Create a one-time code. Share or copy so someone can join this household.
            </Text>
            {lastInviteCode ? (
              <>
                <Text style={typography.heading}>Code · {lastInviteCode}</Text>
                <View style={styles.row}>
                  <Button
                    title="Share again"
                    variant="secondary"
                    onPress={() => void shareInviteCode(lastInviteCode, familyName || undefined)}
                  />
                  <Button title="Copy" variant="secondary" onPress={() => void copyInviteCode(lastInviteCode)} />
                </View>
              </>
            ) : null}
            <View style={styles.row}>
              <Button title={busy ? '…' : 'Invite Adult'} onPress={() => void createInvite('Adult')} />
              <Button title="Invite Teen" variant="secondary" onPress={() => void createInvite('Teen')} />
              <Button title="Invite Child" variant="secondary" onPress={() => void createInvite('Child')} />
            </View>
            {invites.filter((i) => i.isOpen).length === 0 ? (
              <Text style={typography.caption}>No open invites.</Text>
            ) : (
              invites
                .filter((i) => i.isOpen)
                .slice(0, 8)
                .map((i) => (
                  <View key={i.id} style={{ marginTop: spacing.xs }}>
                    <Text style={typography.body}>
                      {i.code} · {i.role}
                    </Text>
                    <Text style={typography.caption}>Expires {new Date(i.expiresAtUtc).toLocaleDateString()}</Text>
                    <View style={styles.row}>
                      <Button
                        title="Share"
                        variant="secondary"
                        onPress={() => void shareInviteCode(i.code, familyName || undefined)}
                      />
                      <Button title="Copy" variant="secondary" onPress={() => void copyInviteCode(i.code)} />
                      <Button title="Revoke" variant="secondary" onPress={() => void revokeInvite(i.id)} />
                    </View>
                  </View>
                ))
            )}
          </Card>
        </>
      ) : null}

      <SectionTitle>Security</SectionTitle>
      <Card style={styles.card}>
        <View style={styles.rowBetween}>
          <View style={{ flex: 1, paddingRight: spacing.sm }}>
            <Text style={typography.body}>Biometric unlock</Text>
            <Text style={typography.caption}>Require fingerprint / Face ID before using a stored session.</Text>
          </View>
          <Switch
            value={bio}
            onValueChange={(v) => void onToggleBio(v)}
            trackColor={{ true: colors.primary, false: colors.border }}
          />
        </View>
      </Card>

      <SectionTitle>Google Calendar</SectionTitle>
      <Card style={styles.card}>
        <Text style={typography.body}>
          {gcal?.connected
            ? `Connected · ${gcal.email ?? 'account'}`
            : gcal?.configured === false
              ? 'Not configured on server'
              : 'Not connected'}
        </Text>
        {gcal?.lastSyncAtUtc ? (
          <Text style={typography.caption}>Last sync {new Date(gcal.lastSyncAtUtc).toLocaleString()}</Text>
        ) : null}
        <View style={styles.row}>
          {gcal?.connected ? (
            <>
              <Button title={busy ? '…' : 'Sync now'} onPress={() => void syncGoogleCalendar()} />
              <Button title="Disconnect" variant="secondary" onPress={() => void disconnectGoogleCalendar()} />
            </>
          ) : (
            <Button title={busy ? '…' : 'Connect Google Calendar'} onPress={() => void connectGoogleCalendar()} />
          )}
        </View>
      </Card>

      <SectionTitle>This device</SectionTitle>
      <Card style={styles.card}>
        <Text style={typography.body}>Platform · {Platform.OS}</Text>
        {localToken ? (
          <Text style={typography.caption}>
            Token · {isExpoPushToken ? 'Expo · ' : 'local · '}{localToken.slice(0, 28)}…
          </Text>
        ) : null}
        <View style={styles.row}>
          <Button
            title={busy ? '…' : thisDeviceRegistered ? 'Refresh registration' : 'Register this device'}
            onPress={() => void registerThisDevice()}
          />
          {thisDeviceRegistered ? (
            <Button title="Unregister" variant="secondary" onPress={() => void unregisterThisDevice()} />
          ) : null}
        </View>
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
        <EmptyState title="No notifications" />
      ) : (
        notifs.slice(0, 15).map((n) => (
          <Pressable key={n.id} onPress={() => void openNotification(n)}>
            <Card style={{ marginBottom: spacing.sm, opacity: n.isRead ? 0.65 : 1 }}>
              <Text style={typography.heading}>{n.title}</Text>
              <Text style={typography.caption}>{n.body}</Text>
              <Badge label={n.type} />
            </Card>
          </Pressable>
        ))
      )}

      <SectionTitle>Quiet hours</SectionTitle>
      <Card style={styles.card}>
        <View style={styles.rowBetween}>
          <Text style={typography.body}>Do not disturb</Text>
          <Switch
            value={quietOn}
            onValueChange={(v) => void saveQuietHours(v)}
            trackColor={{ true: colors.primary, false: colors.border }}
          />
        </View>
        <Text style={typography.caption}>
          {quietStart} – {quietEnd} (tap times in a full build to cycle)
        </Text>
        {quietOn ? <Button title="Save window" variant="secondary" onPress={() => void saveQuietHours(true)} /> : null}
      </Card>

      <SectionTitle>Session</SectionTitle>
      <Card style={styles.card}>
        <Button title={busy ? 'Signing out…' : 'Sign out'} onPress={() => void onSignOut()} />
      </Card>
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
});
