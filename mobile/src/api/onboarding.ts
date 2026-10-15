import { api } from './client';

export type FamilyDto = {
  id: string;
  name: string;
  timeZone?: string;
  currency?: string;
};

export type InviteDto = {
  id: string;
  code: string;
  role: string;
  emailHint?: string | null;
  expiresAtUtc: string;
  isOpen: boolean;
  acceptedAtUtc?: string | null;
};

export type InvitePreviewDto = {
  code: string;
  familyName: string;
  role: string;
  expiresAtUtc: string;
  isOpen: boolean;
};

export const onboardingFamilyApi = {
  create: (body: {
    name: string;
    timeZone?: string;
    currency?: string;
    ownerDisplayName?: string;
    ownerEmail?: string;
    ownerFirstName?: string;
    ownerLastName?: string;
  }) =>
    api<FamilyDto>('/api/family', {
      method: 'POST',
      body: JSON.stringify(body),
    }),
};

export const invitesApi = {
  preview: (code: string) => api<InvitePreviewDto>(`/api/invites/${encodeURIComponent(code)}`),
  create: (body: { role: string; emailHint?: string; validDays?: number }) =>
    api<InviteDto>('/api/invites', { method: 'POST', body: JSON.stringify(body) }),
  list: () => api<InviteDto[]>('/api/invites'),
  accept: (code: string, displayName?: string) =>
    api<{ id: string; displayName: string; role: string }>(`/api/invites/${encodeURIComponent(code)}/accept`, {
      method: 'POST',
      body: JSON.stringify({ displayName: displayName ?? null }),
    }),
  revoke: (id: string) => api<void>(`/api/invites/${id}/revoke`, { method: 'POST' }),
};
