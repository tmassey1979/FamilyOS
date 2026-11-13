import { useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, Text, View } from 'react-native';
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
import { setupNotificationNavigation } from '../src/push/notificationRouting';
import { initPostHog, identifyUser, captureException } from '../src/analytics/posthog';
import { Observe, ObserveRoot } from 'expo-observe';
import { ReanimatedLogLevel } from 'react-native-reanimated';

// Configure integrations before any route mounts. Route values that may contain
// invite/auth identifiers are excluded from exported navigation telemetry.
Observe.configure({
  environment: __DEV__ ? 'development' : 'production',
  errorHandlingEnabled: true,
  networkTraces: false,
  integrations: {
    'expo-router': {
      filteredParams: ['code', 'inviteCode', 'token', 'accessToken', 'refreshToken', 'email', 'userId', 'familyId', 'session', 'state', 'redirect', 'redirect_uri'],
    },
    'react-native-reanimated': {
      level: ReanimatedLogLevel.error,
      strict: false,
      reportWarnings: false,
    },
  },
});

const AUTO_SKIP_LOGIN =
  process.env.EXPO_PUBLIC_SKIP_LOGIN === '1' ||
  process.env.EXPO_PUBLIC_SKIP_LOGIN === 'true';

function RootLayout() {
  const [ready, setReady] = useState(false);
  const [signedIn, setSignedIn] = useState(false);
  const [needsOnboarding, setNeedsOnboarding] = useState(false);
  const segments = useSegments();
  const router = useRouter();

  useEffect(() => {
    void ensureStarted();
    void initPostHog();
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
            if (!cancelled) {
              setNeedsOnboarding(!me.familyId);
              if (me.userId) identifyUser(String(me.userId), { role: me.role ?? undefined });
            }
          } catch (e) {
            captureException(e, { where: 'me_on_boot' });
            if (!cancelled) setNeedsOnboarding(true);
          }
        } else if (!cancelled) {
          const dev = !!getDevUser();
          setSignedIn(dev);
          setNeedsOnboarding(false);
        }
      } catch (e) {
        captureException(e, { where: 'auth_boot' });
        Observe.logEvent('auth.bootstrap.failed', { severity: 'error', attributes: { stage: 'auth_boot' } });
        if (!cancelled) setSignedIn(false);
      } finally {
        if (!cancelled) {
          Observe.logEvent('auth.bootstrap.finished');
          setReady(true);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

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

  useEffect(() => {
    if (!ready || (!signedIn && !getDevUser()) || needsOnboarding) return;
    return setupNotificationNavigation((href) => {
      try {
        router.push(href as never);
      } catch {
        /* navigation race during boot */
      }
    });
  }, [ready, signedIn, needsOnboarding, router]);

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


export default function ObservedRootLayout() {
  return (
    <ObserveRoot
      errorBoundaryFallback={({ resetError }) => (
        <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center', padding: 24, backgroundColor: colors.bg }}>
          <Text style={{ color: colors.text, fontSize: 18, fontWeight: '600', marginBottom: 12 }}>FamilyOS hit an unexpected error.</Text>
          <Text style={{ color: colors.textMuted, textAlign: 'center', marginBottom: 20 }}>An unexpected error stopped this screen. You can try loading it again.</Text>
          <Pressable onPress={resetError} style={{ paddingHorizontal: 20, paddingVertical: 12, borderRadius: 10, backgroundColor: colors.primary }}>
            <Text style={{ color: '#fff', fontWeight: '600' }}>Try again</Text>
          </Pressable>
        </View>
      )}
    >
      <RootLayout />
    </ObserveRoot>
  );
}