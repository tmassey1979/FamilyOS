import { api } from './client';

export type OutcomesDto = {
  tasksCompletedLast7Days: number;
  tasksCompletedLast30Days: number;
  requestsApprovedLast7Days: number;
  requestsDeniedLast7Days: number;
  requestsApprovedLast30Days?: number;
  requestsDeniedLast30Days?: number;
  shoppingItemsPurchasedLast7Days: number;
  avgTaskCompletionMinutes?: number | null;
  approvalRateLast7Days?: number | null;
  openTasks: number;
  pendingRequests: number;
  queuedShoppingItems: number;
};

export const outcomesApi = {
  get: () => api<OutcomesDto>('/api/outcomes'),
};
