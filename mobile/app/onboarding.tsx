import { useState } from 'react';
import {
  ActivityIndicator,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { useRouter } from 'expo-router';
import { Button, Card } from '../src/components/ui';
import { familyApi, invitesApi, meApi } from '../src/api/client';
import { colors, spacing, typography } from '../src/theme';

type Mode = 'choose' | 'create' | 'join';

export default function OnboardingScreen() {
  const router = useRouter();
  const [mode, setMode] = useState<Mode>('choose');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Create household
  const [familyName, setFamilyName] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');

  // Join
  const [code, setCode] = useState('');
  const [joinName, setJoinName] = useState('');
  const [preview, setPreview] = useState<{ familyName: string; role: string; isOpen: boolean } | null>(null);

  const goHome = () => router.replace('/(tabs)');

  const createHousehold = async () => {
    if (!familyName.trim()) {
      setError('Give your household a name');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await familyApi.create({
        name: familyName.trim(),
        ownerDisplayName: displayName.trim() || undefined,
        ownerEmail: email.trim() || undefined,
        timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone,
      });
      await meApi.get().catch(() => null);
      goHome();
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
      await invitesApi.accept(c, joinName.trim() || undefined);
      goHome();
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

      <Pressable onPress={goHome} style={{ marginTop: spacing.lg }}>
        <Text style={styles.skip}>Skip for now (dev)</Text>
      </Pressable>
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
