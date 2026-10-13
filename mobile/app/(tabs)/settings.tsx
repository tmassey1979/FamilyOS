import { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Switch, Text, View,
} from 'react-native';
import { useRouter } from 'expo-router';
import { Badge, Button, Card, SectionTitle } from '../../src/components/ui';
import { familyApi, meApi, type MeDto } from '../../src/api/client';
import {
  isBiometricUnlockEnabled,
  setBiometricUnlockEnabled,
  signOut,
} from '../../src/auth/keycloak';
import { colors, spacing, typography } from '../../src/theme';

/**
 * Settings hub — account, family summary, security, sign out (#37).
 */
export default function SettingsScreen() {
  const router = useRouter();
  const [me, setMe] = useState<MeDto | null>(null);
  const [familyName, setFamilyName] = useState<string>('');
  const [memberCount, setMemberCount] = useState(0);
  const [bio, setBio] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setRefreshing(true);
    setError(null);
    try {
      const [who, fam, mems, bioOn] = await Promise.all([
        meApi.get().catch(() => null),
        familyApi.get().catch(() => null),
        familyApi.members().catch(() => []),
        isBiometricUnlockEnabled(),
      ]);
      setMe(who);
      setFamilyName(fam?.name ?? '');
      setMemberCount(mems.filter((m) => m.isActive !== false).length);
      setBio(bioOn);
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Unable to load settings');
    } finally {
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const onToggleBio = async (on: boolean) => {
    setBio(on);
    try {
      await setBiometricUnlockEnabled(on);
    } catch {
      setBio(!on);
      setError('Could not update biometric preference');
    }
  };

  const onSignOut = async () => {
    setBusy(true);
    try {
      await signOut();
      router.replace('/login');
    } catch {
      setError('Sign out failed');
    } finally {
      setBusy(false);
    }
  };

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void load()} />}
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

      <SectionTitle>Session</SectionTitle>
      <Card style={styles.card}>
        <Button
          title={busy ? 'Signing out…' : 'Sign out'}
          onPress={() => void onSignOut()}
        />
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
  rowBetween: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  hint: { ...typography.caption, color: colors.danger, marginBottom: spacing.sm },
});
