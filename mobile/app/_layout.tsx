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
import { setAccessToken, getDevUser, meApi } from '../src/api/client';
import { OfflineBanner } from '../src/components/OfflineBanner';
import { ensureStarted } from '../src/offline';

const AUTO_SKIP_LOGIN =
  process.env.EXPO_PUBLIC_SKIP_LOGIN === '1' ||
  process.env.EXPO_PUBLIC_SKIP_LOGIN === 'true';

export default function RootLayout() {
  const [ready, setReady] = useState(false);
  const [signedIn, setSignedIn] = useState(false);
  const [needsOnboarding, setNeedsOnboarding] = useState(false);
  const segments = useSegments();
  const router = useRouter();

  useEffect(() => {
    void ensureStarted();
  }, []);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        if (AUTO_SKIP_LOGIN && getDevUser()) {
          if (!cancelled) {
            setSignedIn(true);
            setNeedsOnboarding(false);
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
          try {
            const me = await meApi.get();
            if (!cancelled) setNeedsOnboarding(!me.familyId);
          } catch {
            if (!cancelled) setNeedsOnboarding(true);
          }
        } else if (!cancelled) {
          const dev = !!getDevUser();
          setSignedIn(dev);
          setNeedsOnboarding(false);
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

  // Re-check membership after create/join so we don't bounce back to onboarding
  useEffect(() => {
    if (!ready || !signedIn || !!getDevUser()) return;
    let cancelled = false;
    void meApi
      .get()
      .then((me) => {
        if (!cancelled) setNeedsOnboarding(!me.familyId);
      })
      .catch(() => {
        /* keep prior */
      });
    return () => {
      cancelled = true;
    };
  }, [segments, ready, signedIn]);

  useEffect(() => {
    if (!ready) return;
    const onLogin = segments[0] === 'login';
    const onOnboarding = segments[0] === 'onboarding';
    const hasSession = signedIn || !!getDevUser();

    if (!hasSession && !onLogin) {
      router.replace('/login');
      return;
    }
    if (hasSession && onLogin) {
      router.replace(needsOnboarding ? '/onboarding' : '/(tabs)');
      return;
    }
    if (hasSession && needsOnboarding && !onOnboarding && !onLogin) {
      router.replace('/onboarding');
      return;
    }
    if (hasSession && !needsOnboarding && onOnboarding) {
      router.replace('/(tabs)');
    }
  }, [ready, signedIn, needsOnboarding, segments, router]);

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
        <Stack.Screen name="onboarding" options={{ headerShown: false, title: 'Welcome' }} />
        <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
      </Stack>
    </>
  );
}
