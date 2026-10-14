import { useCallback, useEffect, useState } from 'react';
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
import { colors, spacing, typography } from '../../src/theme';

/** Family chat — shared household channel (poll refresh; SignalR ready on API). */
export default function ChatScreen() {
  const [messages, setMessages] = useState<ChatMessageDto[]>([]);
  const [text, setText] = useState('');
  const [memberId, setMemberId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [sending, setSending] = useState(false);

  const load = useCallback(async () => {
    try {
      setError(null);
      const [msgs, me] = await Promise.all([
        chatApi.messages(undefined, 80),
        meApi.get().catch(() => null),
      ]);
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
    const id = setInterval(() => void load(), 8000);
    return () => clearInterval(id);
  }, [load]);

  const send = async () => {
    const body = text.trim();
    if (!body || sending) return;
    setSending(true);
    try {
      const msg = await chatApi.send(body);
      setMessages((prev) => [...prev.filter((m) => m.id !== msg.id), msg]);
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
      {loading && messages.length === 0 ? (
        <ActivityIndicator color={colors.primary} style={{ marginTop: spacing.lg }} />
      ) : (
        <FlatList
          data={messages}
          keyExtractor={(m) => m.id}
          contentContainerStyle={styles.list}
          renderItem={({ item }) => {
            const mine = memberId != null && item.senderMemberId === memberId;
            return (
              <View style={[styles.bubble, mine ? styles.mine : styles.theirs]}>
                {!mine ? <Text style={styles.sender}>{item.senderName}</Text> : null}
                <Text style={[styles.body, mine && styles.bodyMine]}>{item.body}</Text>
                <Text style={styles.time}>{new Date(item.sentAtUtc).toLocaleTimeString()}</Text>
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
