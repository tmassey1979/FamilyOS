import { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Image,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { useRouter } from 'expo-router';
import * as AuthSession from 'expo-auth-session';
import * as WebBrowser from 'expo-web-browser';
import { colors, spacing, typography } from '../src/theme';
import { Button, Card } from '../src/components/ui';
import {
  discovery,
  exchangeCode,
  persistTokens,
  setBiometricUnlockEnabled,
  useAuthRequestConfig,
  getKeycloakAuthority,
} from '../src/auth/keycloak';
import { setDevUser } from '../src/api/client';

WebBrowser.maybeCompleteAuthSession();

const USE_DEV =
  typeof __DEV__ !== 'undefined' &&
  __DEV__ &&
  process.env.EXPO_PUBLIC_ALLOW_DEV_LOGIN !== '0';

export default function LoginScreen() {
  const router = useRouter();
  const cfg = useAuthRequestConfig();
  const [request, response, promptAsync] = AuthSession.useAuthRequest(
    {
      clientId: cfg.clientId,
      redirectUri: cfg.redirectUri,
      scopes: cfg.scopes,
      usePKCE: true,
    },
    discovery,
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const finish = useCallback(
    async (access: string, refresh?: string) => {
      await persistTokens(access, refresh);
      await setBiometricUnlockEnabled(true);
      router.replace('/(tabs)');
    },
    [router],
  );

  useEffect(() => {
    if (response?.type !== 'success') return;
    const code = response.params.code;
    if (!code || !request?.codeVerifier) {
      setError('Missing authorization code');
      return;
    }
    setBusy(true);
    setError(null);
    void exchangeCode(code, request.codeVerifier, cfg.redirectUri)
      .then((tokens) => finish(tokens.access_token, tokens.refresh_token))
      .catch((e: Error) => setError(e.message))
      .finally(() => setBusy(false));
  }, [response, request, cfg.redirectUri, finish]);

  const signIn = async () => {
    setError(null);
    setBusy(true);
    try {
      await promptAsync({ showInRecents: true });
    } catch (e: unknown) {
      setError((e as Error)?.message ?? 'Sign-in failed');
    } finally {
      setBusy(false);
    }
  };

  const devLogin = (user: string) => {
    setDevUser(user);
    router.replace('/(tabs)');
  };

  return (
    <View style={styles.root}>
      <View style={styles.hero}>
        <Image
          source={require('../assets/brand/play-icon-512.png')}
          style={styles.logo}
          resizeMode="contain"
        />
        <Text style={styles.title}>Family OS</Text>
        <Text style={styles.sub}>Sign in with your family account</Text>
      </View>

      <Card style={styles.card}>
        <Text style={typography.caption}>
          Authority: {getKeycloakAuthority().replace(/^https?:\/\//, '')}
        </Text>
        {error ? <Text style={styles.error}>{error}</Text> : null}
        {busy ? (
          <ActivityIndicator color={colors.primary} style={{ marginVertical: spacing.md }} />
        ) : (
          <Button title="Sign in with Keycloak" onPress={() => void signIn()} disabled={!request} />
        )}
        <Text style={[typography.caption, { marginTop: spacing.md }]}>
          After password, Keycloak can prompt for fingerprint / Face ID (WebAuthn) if you enrolled a
          device authenticator in Account settings.
        </Text>
      </Card>

      {USE_DEV ? (
        <Card style={styles.card}>
          <Text style={typography.caption}>Dev only — X-Dev-User (API Auth:UseDevBypass)</Text>
          <View style={styles.row}>
            {['terry.owner', 'michelle.adult', 'mia.teen', 'eli.child'].map((u) => (
              <Pressable key={u} onPress={() => devLogin(u)} style={styles.chip}>
                <Text style={typography.body}>{u.split('.')[0]}</Text>
              </Pressable>
            ))}
          </View>
        </Card>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  root: {
    flex: 1,
    backgroundColor: colors.bg ?? '#0F172A',
    padding: spacing.lg,
    justifyContent: 'center',
    gap: spacing.md,
  },
  hero: { alignItems: 'center', marginBottom: spacing.lg },
  logo: { width: 88, height: 88, borderRadius: 20, marginBottom: spacing.sm },
  title: { ...typography.heading, color: '#F8FAFC', fontSize: 28 },
  sub: { ...typography.caption, color: '#94A3B8' },
  card: { gap: spacing.sm },
  error: { color: '#EF4444', marginBottom: spacing.sm },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, marginTop: spacing.sm },
  chip: {
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
    backgroundColor: '#1E293B',
    borderRadius: 8,
  },
});
