import { useCallback, useEffect, useState } from 'react';
import { FlatList, StyleSheet, Text, View } from 'react-native';
import { Badge, Button, Card, EmptyState } from '../../src/components/ui';
import { tasksApi, type TaskDto } from '../../src/api/client';
import { colors, spacing, typography } from '../../src/theme';

export default function TasksScreen() {
  const [tasks, setTasks] = useState<TaskDto[]>([]);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const data = await tasksApi.mine();
      setTasks(data);
      setError(null);
    } catch {
      setError('Sign in to load tasks');
      setTasks([
        { id: '1', title: 'Take out trash', status: 'Assigned', priority: 'Normal', category: 'Chores' },
        { id: '2', title: 'Fold laundry', status: 'Accepted', priority: 'Normal', category: 'Chores' },
      ]);
    }
  }, []);

  useEffect(() => { void load(); }, [load]);

  return (
    <View style={styles.container}>
      {error ? <Text style={styles.hint}>{error} · showing demo data</Text> : null}
      <FlatList
        data={tasks}
        keyExtractor={(item) => item.id}
        contentContainerStyle={styles.list}
        ListEmptyComponent={<EmptyState title="No tasks" subtitle="You're all caught up." />}
        renderItem={({ item }) => (
          <Card style={styles.card}>
            <Text style={typography.heading}>{item.title}</Text>
            <View style={styles.row}>
              <Badge label={item.status} tone={item.status === 'Assigned' ? 'warning' : 'default'} />
              {item.category ? <Badge label={item.category} /> : null}
            </View>
            {item.status === 'Assigned' ? (
              <Button title="Accept" onPress={() => tasksApi.accept(item.id).then(load).catch(() => {})} />
            ) : null}
            {item.status === 'Accepted' ? (
              <Button title="Start" onPress={() => tasksApi.start(item.id).then(load).catch(() => {})} />
            ) : null}
            {item.status === 'InProgress' ? (
              <Button title="Complete" onPress={() => tasksApi.complete(item.id).then(load).catch(() => {})} />
            ) : null}
          </Card>
        )}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  list: { padding: spacing.md, gap: spacing.md },
  card: { marginBottom: spacing.md, gap: spacing.sm },
  row: { flexDirection: 'row', gap: spacing.sm },
  hint: { ...typography.caption, padding: spacing.md },
});
