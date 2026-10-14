/**
 * Family chat SignalR client — connects when a JWT is available.
 * Falls back to REST poll when using X-Dev-User only (no token).
 */
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { getAccessToken, getApiBaseUrl } from './client';
import type { ChatMessageDto } from './chatApi';

export type ChatMessageHandler = (msg: ChatMessageDto) => void;

let connection: HubConnection | null = null;
let handler: ChatMessageHandler | null = null;

function hubUrl(): string {
  const base = getApiBaseUrl().replace(/\/$/, '');
  return `${base}/hubs/chat`;
}

/** Start (or restart) the chat hub. No-ops when no JWT. */
export async function startChatHub(onMessage: ChatMessageHandler): Promise<boolean> {
  handler = onMessage;
  const token = getAccessToken();
  if (!token) {
    await stopChatHub();
    return false;
  }

  if (connection?.state === HubConnectionState.Connected) {
    return true;
  }

  await stopChatHub();

  connection = new HubConnectionBuilder()
    .withUrl(hubUrl(), {
      accessTokenFactory: () => getAccessToken() ?? '',
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(LogLevel.Warning)
    .build();

  connection.on('messageReceived', (payload: ChatMessageDto | Record<string, unknown>) => {
    if (!handler) return;
    const msg: ChatMessageDto = {
      id: String((payload as any).id ?? (payload as any).Id ?? ''),
      channelId: String((payload as any).channelId ?? (payload as any).ChannelId ?? ''),
      senderMemberId: String((payload as any).senderMemberId ?? (payload as any).SenderMemberId ?? ''),
      senderName: String((payload as any).senderName ?? (payload as any).SenderName ?? 'Member'),
      body: String((payload as any).body ?? (payload as any).Body ?? ''),
      sentAtUtc: String((payload as any).sentAtUtc ?? (payload as any).SentAtUtc ?? new Date().toISOString()),
    };
    if (msg.id) handler(msg);
  });

  try {
    await connection.start();
    try {
      await connection.invoke('JoinFamily');
    } catch {
      /* OnConnectedAsync already joins when FamilyId is loaded */
    }
    return true;
  } catch {
    await stopChatHub();
    return false;
  }
}

export async function stopChatHub(): Promise<void> {
  if (connection) {
    try {
      await connection.stop();
    } catch {
      /* ignore */
    }
    connection = null;
  }
}

export function isChatHubConnected(): boolean {
  return connection?.state === HubConnectionState.Connected;
}
