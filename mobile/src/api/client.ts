/**
 * Typed API client for Family OS.
 * - Production: JWT via setAccessToken (Keycloak PKCE)
 * - Optional dev: setDevUser('terry.owner') or EXPO_PUBLIC_DEV_USER → X-Dev-User
 */

const DEFAULT_BASE =
  process.env.EXPO_PUBLIC_API_URL ??
  (typeof __DEV__ !== 'undefined' && __DEV__
    ? 'http://localhost:5080'
    : 'https://api.18.226.226.118.sslip.io');

export type ApiError = { error: string; status: number };

let accessToken: string | null = null;
let devUser: string | null = process.env.EXPO_PUBLIC_DEV_USER ?? null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function getAccessToken() {
  return accessToken;
}

export function setDevUser(externalId: string | null) {
  devUser = externalId;
}

export function getDevUser() {
  return devUser;
}

export async function api<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  const headers: Record<string, string> = {
    Accept: 'application/json',
    ...(options.body ? { 'Content-Type': 'application/json' } : {}),
    ...(options.headers as Record<string, string> | undefined),
  };
  if (accessToken) {
    headers.Authorization = `Bearer ${accessToken}`;
  } else if (devUser) {
    headers['X-Dev-User'] = devUser;
  }

  const res = await fetch(`${DEFAULT_BASE}${path}`, {
    ...options,
    headers,
  });

  if (!res.ok) {
    let message = res.statusText;
    try {
      const body = await res.json();
      message = body.detail ?? body.error ?? body.title ?? message;
    } catch {
      /* ignore */
    }
    const err: ApiError = { error: message, status: res.status };
    throw err;
  }

  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export type MeDto = {
  externalIdentityId?: string;
  userId?: string;
  familyId?: string;
  memberId?: string;
  role?: string;
};

export type QuietHoursDto = {
  enabled: boolean;
  startMinutes?: number | null;
  endMinutes?: number | null;
  startLabel?: string | null;
  endLabel?: string | null;
};

export type DeviceRegistrationDto = {
  id: string;
  platform: string;
  deviceName?: string | null;
  lastSeenAtUtc: string;
  isActive: boolean;
};

/** Per-member Google Calendar OAuth status (#32). */
export type GoogleCalendarStatusDto = {
  connected: boolean;
  email?: string | null;
  calendarId?: string | null;
  lastSyncAtUtc?: string | null;
  configured: boolean;
};

export const meApi = {
  get: () => api<MeDto>('/api/me'),
  getQuietHours: () => api<QuietHoursDto>('/api/me/quiet-hours'),
  setQuietHours: (body: {
    enabled: boolean;
    startMinutes?: number | null;
    endMinutes?: number | null;
  }) =>
    api<QuietHoursDto>('/api/me/quiet-hours', {
      method: 'PUT',
      body: JSON.stringify({
        enabled: body.enabled,
        startMinutes: body.startMinutes ?? null,
        endMinutes: body.endMinutes ?? null,
      }),
    }),
  registerDevice: (body: { token: string; platform: string; deviceName?: string | null }) =>
    api<DeviceRegistrationDto>('/api/me/devices', {
      method: 'POST',
      body: JSON.stringify({
        token: body.token,
        platform: body.platform,
        deviceName: body.deviceName ?? null,
      }),
    }),
  myDevices: () => api<DeviceRegistrationDto[]>('/api/me/devices'),
  unregisterDevice: (token: string) =>
    api<void>(`/api/me/devices?token=${encodeURIComponent(token)}`, { method: 'DELETE' }),
  googleCalendarStatus: () => api<GoogleCalendarStatusDto>('/api/me/google-calendar'),
  googleCalendarAuthUrl: () => api<{ url: string }>('/api/me/google-calendar/auth-url'),
  disconnectGoogleCalendar: () =>
    api<void>('/api/me/google-calendar', { method: 'DELETE' }),
  syncGoogleCalendar: () =>
    api<{ changed: number }>('/api/me/google-calendar/sync', { method: 'POST' }),
};

// NOTE: full client restored in follow-up — temporary slim file to unblock PLACEHOLDER
export const pulseApi = { get: () => api<any>('/api/pulse') };
export const tasksApi = {} as any;
export const familyApi = {} as any;
export const calendarApi = {} as any;
export const conditionsApi = {} as any;
export const requestsApi = {} as any;
export const procurementApi = {} as any;
export const notificationsApi = {} as any;
