/**
 * Typed API client for Family OS.
 * - Production: JWT via setAccessToken (Keycloak PKCE)
 * - Development: setDevUser('terry.owner') → X-Dev-User header (API DevBypass)
 */

const DEFAULT_BASE = process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5080';

export type ApiError = { error: string; status: number };

let accessToken: string | null = null;
/** Dev-only: seed external id, e.g. terry.owner | michelle.adult | mia.teen | eli.child */
let devUser: string | null =
  process.env.EXPO_PUBLIC_DEV_USER ?? (typeof __DEV__ !== 'undefined' && __DEV__ ? 'terry.owner' : null);

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

export type PulseDto = {
  greeting: string;
  nextAction?: {
    kind: string;
    entityId: string;
    title: string;
    subtitle?: string;
    primaryAction: string;
    dueLabel?: string;
  };
  needsAttention: Array<{
    kind: string;
    entityId: string;
    title: string;
    reason: string;
    actionLabel: string;
  }>;
  comingUp: Array<{
    kind: string;
    entityId: string;
    title: string;
    whenUtc: string;
    location?: string;
  }>;
  household: {
    requestsAwaitingApproval: number;
    tasksDueToday: number;
    tasksNeedingAcceptance: number;
    nextShoppingTrip?: string;
  };
  procurement?: {
    approvedItems: number;
    readyForStore: number;
    primaryStore?: string;
  };
  conditions?: Array<{
    id: string;
    name: string;
    status: string;
    relatedTitle?: string;
  }>;
};

export type TaskDto = {
  id: string;
  title: string;
  description?: string;
  status: string;
  priority: string;
  assignedToMemberId?: string;
  assignedToName?: string;
  dueDate?: string;
  estimatedDurationMinutes?: number;
  actualDurationMinutes?: number;
  category?: string;
};

export type MeDto = {
  externalIdentityId?: string;
  userId?: string;
  familyId?: string;
  memberId?: string;
  role?: string;
};

export const meApi = {
  get: () => api<MeDto>('/api/me'),
};

export const pulseApi = {
  get: () => api<PulseDto>('/api/pulse'),
};

export const tasksApi = {
  mine: (status?: string) =>
    api<TaskDto[]>(`/api/tasks/mine${status ? `?status=${status}` : ''}`),
  family: () => api<TaskDto[]>('/api/tasks'),
  get: (id: string) => api<TaskDto>(`/api/tasks/${id}`),
  accept: (id: string) => api<TaskDto>(`/api/tasks/${id}/accept`, { method: 'POST' }),
  start: (id: string) => api<TaskDto>(`/api/tasks/${id}/start`, { method: 'POST' }),
  pause: (id: string) => api<TaskDto>(`/api/tasks/${id}/pause`, { method: 'POST' }),
  resume: (id: string) => api<TaskDto>(`/api/tasks/${id}/resume`, { method: 'POST' }),
  complete: (id: string) => api<TaskDto>(`/api/tasks/${id}/complete`, { method: 'POST' }),
  decline: (id: string, body?: { reasonId?: string; note?: string }) =>
    api<TaskDto>(`/api/tasks/${id}/decline`, {
      method: 'POST',
      body: JSON.stringify(body ?? {}),
    }),
};

export const familyApi = {
  get: () => api<{ id: string; name: string; timeZone?: string; currency?: string }>('/api/family'),
  members: () =>
    api<Array<{ id: string; userId: string; displayName: string; role: string; isActive: boolean }>>(
      '/api/family/members',
    ),
};

export type RequestDto = {
  id: string;
  type: string;
  status: string;
  title: string;
  summary?: string;
  amount?: number;
  neededByUtc?: string;
  requesterMemberId: string;
  requesterName: string;
  currentApproverMemberId?: string;
  denialReason?: string;
  createdAtUtc: string;
  answers?: Record<string, unknown>;
};

export type RequestTypeDto = {
  id: string;
  code: string;
  name: string;
  description?: string;
  questions: Array<{ key: string; prompt: string; required?: boolean }>;
};

export const requestsApi = {
  types: () => api<RequestTypeDto[]>('/api/requests/types'),
  mine: () => api<RequestDto[]>('/api/requests/mine'),
  approvalQueue: () => api<RequestDto[]>('/api/requests/approval-queue'),
  get: (id: string) => api<RequestDto>(`/api/requests/${id}`),
  create: (body: {
    type: string;
    title: string;
    answers?: Record<string, unknown>;
    amount?: number;
    neededBy?: string;
  }) => api<RequestDto>('/api/requests', { method: 'POST', body: JSON.stringify(body) }),
  submit: (id: string) => api<RequestDto>(`/api/requests/${id}/submit`, { method: 'POST' }),
  approve: (id: string, conditional = false) =>
    api<RequestDto>(`/api/requests/${id}/approve`, {
      method: 'POST',
      body: JSON.stringify({ conditional }),
    }),
  deny: (id: string, reason: string) =>
    api<RequestDto>(`/api/requests/${id}/deny`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),
};

export type ProcurementItemDto = {
  id: string;
  name: string;
  brand?: string;
  size?: string;
  category?: string;
  quantity?: number;
  estimatedPrice?: number;
  actualPrice?: number;
  preferredStore?: string;
  status: string;
  createdAtUtc: string;
};

export type CartDto = {
  id: string;
  storeName: string;
  items: ProcurementItemDto[];
  isActive: boolean;
};

export const procurementApi = {
  queue: () => api<ProcurementItemDto[]>('/api/procurement/queue'),
  carts: () => api<CartDto[]>('/api/procurement/carts'),
  createCart: (storeName: string) =>
    api<CartDto>('/api/procurement/carts', {
      method: 'POST',
      body: JSON.stringify({ storeName }),
    }),
  hold: (id: string) => api<ProcurementItemDto>(`/api/procurement/items/${id}/hold`, { method: 'POST' }),
  purchase: (id: string, actualPrice: number, store?: string) =>
    api<ProcurementItemDto>(`/api/procurement/items/${id}/purchase`, {
      method: 'POST',
      body: JSON.stringify({ actualPrice, store }),
    }),
};
