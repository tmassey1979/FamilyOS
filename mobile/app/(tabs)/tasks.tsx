import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Modal,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { Badge, Button, Card, EmptyState, SectionTitle } from '../../src/components/ui';
import { familyApi, meApi, tasksApi, type TaskDto } from '../../src/api/client';
import { colors, spacing, typography } from '../../src/theme';

type Member = { id: string; displayName: string; role: string; isActive: boolean };
type Priority = 'Low' | 'Normal' | 'High' | 'Urgent';

const PRIORITIES: Priority[] = ['Low', 'Normal', 'High', 'Urgent'];

function statusLabel(status: string) {
  return status.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function dueLabel(value?: string) {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toLocaleDateString();
}

export default function TasksScreen() {
  const [tasks, setTasks] = useState<TaskDto[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [currentMemberId, setCurrentMemberId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [assigneeId, setAssigneeId] = useState('');
  const [priority, setPriority] = useState<Priority>('Normal');
  const [dueDate, setDueDate] = useState('');

  const activeMembers = useMemo(() => members.filter((member) => member.isActive), [members]);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [list, familyMembers, me] = await Promise.all([
        tasksApi.family(),
        familyApi.members(),
        meApi.get(),
      ]);
      setTasks(list);
      setMembers(familyMembers);
      setCurrentMemberId(me.memberId ?? null);
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

  const resetCreateForm = () => {
    setTitle('');
    setDescription('');
    setAssigneeId('');
    setPriority('Normal');
    setDueDate('');
  };

  const createTask = async () => {
    if (!title.trim()) {
      setError('Give the task a title before saving.');
      return;
    }

    let dueDateIso: string | undefined;
    if (dueDate.trim()) {
      const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dueDate.trim());
      if (!match) {
        setError('Enter the due date as YYYY-MM-DD.');
        return;
      }
      const parsed = new Date(`${dueDate.trim()}T12:00:00`);
      if (Number.isNaN(parsed.getTime()) || parsed.toISOString().slice(0, 10) !== dueDate.trim()) {
        setError('Enter a real calendar date.');
        return;
      }
      dueDateIso = parsed.toISOString();
    }

    setSaving(true);
    setError(null);
    try {
      await tasksApi.create({
        title: title.trim(),
        description: description.trim() || undefined,
        assignToMemberId: assigneeId || undefined,
        priority,
        dueDate: dueDateIso,
      });
      setCreateOpen(false);
      resetCreateForm();
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not create task');
    } finally {
      setSaving(false);
    }
  };

  const actionsFor = (task: TaskDto) => {
    if (!currentMemberId || task.assignedToMemberId !== currentMemberId) return [];
    const status = task.status;
    const actions: Array<{ label: string; run: () => Promise<unknown>; primary?: boolean }> = [];

    if (status === 'Assigned' || status === 'Reassigned') {
      actions.push({ label: 'Accept', run: () => tasksApi.accept(task.id), primary: true });
    } else if (status === 'Accepted') {
      actions.push({ label: 'Start', run: () => tasksApi.start(task.id), primary: true });
      actions.push({ label: 'Complete', run: () => tasksApi.complete(task.id) });
    } else if (status === 'InProgress') {
      actions.push({ label: 'Pause', run: () => tasksApi.pause(task.id) });
      actions.push({ label: 'Complete', run: () => tasksApi.complete(task.id), primary: true });
    } else if (status === 'Paused') {
      actions.push({ label: 'Resume', run: () => tasksApi.resume(task.id), primary: true });
      actions.push({ label: 'Complete', run: () => tasksApi.complete(task.id) });
    } else if (status === 'Deferred') {
      actions.push({ label: 'Start', run: () => tasksApi.start(task.id), primary: true });
    }

    return actions;
  };

  return (
    <>
      <ScrollView
        style={styles.container}
        contentContainerStyle={styles.content}
        refreshControl={<RefreshControl refreshing={loading} onRefresh={() => void load()} />}
      >
        <View style={styles.header}>
          <View style={styles.headerText}>
            <SectionTitle>Tasks</SectionTitle>
            <Text style={styles.subtitle}>Make the next thing clear, then get it done.</Text>
          </View>
          <Button title="+ New task" onPress={() => { setError(null); setCreateOpen(true); }} />
        </View>

        {error ? <Text style={styles.error}>{error}</Text> : null}
        {loading && tasks.length === 0 ? <ActivityIndicator color={colors.primary} /> : null}
        {!loading && tasks.length === 0 ? (
          <EmptyState title="No tasks yet" subtitle="Create the first task or wait for someone to assign one to you." />
        ) : null}

        {tasks.map((task) => {
          const actions = actionsFor(task);
          const due = dueLabel(task.dueDate);
          const assignee = task.assignedToName || 'Unassigned';
          return (
            <Card key={task.id} style={styles.card}>
              <View style={styles.row}>
                <Text style={[typography.heading, styles.taskTitle]}>{task.title}</Text>
                {task.isOverdue ? <Badge label="Overdue" tone="danger" /> : null}
                {task.priority && task.priority !== 'Normal' ? (
                  <Badge label={task.priority} tone={task.priority === 'Urgent' ? 'danger' : 'warning'} />
                ) : null}
              </View>
              {task.description ? <Text style={typography.caption}>{task.description}</Text> : null}
              <View style={styles.metaRow}>
                <Badge label={statusLabel(task.status)} tone={task.status === 'Completed' ? 'success' : 'default'} />
                <Text style={typography.caption}>{assignee}</Text>
                {due ? <Text style={typography.caption}>Due {due}</Text> : null}
              </View>
              {actions.length > 0 ? (
                <View style={styles.row}>
                  {actions.map((action) => (
                    <Button
                      key={action.label}
                      title={busyId === task.id ? 'Working…' : action.label}
                      variant={action.primary ? 'primary' : 'secondary'}
                      disabled={busyId !== null}
                      onPress={() => void run(task.id, action.run)}
                    />
                  ))}
                </View>
              ) : null}
            </Card>
          );
        })}
      </ScrollView>

      <Modal visible={createOpen} animationType="slide" presentationStyle="pageSheet" onRequestClose={() => setCreateOpen(false)}>
        <View style={styles.modal}>
          <View style={styles.modalHeader}>
            <Text style={typography.heading}>Create a task</Text>
            <Pressable onPress={() => setCreateOpen(false)} accessibilityRole="button" accessibilityLabel="Close task form">
              <Text style={styles.close}>Close</Text>
            </Pressable>
          </View>

          <ScrollView contentContainerStyle={styles.formContent} keyboardShouldPersistTaps="handled">\n            {error ? <Text style={styles.error}>{error}</Text> : null}
            <Text style={styles.label}>Task title *</Text>
            <TextInput
              value={title}
              onChangeText={setTitle}
              placeholder="What needs to get done?"
              placeholderTextColor={colors.textMuted}
              style={styles.input}
              returnKeyType="next"
              maxLength={120}
            />

            <Text style={styles.label}>Details</Text>
            <TextInput
              value={description}
              onChangeText={setDescription}
              placeholder="Add helpful context (optional)"
              placeholderTextColor={colors.textMuted}
              style={[styles.input, styles.multiline]}
              multiline
              textAlignVertical="top"
              maxLength={1000}
            />

            <Text style={styles.label}>Assign to</Text>
            <View style={styles.choiceWrap}>
              <Pressable
                onPress={() => setAssigneeId('')}
                style={[styles.choice, !assigneeId && styles.choiceSelected]}
              >
                <Text style={[styles.choiceText, !assigneeId && styles.choiceTextSelected]}>Unassigned</Text>
              </Pressable>
              {activeMembers.map((member) => (
                <Pressable
                  key={member.id}
                  onPress={() => setAssigneeId(member.id)}
                  style={[styles.choice, assigneeId === member.id && styles.choiceSelected]}
                >
                  <Text style={[styles.choiceText, assigneeId === member.id && styles.choiceTextSelected]}>
                    {member.displayName}
                  </Text>
                </Pressable>
              ))}
            </View>
            <Text style={styles.helper}>An assignment is a request—not acceptance. The assignee must accept it.</Text>

            <Text style={styles.label}>Priority</Text>
            <View style={styles.choiceWrap}>
              {PRIORITIES.map((value) => (
                <Pressable
                  key={value}
                  onPress={() => setPriority(value)}
                  style={[styles.choice, priority === value && styles.choiceSelected]}
                >
                  <Text style={[styles.choiceText, priority === value && styles.choiceTextSelected]}>{value}</Text>
                </Pressable>
              ))}
            </View>

            <Text style={styles.label}>Due date</Text>
            <TextInput
              value={dueDate}
              onChangeText={setDueDate}
              placeholder="YYYY-MM-DD (optional)"
              placeholderTextColor={colors.textMuted}
              style={styles.input}
              keyboardType="numbers-and-punctuation"
              maxLength={10}
            />
          </ScrollView>

          <View style={styles.footer}>
            <Button title={saving ? 'Creating…' : 'Create task'} onPress={() => void createTask()} disabled={saving || !title.trim()} />
            <Button title="Cancel" variant="secondary" onPress={() => setCreateOpen(false)} disabled={saving} />
          </View>
        </View>
      </Modal>
    </>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  content: { padding: spacing.md, paddingBottom: spacing.xl * 2, gap: spacing.sm },
  header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm },
  headerText: { flex: 1 },
  subtitle: { ...typography.caption, marginTop: spacing.xs },
  card: { gap: spacing.sm, marginBottom: spacing.sm },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, alignItems: 'center' },
  taskTitle: { flex: 1 },
  metaRow: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, alignItems: 'center' },
  error: { ...typography.caption, color: colors.danger },
  modal: { flex: 1, backgroundColor: colors.bg, paddingTop: spacing.lg },
  modalHeader: { paddingHorizontal: spacing.md, paddingBottom: spacing.md, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  close: { ...typography.body, color: colors.primary, fontWeight: '600' },
  formContent: { padding: spacing.md, paddingBottom: spacing.xl, gap: spacing.sm },
  label: { ...typography.caption, color: colors.text, fontWeight: '700', marginTop: spacing.sm },
  input: { minHeight: 48, borderWidth: 1, borderColor: colors.border, borderRadius: 12, paddingHorizontal: spacing.md, color: colors.text, backgroundColor: colors.surface },
  multiline: { minHeight: 104, paddingTop: spacing.md },
  choiceWrap: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  choice: { borderWidth: 1, borderColor: colors.border, borderRadius: 20, paddingHorizontal: spacing.md, paddingVertical: spacing.sm, backgroundColor: colors.surface },
  choiceSelected: { borderColor: colors.primary, backgroundColor: colors.primary },
  choiceText: { ...typography.caption, color: colors.text },
  choiceTextSelected: { color: '#FFFFFF', fontWeight: '700' },
  helper: { ...typography.caption, color: colors.textMuted },
  footer: { padding: spacing.md, paddingBottom: spacing.xl, gap: spacing.sm, borderTopWidth: 1, borderTopColor: colors.border },
});
