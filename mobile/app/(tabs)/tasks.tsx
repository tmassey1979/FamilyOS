import { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator, FlatList, Modal, Pressable, StyleSheet, Text, TextInput, View,
} from 'react-native';
import { Badge, Button, Card, EmptyState } from '../../src/components/ui';
import {
  familyApi, tasksApi,
  type DeclineReasonDto, type TaskDto, type TaskHistoryDto,
} from '../../src/api/client';
import { colors, spacing, typography } from '../../src/theme';

type Member = { id: string; displayName: string; role: string };

function statusTone(status: string): 'default' | 'warning' | 'success' | 'danger' {
  if (status === 'Assigned' || status === 'Reassigned') return 'warning';
  if (status === 'Accepted' || status === 'InProgress') return 'success';
  if (status === 'Declined' || status === 'Cancelled') return 'danger';
  return 'default';
}

export default function TasksScreen() {
  const [tasks, setTasks] = useState<TaskDto[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [reasons, setReasons] = useState<DeclineReasonDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [scope, setScope] = useState<'mine' | 'family'>('mine');
  const [createOpen, setCreateOpen] = useState(false);
  const [title, setTitle] = useState('');
  const [category, setCategory] = useState('Chores');
  const [assignTo, setAssignTo] = useState<string | undefined>();
  const [creating, setCreating] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [history, setHistory] = useState<TaskHistoryDto[]>([]);
  const [historyTitle, setHistoryTitle] = useState('');

  // Decline modal
  const [declineTask, setDeclineTask] = useState<TaskDto | null>(null);
  const [declineReasonId, setDeclineReasonId] = useState<string | undefined>();
  const [declineNote, setDeclineNote] = useState('');
  const [declining, setDeclining] = useState(false);

  // Reassign modal
  const [reassignTask, setReassignTask] = useState<TaskDto | null>(null);
  const [reassignTo, setReassignTo] = useState<string | undefined>();
  const [reassigning, setReassigning] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [data, mems, rs] = await Promise.all([
        scope === 'mine' ? tasksApi.mine() : tasksApi.family(),
        familyApi.members().catch(() => [] as Member[]),
        familyApi.declineReasons().catch(() => [] as DeclineReasonDto[]),
      ]);
      setTasks(data);
      setMembers(mems);
      setReasons(rs.filter((r) => r.isEnabled !== false));
      setError(null);
    } catch {
      setError('Could not load tasks — check API / DevBypass');
      setTasks([]);
    } finally {
      setLoading(false);
    }
  }, [scope]);

  useEffect(() => { void load(); }, [load]);

  const run = async (id: string, action: () => Promise<unknown>) => {
    setBusyId(id);
    try {
      await action();
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Action failed');
    } finally {
      setBusyId(null);
    }
  };

  const openHistory = async (task: TaskDto) => {
    setHistoryTitle(task.title);
    setHistoryOpen(true);
    try { setHistory(await tasksApi.history(task.id)); }
    catch { setHistory([]); }
  };

  const createTask = async () => {
    if (!title.trim()) return;
    setCreating(true);
    try {
      await tasksApi.create({
        title: title.trim(),
        category: category.trim() || undefined,
        assignToMemberId: assignTo,
        priority: 'Normal',
      });
      setTitle('');
      setCreateOpen(false);
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Create failed');
    } finally {
      setCreating(false);
    }
  };

  const submitDecline = async () => {
    if (!declineTask) return;
    setDeclining(true);
    try {
      await tasksApi.decline(declineTask.id, {
        reasonId: declineReasonId,
        note: declineNote.trim() || undefined,
      });
      setDeclineTask(null);
      setDeclineReasonId(undefined);
      setDeclineNote('');
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Decline failed');
    } finally {
      setDeclining(false);
    }
  };

  const submitReassign = async () => {
    if (!reassignTask || !reassignTo) return;
    setReassigning(true);
    try {
      await tasksApi.reassign(reassignTask.id, reassignTo);
      setReassignTask(null);
      setReassignTo(undefined);
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Reassign failed');
    } finally {
      setReassigning(false);
    }
  };

  const actionsFor = (item: TaskDto) => {
    const s = item.status;
    const nodes: React.ReactNode[] = [];
    if (s === 'Assigned' || s === 'Reassigned') {
      nodes.push(<Button key="a" title="Accept" onPress={() => run(item.id, () => tasksApi.accept(item.id))} />);
      nodes.push(
        <Button
          key="d"
          title="Decline"
          variant="secondary"
          onPress={() => {
            setDeclineReasonId(reasons[0]?.id);
            setDeclineNote('');
            setDeclineTask(item);
          }}
        />,
      );
    }
    if (s === 'Accepted' || s === 'Deferred') {
      nodes.push(<Button key="s" title="Start" onPress={() => run(item.id, () => tasksApi.start(item.id))} />);
    }
    if (s === 'InProgress') {
      nodes.push(<Button key="p" title="Pause" variant="secondary" onPress={() => run(item.id, () => tasksApi.pause(item.id))} />);
      nodes.push(<Button key="c" title="Complete" onPress={() => run(item.id, () => tasksApi.complete(item.id))} />);
    }
    if (s === 'Paused') {
      nodes.push(<Button key="r" title="Resume" onPress={() => run(item.id, () => tasksApi.resume(item.id))} />);
      nodes.push(<Button key="c2" title="Complete" onPress={() => run(item.id, () => tasksApi.complete(item.id))} />);
    }
    if (s === 'Accepted' || s === 'InProgress' || s === 'Paused') {
      nodes.push(<Button key="df" title="Defer" variant="secondary" onPress={() => run(item.id, () => tasksApi.defer(item.id, 'Deferred from app'))} />);
    }
    if (s !== 'Completed' && s !== 'Cancelled' && s !== 'Declined') {
      nodes.push(
        <Button
          key="ra"
          title="Reassign"
          variant="secondary"
          onPress={() => {
            setReassignTo(members.find((m) => m.id !== item.assignedToMemberId)?.id);
            setReassignTask(item);
          }}
        />,
      );
    }
    if (s !== 'Completed' && s !== 'Cancelled') {
      nodes.push(<Button key="x" title="Cancel" variant="secondary" onPress={() => run(item.id, () => tasksApi.cancel(item.id, 'Cancelled from app'))} />);
    }
    nodes.push(<Button key="h" title="History" variant="secondary" onPress={() => openHistory(item)} />);
    return nodes;
  };

  return (
    <View style={styles.container}>
      <View style={styles.toolbar}>
        <View style={styles.scopeRow}>
          <Pressable onPress={() => setScope('mine')} style={[styles.chip, scope === 'mine' && styles.chipOn]}>
            <Text style={styles.chipText}>Mine</Text>
          </Pressable>
          <Pressable onPress={() => setScope('family')} style={[styles.chip, scope === 'family' && styles.chipOn]}>
            <Text style={styles.chipText}>Family</Text>
          </Pressable>
        </View>
        <Button title="New task" onPress={() => setCreateOpen(true)} />
      </View>
      {error ? <Text style={styles.hint}>{error}</Text> : null}
      {loading ? <ActivityIndicator style={{ marginTop: spacing.lg }} color={colors.primary} /> : null}
      <FlatList
        data={tasks}
        keyExtractor={(item) => item.id}
        contentContainerStyle={styles.list}
        ListEmptyComponent={!loading ? <EmptyState title="No tasks" subtitle="Create one or switch scope." /> : null}
        renderItem={({ item }) => (
          <Card style={styles.card}>
            <Text style={typography.heading}>{item.title}</Text>
            {item.description ? <Text style={typography.caption}>{item.description}</Text> : null}
            <View style={styles.row}>
              <Badge label={item.status} tone={statusTone(item.status)} />
              {item.category ? <Badge label={item.category} /> : null}
              {item.assignedToName ? <Badge label={item.assignedToName} /> : null}
            </View>
            <View style={styles.actions}>
              {busyId === item.id ? <ActivityIndicator color={colors.primary} /> : actionsFor(item)}
            </View>
          </Card>
        )}
      />

      <Modal visible={createOpen} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>New task</Text>
            <TextInput style={styles.input} placeholder="Title" placeholderTextColor={colors.textMuted} value={title} onChangeText={setTitle} />
            <TextInput style={styles.input} placeholder="Category" placeholderTextColor={colors.textMuted} value={category} onChangeText={setCategory} />
            {members.length > 0 ? (
              <View style={styles.memberRow}>
                <Pressable style={[styles.chip, !assignTo && styles.chipOn]} onPress={() => setAssignTo(undefined)}>
                  <Text style={styles.chipText}>Unassigned</Text>
                </Pressable>
                {members.map((m) => (
                  <Pressable key={m.id} style={[styles.chip, assignTo === m.id && styles.chipOn]} onPress={() => setAssignTo(m.id)}>
                    <Text style={styles.chipText}>{m.displayName}</Text>
                  </Pressable>
                ))}
              </View>
            ) : null}
            <View style={styles.modalActions}>
              <Button title="Cancel" variant="secondary" onPress={() => setCreateOpen(false)} />
              <Button title={creating ? 'Saving…' : 'Create'} onPress={() => void createTask()} />
            </View>
          </View>
        </View>
      </Modal>

      <Modal visible={!!declineTask} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>Decline · {declineTask?.title}</Text>
            <Text style={typography.caption}>Pick a reason (optional note)</Text>
            <View style={styles.memberRow}>
              {reasons.length === 0 ? (
                <Text style={typography.caption}>No seeded reasons — note only.</Text>
              ) : (
                reasons.map((r) => (
                  <Pressable
                    key={r.id}
                    style={[styles.chip, declineReasonId === r.id && styles.chipOn]}
                    onPress={() => setDeclineReasonId(r.id)}
                  >
                    <Text style={styles.chipText}>{r.text}</Text>
                  </Pressable>
                ))
              )}
            </View>
            <TextInput
              style={styles.input}
              placeholder="Note (optional)"
              placeholderTextColor={colors.textMuted}
              value={declineNote}
              onChangeText={setDeclineNote}
            />
            <View style={styles.modalActions}>
              <Button title="Cancel" variant="secondary" onPress={() => setDeclineTask(null)} />
              <Button title={declining ? 'Declining…' : 'Decline'} onPress={() => void submitDecline()} />
            </View>
          </View>
        </View>
      </Modal>

      <Modal visible={!!reassignTask} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>Reassign · {reassignTask?.title}</Text>
            <Text style={typography.caption}>Choose a family member</Text>
            <View style={styles.memberRow}>
              {members.map((m) => (
                <Pressable
                  key={m.id}
                  style={[styles.chip, reassignTo === m.id && styles.chipOn]}
                  onPress={() => setReassignTo(m.id)}
                >
                  <Text style={styles.chipText}>{m.displayName}</Text>
                </Pressable>
              ))}
            </View>
            <View style={styles.modalActions}>
              <Button title="Cancel" variant="secondary" onPress={() => setReassignTask(null)} />
              <Button
                title={reassigning ? 'Saving…' : 'Reassign'}
                onPress={() => void submitReassign()}
              />
            </View>
          </View>
        </View>
      </Modal>

      <Modal visible={historyOpen} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>History · {historyTitle}</Text>
            {history.length === 0 ? (
              <Text style={typography.caption}>No history entries.</Text>
            ) : (
              history.map((h) => (
                <View key={h.id} style={styles.histRow}>
                  <Badge label={h.status} tone={statusTone(h.status)} />
                  <Text style={typography.caption}>{h.detail}</Text>
                  <Text style={styles.histTime}>{new Date(h.timestampUtc).toLocaleString()}</Text>
                </View>
              ))
            )}
            <Button title="Close" onPress={() => setHistoryOpen(false)} />
          </View>
        </View>
      </Modal>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  toolbar: {
    flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between',
    paddingHorizontal: spacing.md, paddingTop: spacing.md, gap: spacing.sm,
  },
  scopeRow: { flexDirection: 'row', gap: spacing.sm },
  chip: { paddingHorizontal: spacing.sm, paddingVertical: 6, borderRadius: 16, backgroundColor: colors.surface },
  chipOn: { backgroundColor: colors.primary },
  chipText: { ...typography.caption, color: colors.text },
  list: { padding: spacing.md, gap: spacing.md },
  card: { marginBottom: spacing.md, gap: spacing.sm },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  actions: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, marginTop: spacing.xs },
  hint: { ...typography.caption, paddingHorizontal: spacing.md, color: colors.danger },
  modalBackdrop: { flex: 1, backgroundColor: 'rgba(0,0,0,0.5)', justifyContent: 'flex-end' },
  modalCard: {
    backgroundColor: colors.surface, borderTopLeftRadius: 16, borderTopRightRadius: 16,
    padding: spacing.lg, gap: spacing.md, maxHeight: '80%',
  },
  input: {
    borderWidth: 1, borderColor: colors.border, borderRadius: 8, padding: spacing.sm,
    color: colors.text, backgroundColor: colors.bg,
  },
  memberRow: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  modalActions: { flexDirection: 'row', justifyContent: 'flex-end', gap: spacing.sm },
  histRow: { gap: 4, marginBottom: spacing.sm },
  histTime: { ...typography.caption, opacity: 0.7 },
});
