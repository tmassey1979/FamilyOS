import { useCallback, useEffect, useRef, useState } from 'react';
import {
  ActivityIndicator,
  FlatList,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { meApi } from '../../src/api/client';
import { chatApi, type ChatMessageDto } from '../../src/api/chatApi';
import { isChatHubConnected, startChatHub, stopChatHub } from '../../src/api/chatHub';
import { colors, spacing, typography } from '../../src/theme';

/** Family chat — SignalR when JWT available; REST poll fallback. */
export default function ChatScreen() {
  const [messages, setMessages] = useState<ChatMessageDto[]>([]);
  const [text, setText] = useState('');
  const [memberId, setMemberId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [sending, setSending] = useState(false);
  const [live, setLive] = useState(false);
  const seen = useRef<Set<string>>(new Set());
  const liveRef = useRef(false);

  const mergeMessage = useCallback((msg: ChatMessageDto) => {
    if (seen.current.has(msg.id)) return;
    seen.current.add(msg.id);
    setMessages((prev) => {
      if (prev.some((m) => m.id === msg.id)) return prev;
      return [...prev, msg];
    });
  }, []);

  const load = useCallback(async () => {
    try {
      setError(null);
      const [msgs, me] = await Promise.all([
        chatApi.messages(undefined, 80),
        meApi.get().catch(() => null),
      ]);
      seen.current = new Set(msgs.map((m) => m.id));
      setMessages(msgs);
      setMemberId(me?.memberId ?? null);
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Unable to load chat');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      const ok = await startChatHub((msg) => {
        if (!cancelled) mergeMessage(msg);
      });
      const connected = ok && isChatHubConnected();
      if (!cancelled) {
        liveRef.current = connected;
        setLive(connected);
      }
    })();

    const id = setInterval(() => {
      void load();
      const c = isChatHubConnected();
      if (c !== liveRef.current) {
        liveRef.current = c;
        setLive(c);
      }
    }, 8000);

    return () => {
      cancelled = true;
      clearInterval(id);
      void stopChatHub();
    };
  }, [load, mergeMessage]);

  const send = async () => {
    const body = text.trim();
    if (!body || sending) return;
    setSending(true);
    try {
      const msg = await chatApi.send(body);
      mergeMessage(msg);
      setText('');
      setError(null);
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Send failed');
    } finally {
      setSending(false);
    }
  };

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      keyboardVerticalOffset={88}
    >
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <View style={styles.statusRow}>
        <Text style={styles.status}>{live ? '● Live' : '○ Polling'}</Text>
      </View>
      {loading && messages.length === 0 ? (
        <ActivityIndicator color={colors.primary} style={{ marginTop: spacing.lg }} />
      ) : (
        <FlatList
          data={[...messages].reverse()}
          inverted
          keyExtractor={(m) => m.id}
          contentContainerStyle={styles.list}
          keyboardShouldPersistTaps="handled"
          renderItem={({ item }) => {
            const mine = memberId != null && item.senderMemberId === memberId;
            return (
              <View style={[styles.bubble, mine ? styles.mine : styles.theirs]}>
                {!mine ? <Text style={styles.sender}>{item.senderName}</Text> : null}
                <Text style={[styles.body, mine && styles.bodyMine]}>{item.body}</Text>
                <Text style={[styles.time, mine && { color: 'rgba(255,255,255,0.75)' }]}>
                  {new Date(item.sentAtUtc).toLocaleTimeString()}
                </Text>
              </View>
            );
          }}
          ListEmptyComponent={
            <Text style={styles.empty}>No messages yet. Say hello to the family.</Text>
          }
        />
      )}
      <View style={styles.composer}>
        <TextInput
          style={styles.input}
          value={text}
          onChangeText={setText}
          placeholder="Message the family…"
          placeholderTextColor={colors.textMuted}
          multiline
          maxLength={4000}
        />
        <Pressable
          onPress={() => void send()}
          style={[styles.sendBtn, (!text.trim() || sending) && { opacity: 0.5 }]}
          disabled={!text.trim() || sending}
        >
          <Text style={styles.sendLabel}>{sending ? '…' : 'Send'}</Text>
        </Pressable>
      </View>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  statusRow: { paddingHorizontal: spacing.md, paddingTop: spacing.xs },
  status: { ...typography.caption, opacity: 0.6, fontSize: 11 },
  list: { padding: spacing.md, paddingBottom: spacing.lg, gap: spacing.sm },
  bubble: {
    maxWidth: '82%',
    padding: spacing.sm,
    borderRadius: 12,
    marginBottom: spacing.xs,
  },
  mine: { alignSelf: 'flex-end', backgroundColor: colors.primary },
  theirs: {
    alignSelf: 'flex-start',
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
  },
  sender: { ...typography.caption, marginBottom: 2, fontWeight: '600' },
  body: { ...typography.body, color: colors.text },
  bodyMine: { color: '#fff' },
  time: { ...typography.caption, opacity: 0.7, marginTop: 4, fontSize: 10 },
  empty: { ...typography.caption, textAlign: 'center', marginTop: spacing.xl },
  error: { ...typography.caption, color: colors.danger, padding: spacing.sm },
  composer: {
    flexDirection: 'row',
    alignItems: 'flex-end',
    gap: spacing.sm,
    padding: spacing.sm,
    borderTopWidth: 1,
    borderTopColor: colors.border,
    backgroundColor: colors.surface,
  },
  input: {
    flex: 1,
    minHeight: 40,
    maxHeight: 120,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 10,
    paddingHorizontal: spacing.sm,
    paddingVertical: 8,
    color: colors.text,
    backgroundColor: colors.bg,
  },
  sendBtn: {
    backgroundColor: colors.primary,
    paddingHorizontal: spacing.md,
    paddingVertical: 10,
    borderRadius: 10,
  },
  sendLabel: { color: '#fff', fontWeight: '700' },
});
