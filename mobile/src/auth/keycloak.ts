/**
 * Keycloak Authorization Code + PKCE for Family OS mobile.
 * Tokens stored in SecureStore; optional device biometric gate.
 */
import * as AuthSession from 'expo-auth-session';
import * as WebBrowser from 'expo-web-browser';
import * as SecureStore from 'expo-secure-store';
import * as LocalAuthentication from 'expo-local-authentication';
import { setAccessToken, setDevUser } from '../api/client';

WebBrowser.maybeCompleteAuthSession();

const AUTHORITY =
  process.env.EXPO_PUBLIC_KEYCLOAK_AUTHORITY ??
  'https://auth.18.226.226.118.sslip.io/realms/familyos';

const CLIENT_ID = process.env.EXPO_PUBLIC_KEYCLOAK_CLIENT_ID ?? 'familyos-mobile';

const TOKEN_KEY = 'familyos.access_token';
const REFRESH_KEY = 'familyos.refresh_token';
const BIOMETRIC_KEY = 'familyos.biometric_enabled';

const discovery: AuthSession.DiscoveryDocument = {
  authorizationEndpoint: `${AUTHORITY}/protocol/openid-connect/auth`,
  tokenEndpoint: `${AUTHORITY}/protocol/openid-connect/token`,
  endSessionEndpoint: `${AUTHORITY}/protocol/openid-connect/logout`,
  userInfoEndpoint: `${AUTHORITY}/protocol/openid-connect/userinfo`,
};

export function getKeycloakAuthority() {
  return AUTHORITY;
}

export function useAuthRequestConfig() {
  const redirectUri = AuthSession.makeRedirectUri({
    scheme: 'familyos',
    path: 'auth',
  });

  return {
    clientId: CLIENT_ID,
    redirectUri,
    scopes: ['openid', 'profile', 'email', 'offline_access'],
    usePKCE: true,
  };
}

export { discovery };

export async function persistTokens(access: string, refresh?: string | null) {
  await SecureStore.setItemAsync(TOKEN_KEY, access);
  if (refresh) await SecureStore.setItemAsync(REFRESH_KEY, refresh);
  setAccessToken(access);
  setDevUser(null);
}

export async function clearTokens() {
  await SecureStore.deleteItemAsync(TOKEN_KEY);
  await SecureStore.deleteItemAsync(REFRESH_KEY);
  setAccessToken(null);
}

export async function loadStoredAccessToken(): Promise<string | null> {
  return SecureStore.getItemAsync(TOKEN_KEY);
}

export async function isBiometricUnlockEnabled(): Promise<boolean> {
  const v = await SecureStore.getItemAsync(BIOMETRIC_KEY);
  return v === '1';
}

export async function setBiometricUnlockEnabled(on: boolean) {
  await SecureStore.setItemAsync(BIOMETRIC_KEY, on ? '1' : '0');
}

/** Device fingerprint / Face ID as local second factor before using stored JWT. */
export async function unlockWithBiometrics(): Promise<boolean> {
  const has = await LocalAuthentication.hasHardwareAsync();
  if (!has) return true;
  const enrolled = await LocalAuthentication.isEnrolledAsync();
  if (!enrolled) return true;
  const result = await LocalAuthentication.authenticateAsync({
    promptMessage: 'Unlock Family OS',
    cancelLabel: 'Cancel',
    disableDeviceFallback: false,
    biometricsSecurityLevel: 'strong',
  });
  return result.success;
}

export async function exchangeCode(
  code: string,
  codeVerifier: string,
  redirectUri: string,
): Promise<{ access_token: string; refresh_token?: string }> {
  const body = new URLSearchParams({
    grant_type: 'authorization_code',
    client_id: CLIENT_ID,
    code,
    redirect_uri: redirectUri,
    code_verifier: codeVerifier,
  });
  const res = await fetch(discovery.tokenEndpoint!, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: body.toString(),
  });
  if (!res.ok) {
    const t = await res.text();
    throw new Error(`Token exchange failed: ${res.status} ${t}`);
  }
  return res.json();
}

export async function refreshAccessToken(): Promise<string | null> {
  const refresh = await SecureStore.getItemAsync(REFRESH_KEY);
  if (!refresh) return null;
  const body = new URLSearchParams({
    grant_type: 'refresh_token',
    client_id: CLIENT_ID,
    refresh_token: refresh,
  });
  const res = await fetch(discovery.tokenEndpoint!, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: body.toString(),
  });
  if (!res.ok) {
    await clearTokens();
    return null;
  }
  const data = await res.json();
  await persistTokens(data.access_token, data.refresh_token ?? refresh);
  return data.access_token as string;
}
