/**
 * Typed API client for Family OS.
 * - Production: JWT via setAccessToken (Keycloak PKCE)
 * - Optional dev: setDevUser('terry.owner') or EXPO_PUBLIC_DEV_USER → X-Dev-User
 *   (does NOT auto-login; login screen is shown until the user signs in)
 */

const DEFAULT_BASE =
  process.env.EXPO_PUBLIC_API_URL ??
  (typeof __DEV__ !== 'undefined' && __DEV__
    ? 'http://localhost:5080'
    : 'https://api.18.226.226.118.sslip.io');

export type ApiError = { error: string; status: number };

let accessToken: string | null = null;
/**
 * Dev bypass identity. Only set when user taps a Dev user on login,
 * or EXPO_PUBLIC_DEV_USER is explicitly set (pair with EXPO_PUBLIC_SKIP_LOGIN=1 to auto-skip).
 * Never default to terry.owner — that skipped the login screen.
 */
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

export type PulseAttentionItem = {
  kind: string;
  entityId: string;
  title: string;
  reason: string;
  actionLabel: string;
};

export type PulseDto = {
  greeting: string;
  /** Current member role (#59) */
  role?: string;
  roleHint?: string;
  nextAction?: {
    kind: string;
    entityId: string;
    title: string;
    subtitle?: string;
    primaryAction: string;
    dueLabel?: string;
  };
  needsAttention: PulseAttentionItem[];
  /** Waiting / blockers: answers needed, pending conditions, overdue (#30) */
  waitingBlockers?: PulseAttentionItem[];
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
  calendarBacked?: boolean;
  createdAtUtc?: string;
  isOverdue?: boolean;
  plannedStartTime?: string;
  plannedEndTime?: string;
  varianceMinutes?: number;
};

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
};

export const pulseApi = {
  get: () => api<PulseDto>('/api/pulse'),
};

export type TaskHistoryDto = {
  id: string;
  status: string;
  actorMemberId: string;
  detail: string;
  timestampUtc: string;
};

export const tasksApi = {
  mine: (status?: string) =>
    api<TaskDto[]>(`/api/tasks/mine${status ? `?status=${status}` : ''}`),
  family: (status?: string) =>
    api<TaskDto[]>(`/api/tasks${status ? `?status=${status}` : ''}`),
  get: (id: string) => api<TaskDto>(`/api/tasks/${id}`),
  history: (id: string) => api<TaskHistoryDto[]>(`/api/tasks/${id}/history`),
  create: (body: {
    title: string;
    description?: string;
    assignToMemberId?: string;
    priority?: string;
    dueDate?: string;
    category?: string;
    calendarBacked?: boolean;
    estimatedDurationMinutes?: number;
  }) =>
    api<TaskDto>('/api/tasks', {
      method: 'POST',
      body: JSON.stringify({
        title: body.title,
        description: body.description ?? null,
        assignToMemberId: body.assignToMemberId ?? null,
        priority: body.priority ?? 'Normal',
        dueDate: body.dueDate ?? null,
        category: body.category ?? null,
        calendarBacked: body.calendarBacked ?? false,
        estimatedDurationMinutes: body.estimatedDurationMinutes ?? null,
      }),
    }),
  assign: (id: string, assignToMemberId: string) =>
    api<TaskDto>(`/api/tasks/${id}/assign`, {
      method: 'POST',
      body: JSON.stringify({ assignToMemberId }),
    }),
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
  defer: (id: string, note?: string) =>
    api<TaskDto>(`/api/tasks/${id}/defer`, {
      method: 'POST',
      body: JSON.stringify({ note: note ?? null }),
    }),
  cancel: (id: string, reason?: string) =>
    api<TaskDto>(`/api/tasks/${id}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ reason: reason ?? null }),
    }),
  reassign: (id: string, assignToMemberId: string) =>
    api<TaskDto>(`/api/tasks/${id}/reassign`, {
      method: 'POST',
      body: JSON.stringify({ assignToMemberId }),
    }),
  skip: (id: string, note?: string) =>
    api<TaskDto>(`/api/tasks/${id}/skip`, {
      method: 'POST',
      body: JSON.stringify({ note: note ?? null }),
    }),
  updateDetails: (
    id: string,
    body: {
      title?: string;
      description?: string;
      priority?: string;
      category?: string;
      dueDate?: string | null;
      clearDueDate?: boolean;
    },
  ) =>
    api<TaskDto>(`/api/tasks/${id}`, {
      method: 'PUT',
      body: JSON.stringify({
        title: body.title ?? null,
        description: body.description ?? null,
        priority: body.priority ?? null,
        category: body.category ?? null,
        dueDate: body.dueDate ?? null,
        clearDueDate: body.clearDueDate ?? false,
      }),
    }),
  setPlannedTimes: (
    id: string,
    body: {
      plannedStartTime?: string;
      plannedEndTime?: string;
      estimatedDurationMinutes?: number;
    },
  ) =>
    api<TaskDto>(`/api/tasks/${id}/planned-times`, {
      method: 'PUT',
      body: JSON.stringify({
        plannedStartTime: body.plannedStartTime ?? null,
        plannedEndTime: body.plannedEndTime ?? null,
        estimatedDurationMinutes: body.estimatedDurationMinutes ?? null,
      }),
    }),
};

export type DeclineReasonDto = {
  id: string;
  text: string;
  sortOrder: number;
  isEnabled: boolean;
};

export const familyApi = {
  get: () => api<{ id: string; name: string; timeZone?: string; currency?: string }>('/api/family'),
  members: () =>
    api<Array<{ id: string; userId: string; displayName: string; role: string; isActive: boolean }>>(
      '/api/family/members',
    ),
  declineReasons: () => api<DeclineReasonDto[]>('/api/family/decline-reasons'),
  addMember: (body: {
    email: string;
    displayName: string;
    role: string;
    externalIdentityId?: string;
    firstName?: string;
    lastName?: string;
  }) =>
    api<{ id: string; userId: string; displayName: string; role: string; isActive: boolean }>(
      '/api/family/members',
      { method: 'POST', body: JSON.stringify(body) },
    ),
  changeRole: (memberId: string, role: string) =>
    api<{ id: string; userId: string; displayName: string; role: string; isActive: boolean }>(
      `/api/family/members/${memberId}/change-role`,
      { method: 'POST', body: JSON.stringify({ role }) },
    ),
  deactivate: (memberId: string) =>
    api<{ id: string; userId: string; displayName: string; role: string; isActive: boolean }>(
      `/api/family/members/${memberId}/deactivate`,
      { method: 'POST' },
    ),
};

export type CalendarEventDto = {
  id: string;
  title: string;
  description?: string;
  startUtc: string;
  endUtc?: string;
  allDay: boolean;
  location?: string;
  linkedTaskId?: string;
  linkedRequestId?: string;
};

export const calendarApi = {
  upcoming: (days = 14) => api<CalendarEventDto[]>(`/api/calendar/upcoming?days=${days}`),
  create: (body: {
    title: string;
    startUtc: string;
    endUtc?: string;
    allDay?: boolean;
    location?: string;
    description?: string;
    linkedTaskId?: string;
    linkedRequestId?: string;
  }) =>
    api<CalendarEventDto>('/api/calendar', {
      method: 'POST',
      body: JSON.stringify({
        title: body.title,
        startUtc: body.startUtc,
        endUtc: body.endUtc ?? null,
        allDay: body.allDay ?? false,
        location: body.location ?? null,
        description: body.description ?? null,
        linkedTaskId: body.linkedTaskId ?? null,
        linkedRequestId: body.linkedRequestId ?? null,
      }),
    }),
  cancel: (id: string) => api<void>(`/api/calendar/${id}/cancel`, { method: 'POST' }),
};

export type ConditionDto = {
  id: string;
  requestId?: string | null;
  taskId?: string | null;
  name: string;
  description?: string | null;
  type: string;
  status: string;
  logicGroup: string;
  groupOrder: number;
  deadlineUtc?: string | null;
  satisfiedByMemberId?: string | null;
  satisfiedAtUtc?: string | null;
};

export const conditionsApi = {
  pending: () => api<ConditionDto[]>('/api/conditions/pending'),
  byRequest: (requestId: string) => api<ConditionDto[]>(`/api/conditions/by-request/${requestId}`),
  create: (body: {
    name: string;
    type: string;
    requestId?: string;
    taskId?: string;
    description?: string;
    deadlineUtc?: string;
  }) =>
    api<ConditionDto>('/api/conditions', {
      method: 'POST',
      body: JSON.stringify({
        name: body.name,
        type: body.type,
        requestId: body.requestId ?? null,
        taskId: body.taskId ?? null,
        description: body.description ?? null,
        deadlineUtc: body.deadlineUtc ?? null,
        logicGroup: 'All',
        groupOrder: 0,
      }),
    }),
  satisfy: (id: string) => api<ConditionDto>(`/api/conditions/${id}/satisfy`, { method: 'POST' }),
  fail: (id: string, reason?: string) =>
    api<ConditionDto>(`/api/conditions/${id}/fail`, {
      method: 'POST',
      body: JSON.stringify({ reason: reason ?? null }),
    }),
  cancel: (id: string) => api<ConditionDto>(`/api/conditions/${id}/cancel`, { method: 'POST' }),
};

export type PendingQuestionDto = {
  id: string;
  questionText: string;
  isAnswered: boolean;
  answer?: string | null;
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
  executionPlanId?: string | null;
  pendingQuestions?: PendingQuestionDto[];
};

export type RequestTypeDto = {
  id: string;
  code: string;
  name: string;
  description?: string;
  questions: Array<{ key: string; prompt: string; required?: boolean }>;
};

export type PolicyEvaluationDto = {
  isAllowed: boolean;
  requiresOwner?: boolean;
  autoApproved?: boolean;
  explanation?: string;
};

export type RequestHistoryDto = {
  id: string;
  status: string;
  actorMemberId: string;
  detail: string;
  timestampUtc: string;
};

export type ExecutionItemDto = {
  id: string;
  type: string;
  title: string;
  assigneeMemberId?: string | null;
  isSelected: boolean;
  resultingEntityId?: string | null;
};

export type ExecutionPlanDto = {
  id: string;
  requestId: string;
  status: string;
  items: ExecutionItemDto[];
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
  updateDraft: (
    id: string,
    body: {
      title?: string;
      answers?: Record<string, unknown>;
      amount?: number;
      neededBy?: string;
    },
  ) =>
    api<RequestDto>(`/api/requests/${id}`, {
      method: 'PUT',
      body: JSON.stringify({
        title: body.title ?? null,
        answers: body.answers ?? null,
        amount: body.amount ?? null,
        neededBy: body.neededBy ?? null,
      }),
    }),
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
  cancel: (id: string, reason?: string) =>
    api<RequestDto>(`/api/requests/${id}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ reason: reason ?? null }),
    }),
  askQuestion: (id: string, questionText: string) =>
    api<RequestDto>(`/api/requests/${id}/ask-question`, {
      method: 'POST',
      body: JSON.stringify({ questionText }),
    }),
  answer: (id: string, questionId: string, answer: string) =>
    api<RequestDto>(`/api/requests/${id}/answer`, {
      method: 'POST',
      body: JSON.stringify({ questionId, answer }),
    }),
  evaluatePolicy: (id: string) =>
    api<PolicyEvaluationDto>(`/api/requests/${id}/policy`),
  history: (id: string) =>
    api<RequestHistoryDto[]>(`/api/requests/${id}/history`),
  /** Owner-only: force approve/deny and record PolicyOverride audit. */
  overridePolicy: (id: string, body: { approve: boolean; reason?: string; conditional?: boolean }) =>
    api<RequestDto>(`/api/requests/${id}/override-policy`, {
      method: 'POST',
      body: JSON.stringify({
        approve: body.approve,
        reason: body.reason ?? null,
        conditional: body.conditional ?? false,
      }),
    }),
  generatePlan: (id: string) =>
    api<ExecutionPlanDto>(`/api/requests/${id}/execution-plan`, { method: 'POST' }),
  commitPlan: (planId: string, deselectedItemIds?: string[]) =>
    api<ExecutionPlanDto>(`/api/requests/execution-plans/${planId}/commit`, {
      method: 'POST',
      body: JSON.stringify({ deselectedItemIds: deselectedItemIds ?? null }),
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
  addToCart: (id: string, cartId: string) =>
    api<ProcurementItemDto>(`/api/procurement/items/${id}/add-to-cart`, {
      method: 'POST',
      body: JSON.stringify({ cartId }),
    }),
  hold: (id: string) => api<ProcurementItemDto>(`/api/procurement/items/${id}/hold`, { method: 'POST' }),
  defer: (id: string) => api<ProcurementItemDto>(`/api/procurement/items/${id}/defer`, { method: 'POST' }),
  noLongerNeeded: (id: string) =>
    api<ProcurementItemDto>(`/api/procurement/items/${id}/no-longer-needed`, { method: 'POST' }),
  removeFromCart: (id: string) =>
    api<ProcurementItemDto>(`/api/procurement/items/${id}/remove-from-cart`, { method: 'POST' }),
  abandonCart: (cartId: string) =>
    api<CartDto>(`/api/procurement/carts/${cartId}/abandon`, { method: 'POST' }),
  purchase: (id: string, actualPrice: number, store?: string) =>
    api<ProcurementItemDto>(`/api/procurement/items/${id}/purchase`, {
      method: 'POST',
      body: JSON.stringify({ actualPrice, store }),
    }),
};
