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

export default function RootLayout() {
  const [ready, setReady] = useState(false);
  const [signedIn, setSignedIn] = useState(false);
  const segments = useSegments();
  const router = useRouter();

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        if (getDevUser()) {
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
          setSignedIn(false);
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
    if (!signedIn && !onLogin) {
      router.replace('/login');
    } else if (signedIn && onLogin) {
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
