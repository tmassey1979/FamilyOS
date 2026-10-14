import { api } from './client';

export type ChatMessageDto = {
  id: string;
  channelId: string;
  senderMemberId: string;
  senderName: string;
  body: string;
  sentAtUtc: string;
};

export type ChatChannelDto = {
  id: string;
  name: string;
  kind: string;
};

export const chatApi = {
  channel: () => api<ChatChannelDto>('/api/chat/channel'),
  messages: (channelId?: string, take = 50, beforeUtc?: string) => {
    const q = new URLSearchParams();
    if (channelId) q.set('channelId', channelId);
    q.set('take', String(take));
    if (beforeUtc) q.set('beforeUtc', beforeUtc);
    return api<ChatMessageDto[]>(`/api/chat/messages?${q.toString()}`);
  },
  send: (body: string, channelId?: string) =>
    api<ChatMessageDto>('/api/chat/messages', {
      method: 'POST',
      body: JSON.stringify({ body, channelId: channelId ?? null }),
    }),
};
