import { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator,
  FlatList,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { Badge, Button, Card, EmptyState, SectionTitle } from '../../src/components/ui';
import { requestsApi, type RequestDto, type RequestTypeDto } from '../../src/api/client';
import { colors, spacing, typography } from '../../src/theme';

type Tab = 'mine' | 'queue';

export default function RequestsScreen() {
  const [types, setTypes] = useState<RequestTypeDto[]>([]);
  const [mine, setMine] = useState<RequestDto[]>([]);
  const [queue, setQueue] = useState<RequestDto[]>([]);
  const [tab, setTab] = useState<Tab>('mine');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [creating, setCreating] = useState<string | null>(null);

  const load = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      const [t, m, q] = await Promise.all([
        requestsApi.types().catch(() => [] as RequestTypeDto[]),
        requestsApi.mine(),
        requestsApi.approvalQueue().catch(() => [] as RequestDto[]),
      ]);
      setTypes(t);
      setMine(m);
      setQueue(q);
    } catch (e: unknown) {
      const msg =
        e && typeof e === 'object' && 'error' in e
          ? String((e as { error: string }).error)
          : 'Unable to load requests';
      setError(msg);
      setMine([]);
      setQueue([]);
    } finally {
      setBusy(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const startRequest = async (type: RequestTypeDto) => {
    setCreating(type.code);
    try {
      const created = await requestsApi.create({
        type: type.code,
        title: type.name,
        answers: {},
      });
      await requestsApi.submit(created.id);
      await load();
      setTab('mine');
    } catch (e: unknown) {
      const msg =
        e && typeof e === 'object' && 'error' in e
          ? String((e as { error: string }).error)
          : 'Create failed';
      setError(msg);
    } finally {
      setCreating(null);
    }
  };

  const list = tab === 'mine' ? mine : queue;

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      <SectionTitle>What do you need?</SectionTitle>
      <Text style={[typography.caption, { marginBottom: spacing.md }]}>
        Types load from the API. Tap to create & submit a draft.
      </Text>
      <View style={styles.grid}>
        {(types.length
          ? types
          : [
              { id: '1', code: 'Ride', name: 'Ride', questions: [] },
              { id: '2', code: 'Money', name: 'Money', questions: [] },
              { id: '3', code: 'Purchase', name: 'Purchase', questions: [] },
              { id: '4', code: 'Grocery', name: 'Groceries', questions: [] },
            ]
        ).map((t) => (
          <Pressable key={t.id} onPress={() => void startRequest(t)} disabled={!!creating}>
            <Card style={styles.chip}>
              <Text style={typography.body}>
                {creating === t.code ? '…' : t.name}
              </Text>
            </Card>
          </Pressable>
        ))}
      </View>

      <View style={styles.tabs}>
        <Pressable onPress={() => setTab('mine')} style={[styles.tab, tab === 'mine' && styles.tabActive]}>
          <Text style={typography.body}>My requests ({mine.length})</Text>
        </Pressable>
        <Pressable onPress={() => setTab('queue')} style={[styles.tab, tab === 'queue' && styles.tabActive]}>
          <Text style={typography.body}>Approval queue ({queue.length})</Text>
        </Pressable>
      </View>

      {error ? <Text style={styles.hint}>{error}</Text> : null}
      {busy ? <ActivityIndicator color={colors.primary} /> : null}

      {!busy && list.length === 0 ? (
        <EmptyState
          title={tab === 'mine' ? 'No open requests' : 'Queue is clear'}
          subtitle={tab === 'mine' ? 'Create one from a type above.' : 'Nothing needs your approval.'}
        />
      ) : null}

      <FlatList
        data={list}
        keyExtractor={(item) => item.id}
        scrollEnabled={false}
        renderItem={({ item }) => (
          <Card style={styles.card}>
            <Text style={typography.heading}>{item.title}</Text>
            <View style={styles.row}>
              <Badge label={item.status} tone={item.status === 'PendingApproval' ? 'warning' : 'default'} />
              <Badge label={item.type} />
            </View>
            {item.requesterName ? (
              <Text style={typography.caption}>From {item.requesterName}</Text>
            ) : null}
            {tab === 'queue' && item.status === 'PendingApproval' ? (
              <View style={styles.actions}>
                <Button
                  title="Approve"
                  onPress={() =>
                    requestsApi.approve(item.id).then(load).catch((e) => setError(String(e?.error ?? e)))
                  }
                />
                <Button
                  title="Deny"
                  variant="secondary"
                  onPress={() =>
                    requestsApi
                      .deny(item.id, 'Not right now')
                      .then(load)
                      .catch((e) => setError(String(e?.error ?? e)))
                  }
                />
              </View>
            ) : null}
          </Card>
        )}
      />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  content: { padding: spacing.md, paddingBottom: spacing.xl * 2 },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, marginBottom: spacing.lg },
  chip: { paddingVertical: spacing.sm, paddingHorizontal: spacing.md, minWidth: '45%' },
  tabs: { flexDirection: 'row', gap: spacing.sm, marginBottom: spacing.md },
  tab: { padding: spacing.sm, borderRadius: 8, backgroundColor: colors.bg },
  tabActive: { backgroundColor: colors.primary + '22' },
  card: { marginBottom: spacing.md, gap: spacing.sm },
  row: { flexDirection: 'row', gap: spacing.sm },
  actions: { flexDirection: 'row', gap: spacing.sm, marginTop: spacing.sm },
  hint: { ...typography.caption, marginBottom: spacing.md },
});
