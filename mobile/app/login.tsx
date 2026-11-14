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
import { meApi, setAccessToken, setDevUser } from '../src/api/client';
import { withObserveScreen } from '../src/analytics/withObserveScreen';

WebBrowser.maybeCompleteAuthSession();

const USE_DEV =
  typeof __DEV__ !== 'undefined' &&
  __DEV__ &&
  process.env.EXPO_PUBLIC_ALLOW_DEV_LOGIN !== '0';

function LoginScreen() {
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
      setAccessToken(access);
      await setBiometricUnlockEnabled(true);
      try {
        const me = await meApi.get();
        router.replace(me.familyId ? '/(tabs)' : '/onboarding');
      } catch {
        router.replace('/onboarding');
      }
    },
    [router],
  );

  useEffect(() => {
    if (!response) return;

    if (response.type === 'error') {
      const message =
        response.params?.error_description ??
        response.params?.error ??
        'The identity provider returned an authorization error.';
      setError(`Sign-in did not complete: ${message}`);
      setBusy(false);
      return;
    }

    if (response.type === 'dismiss' || response.type === 'cancel') {
      setError('Sign-in was closed before Family OS received the authorization redirect. Try again.');
      setBusy(false);
      return;
    }

    if (response.type !== 'success') return;

    const code = response.params.code;
    if (!code || !request?.codeVerifier) {
      const providerError =
        response.params.error_description ??
        response.params.error ??
        'No authorization code was returned.';
      setError(`Sign-in redirect received, but token exchange cannot start: ${providerError}`);
      setBusy(false);
      return;
    }

    setBusy(true);
    setError('Authorization received. Exchanging code for a token…');
    void exchangeCode(code, request.codeVerifier, cfg.redirectUri)
      .then((tokens) => {
        if (!tokens.access_token) {
          throw new Error('Keycloak token response did not contain an access token.');
        }
        return finish(tokens.access_token, tokens.refresh_token);
      })
      .catch((e: Error) => setError(`Token exchange failed: ${e.message}`))
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

  /**
   * Open Keycloak registration endpoint in the system browser.
   * After register, user returns and taps Sign in (realm must allow registration).
   */
  const createAccount = async () => {
    setError(null);
    setBusy(true);
    try {
      const authority = getKeycloakAuthority().replace(/\/$/, '');
      const url =
        `${authority}/protocol/openid-connect/registrations` +
        `?client_id=${encodeURIComponent(cfg.clientId)}` +
        `&response_type=code` +
        `&scope=${encodeURIComponent(cfg.scopes.join(' '))}` +
        `&redirect_uri=${encodeURIComponent(cfg.redirectUri)}`;
      // Registration returns to the same custom-scheme URI as sign-in. Use an
      // auth session so Android captures that redirect here instead of handing
      // familyos://auth to Expo Router as a screen navigation (which has no route).
      // The registration authorization code is intentionally not exchanged;
      // the user signs in afterward through useAuthRequest, which owns its PKCE verifier.
      const result = await WebBrowser.openAuthSessionAsync(url, cfg.redirectUri);
      if (result.type === 'success') {
        setError('Account registration completed. Return here and tap Sign in.');
      } else if (result.type === 'cancel' || result.type === 'dismiss') {
        setError('Registration was closed before completion. You can try again or sign in.');
      }
    } catch (e: unknown) {
      setError((e as Error)?.message ?? 'Could not open registration');
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
        <Text style={styles.sub}>Sign in or create a family account</Text>
      </View>

      <Card style={styles.card}>
        <Text style={typography.caption}>
          Authority: {getKeycloakAuthority().replace(/^https?:\/\//, '')}
        </Text>
        <Text style={typography.caption}>
          Redirect URI: {cfg.redirectUri}
        </Text>
        {error ? <Text style={styles.error}>{error}</Text> : null}
        {busy ? (
          <ActivityIndicator color={colors.primary} style={{ marginVertical: spacing.md }} />
        ) : (
          <>
            <Button title="Sign in" onPress={() => void signIn()} disabled={!request} />
            <Button
              title="Create account"
              variant="secondary"
              onPress={() => void createAccount()}
            />
          </>
        )}
        <Text style={[typography.caption, { marginTop: spacing.md }]}>
          New here? Create account, then Sign in and set up a household or join with an invite code.
          Keycloak may prompt for fingerprint / Face ID if WebAuthn is enrolled.
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

export default withObserveScreen(LoginScreen);