import { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { Badge, Button, Card, EmptyState, SectionTitle } from '../../src/components/ui';
import { tasksApi, type TaskDto } from '../../src/api/client';
import { colors, spacing, typography } from '../../src/theme';

export default function TasksScreen() {
  const [tasks, setTasks] = useState<TaskDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await tasksApi.mine();
      setTasks(list);
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not load tasks');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const run = async (id: string, fn: () => Promise<unknown>) => {
    setBusyId(id);
    setError(null);
    try {
      await fn();
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Action failed');
    } finally {
      setBusyId(null);
    }
  };

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={loading} onRefresh={() => void load()} />}
    >
      <SectionTitle>Tasks</SectionTitle>
      {error ? <Text style={styles.error}>{error}</Text> : null}
      {loading && tasks.length === 0 ? <ActivityIndicator color={colors.primary} /> : null}
      {!loading && tasks.length === 0 ? (
        <EmptyState title="No tasks" subtitle="Create a task from the + flow or wait for an assignment." />
      ) : null}
      {tasks.map((t) => (
        <Card key={t.id} style={styles.card}>
          <View style={styles.row}>
            <Text style={[typography.heading, { flex: 1 }]}>{t.title}</Text>
            {t.isOverdue ? <Badge label="Overdue" tone="danger" /> : null}
            {t.priority ? <Badge label={String(t.priority)} tone="warning" /> : null}
          </View>
          {t.description ? <Text style={typography.caption}>{t.description}</Text> : null}
          <Text style={typography.caption}>
            {t.status}
            {t.dueDate ? ` · due ${new Date(t.dueDate).toLocaleDateString()}` : ''}
          </Text>
          <View style={styles.row}>
            <Button
              title={busyId === t.id ? '…' : 'Accept'}
              variant="secondary"
              onPress={() => void run(t.id, () => tasksApi.accept(t.id))}
            />
            <Button
              title="Start"
              variant="secondary"
              onPress={() => void run(t.id, () => tasksApi.start(t.id))}
            />
            <Button title="Done" onPress={() => void run(t.id, () => tasksApi.complete(t.id))} />
          </View>
        </Card>
      ))}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  content: { padding: spacing.md, paddingBottom: spacing.xl * 2, gap: spacing.sm },
  card: { gap: spacing.xs, marginBottom: spacing.sm },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, alignItems: 'center' },
  error: { ...typography.caption, color: colors.danger },
});
