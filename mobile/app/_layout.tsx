import { useEffect, useState } from 'react';
import { ActivityIndicator, View } from 'react-native';
import { Stack, useRouter, useSegments } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { colors } from '../src/theme';
import {
  clearTokens,
  isBiometricUnlockEnabled,
  loadStoredAccessToken,
  refreshAccessToken,
  unlockWithBiometrics,
} from '../src/auth/keycloak';
import { setAccessToken, getDevUser } from '../src/api/client';
import { OfflineBanner } from '../src/components/OfflineBanner';
import { ensureStarted } from '../src/offline';

/** Only skip login automatically when explicitly requested via env. */
const AUTO_SKIP_LOGIN =
  process.env.EXPO_PUBLIC_SKIP_LOGIN === '1' ||
  process.env.EXPO_PUBLIC_SKIP_LOGIN === 'true';

export default function RootLayout() {
  const [ready, setReady] = useState(false);
  const [signedIn, setSignedIn] = useState(false);
  const segments = useSegments();
  const router = useRouter();

  useEffect(() => {
    void ensureStarted();
  }, []);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        // Explicit opt-in only: EXPO_PUBLIC_SKIP_LOGIN=1 + EXPO_PUBLIC_DEV_USER
        if (AUTO_SKIP_LOGIN && getDevUser()) {
          if (!cancelled) {
            setSignedIn(true);
            setReady(true);
          }
          return;
        }

        let token = await loadStoredAccessToken();
        if (token) {
          if (await isBiometricUnlockEnabled()) {
            const ok = await unlockWithBiometrics();
            if (!ok) {
              await clearTokens();
              token = null;
            }
          }
        }
        if (!token) {
          token = await refreshAccessToken();
        }
        if (token) {
          setAccessToken(token);
          if (!cancelled) setSignedIn(true);
        } else if (!cancelled) {
          setSignedIn(!!getDevUser());
        }
      } catch {
        if (!cancelled) setSignedIn(false);
      } finally {
        if (!cancelled) setReady(true);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!ready) return;
    const onLogin = segments[0] === 'login';
    const hasSession = signedIn || !!getDevUser();
    if (!hasSession && !onLogin) {
      router.replace('/login');
    } else if (hasSession && onLogin) {
      router.replace('/(tabs)');
    }
  }, [ready, signedIn, segments, router]);

  if (!ready) {
    return (
      <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: colors.bg }}>
        <ActivityIndicator color={colors.primary} />
      </View>
    );
  }

  return (
    <>
      <StatusBar style="light" />
      <OfflineBanner />
      <Stack
        screenOptions={{
          headerStyle: { backgroundColor: colors.bg },
          headerTintColor: colors.text,
          contentStyle: { backgroundColor: colors.bg },
        }}
      >
        <Stack.Screen name="login" options={{ headerShown: false }} />
        <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
      </Stack>
    </>
  );
}
