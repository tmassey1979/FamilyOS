import { useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  Linking,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { Button, Card } from '../src/components/ui';
import { meApi } from '../src/api/client';
import { invitesApi, onboardingFamilyApi } from '../src/api/onboarding';
import { colors, spacing, typography } from '../src/theme';

type Mode = 'choose' | 'create' | 'join';

const SHOW_DEV_SKIP =
  typeof __DEV__ !== 'undefined' &&
  __DEV__ &&
  process.env.EXPO_PUBLIC_ALLOW_ONBOARDING_SKIP !== '0';

function extractInviteCode(url: string): string | null {
  try {
    const lower = url.toLowerCase();
    const inviteIdx = lower.indexOf('invite/');
    if (inviteIdx >= 0) {
      const rest = url.slice(inviteIdx + 'invite/'.length).split(/[?#/]/)[0];
      return rest ? rest.toUpperCase().replace(/\s/g, '') : null;
    }
    const q = url.split('?')[1];
    if (q) {
      const params = new URLSearchParams(q);
      const c = params.get('code');
      if (c) return c.toUpperCase().replace(/\s/g, '');
    }
  } catch {
    /* ignore */
  }
  return null;
}

export default function OnboardingScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ code?: string }>();
  const [mode, setMode] = useState<Mode>('choose');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [familyName, setFamilyName] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');

  const [code, setCode] = useState('');
  const [joinName, setJoinName] = useState('');
  const [preview, setPreview] = useState<{ familyName: string; role: string; isOpen: boolean } | null>(
    null,
  );

  useEffect(() => {
    const fromParam = typeof params.code === 'string' ? params.code : undefined;
    if (fromParam) {
      setCode(fromParam.toUpperCase().replace(/\s/g, ''));
      setMode('join');
    }
  }, [params.code]);

  useEffect(() => {
    const applyUrl = (url: string | null) => {
      if (!url) return;
      const c = extractInviteCode(url);
      if (c) {
        setCode(c);
        setMode('join');
      }
    };
    void Linking.getInitialURL().then(applyUrl);
    const sub = Linking.addEventListener('url', ({ url }) => applyUrl(url));
    return () => sub.remove();
  }, []);

  const goHome = () => router.replace('/(tabs)');
  const goSettings = () => router.replace('/(tabs)/settings');

  const createHousehold = async () => {
    if (!familyName.trim()) {
      setError('Give your household a name');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await onboardingFamilyApi.create({
        name: familyName.trim(),
        ownerDisplayName: displayName.trim() || undefined,
        ownerEmail: email.trim() || undefined,
        timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone,
      });
      await meApi.get().catch(() => null);
      Alert.alert(
        'Household ready',
        'Invite family from Settings, or open Home to add tasks and meals.',
        [
          { text: 'Home', onPress: goHome },
          { text: 'Invite now', onPress: goSettings },
        ],
      );
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not create household');
    } finally {
      setBusy(false);
    }
  };

  const lookupInvite = async () => {
    const c = code.trim().toUpperCase().replace(/\s/g, '');
    if (c.length < 6) {
      setError('Enter the full invite code');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const p = await invitesApi.preview(c);
      setPreview({ familyName: p.familyName, role: p.role, isOpen: p.isOpen });
      if (!p.isOpen) setError('This invite is no longer valid');
    } catch (e: unknown) {
      setPreview(null);
      setError((e as { error?: string })?.error ?? 'Invite not found');
    } finally {
      setBusy(false);
    }
  };

  const acceptInvite = async () => {
    const c = code.trim().toUpperCase().replace(/\s/g, '');
    setBusy(true);
    setError(null);
    try {
      const member = await invitesApi.accept(c, joinName.trim() || undefined);
      Alert.alert(
        `Welcome${preview?.familyName ? ` to ${preview.familyName}` : ''}`,
        `You joined as ${member.role}. Open Home to see what needs attention.`,
        [{ text: 'Continue', onPress: goHome }],
      );
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not join family');
    } finally {
      setBusy(false);
    }
  };

  return (
    <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
      <Text style={typography.heading}>Welcome to Family OS</Text>
      <Text style={styles.sub}>
        Set up your household or join one with an invite code from a parent or partner.
      </Text>

      {error ? <Text style={styles.error}>{error}</Text> : null}

      {mode === 'choose' ? (
        <View style={styles.gap}>
          <Card style={styles.card}>
            <Text style={typography.body}>I am starting a new household</Text>
            <Button title="Create household" onPress={() => setMode('create')} />
          </Card>
          <Card style={styles.card}>
            <Text style={typography.body}>I was invited to a family</Text>
            <Button title="Join with code" variant="secondary" onPress={() => setMode('join')} />
          </Card>
        </View>
      ) : null}

      {mode === 'create' ? (
        <Card style={styles.card}>
          <Text style={typography.heading}>Create household</Text>
          <TextInput
            style={styles.input}
            placeholder="Household name (e.g. The Hendersons)"
            placeholderTextColor={colors.textMuted}
            value={familyName}
            onChangeText={setFamilyName}
          />
          <TextInput
            style={styles.input}
            placeholder="Your display name"
            placeholderTextColor={colors.textMuted}
            value={displayName}
            onChangeText={setDisplayName}
          />
          <TextInput
            style={styles.input}
            placeholder="Email (optional)"
            placeholderTextColor={colors.textMuted}
            autoCapitalize="none"
            keyboardType="email-address"
            value={email}
            onChangeText={setEmail}
          />
          <View style={styles.row}>
            <Button title="Back" variant="secondary" onPress={() => setMode('choose')} />
            {busy ? (
              <ActivityIndicator color={colors.primary} />
            ) : (
              <Button title="Create" onPress={() => void createHousehold()} />
            )}
          </View>
        </Card>
      ) : null}

      {mode === 'join' ? (
        <Card style={styles.card}>
          <Text style={typography.heading}>Join with invite</Text>
          <TextInput
            style={styles.input}
            placeholder="Invite code"
            placeholderTextColor={colors.textMuted}
            autoCapitalize="characters"
            value={code}
            onChangeText={setCode}
          />
          <TextInput
            style={styles.input}
            placeholder="Your display name (optional)"
            placeholderTextColor={colors.textMuted}
            value={joinName}
            onChangeText={setJoinName}
          />
          {preview ? (
            <Text style={typography.caption}>
              {preview.familyName} · role {preview.role}
              {preview.isOpen ? '' : ' · closed'}
            </Text>
          ) : null}
          <View style={styles.row}>
            <Button title="Back" variant="secondary" onPress={() => setMode('choose')} />
            <Button title="Look up" variant="secondary" onPress={() => void lookupInvite()} />
            {busy ? (
              <ActivityIndicator color={colors.primary} />
            ) : (
              <Button title="Join" onPress={() => void acceptInvite()} />
            )}
          </View>
        </Card>
      ) : null}

      {SHOW_DEV_SKIP ? (
        <Pressable onPress={goHome} style={{ marginTop: spacing.lg }}>
          <Text style={styles.skip}>Skip for now (dev)</Text>
        </Pressable>
      ) : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flexGrow: 1,
    backgroundColor: colors.bg,
    padding: spacing.lg,
    gap: spacing.md,
    justifyContent: 'center',
  },
  sub: { ...typography.caption, marginBottom: spacing.md },
  gap: { gap: spacing.md },
  card: { gap: spacing.sm },
  input: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 8,
    padding: spacing.sm,
    color: colors.text,
    backgroundColor: colors.surface,
  },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, alignItems: 'center' },
  error: { ...typography.caption, color: colors.danger },
  skip: { ...typography.caption, color: colors.textMuted, textAlign: 'center' },
});
