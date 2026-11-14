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
import {
  familyApi,
  meApi,
  tasksApi,
  type TaskDto,
  type TaskHistoryDto,
  type RecurrenceDto,
} from '../../src/api/client';
import { colors, spacing, typography } from '../../src/theme';
import { withObserveScreen } from '../../src/analytics/withObserveScreen';

type Member = { id: string; displayName: string; role: string; isActive: boolean };
type Priority = 'Low' | 'Normal' | 'High' | 'Urgent';
type TaskFilter = 'All' | 'Mine' | 'Needs acceptance' | 'Active' | 'Closed';
type NoteAction = 'decline' | 'defer' | 'cancel' | 'skip';

const PRIORITIES: Priority[] = ['Low', 'Normal', 'High', 'Urgent'];
const FILTERS: TaskFilter[] = ['All', 'Mine', 'Needs acceptance', 'Active', 'Closed'];
const RECURRENCE_FREQUENCIES = ['Daily', 'Weekly', 'BiWeekly', 'Monthly', 'Yearly'] as const;
const DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

function statusLabel(status: string) {
  return status.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function dueLabel(value?: string) {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toLocaleDateString();
}

function isAdultRole(role?: string | null) {
  return role === 'Owner' || role === 'Adult';
}

function TasksScreen() {
  const [tasks, setTasks] = useState<TaskDto[]>([]);
  const [recurrences, setRecurrences] = useState<RecurrenceDto[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [currentMemberId, setCurrentMemberId] = useState<string | null>(null);
  const [currentRole, setCurrentRole] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [filter, setFilter] = useState<TaskFilter>('All');

  const [formOpen, setFormOpen] = useState(false);
  const [formTask, setFormTask] = useState<TaskDto | null>(null);
  const [saving, setSaving] = useState(false);
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [assigneeId, setAssigneeId] = useState('');
  const [priority, setPriority] = useState<Priority>('Normal');
  const [dueDate, setDueDate] = useState('');

  const [recurrenceOpen, setRecurrenceOpen] = useState(false);
  const [recurrenceTitle, setRecurrenceTitle] = useState('');
  const [recurrenceDescription, setRecurrenceDescription] = useState('');
  const [recurrenceFrequency, setRecurrenceFrequency] = useState<(typeof RECURRENCE_FREQUENCIES)[number]>('Weekly');
  const [recurrenceInterval, setRecurrenceInterval] = useState('1');
  const [recurrenceDay, setRecurrenceDay] = useState('1');
  const [recurrenceAssignee, setRecurrenceAssignee] = useState('');
  const [recurrenceCategory, setRecurrenceCategory] = useState('');
  const [recurrenceSaving, setRecurrenceSaving] = useState(false);

  const [historyTask, setHistoryTask] = useState<TaskDto | null>(null);
  const [history, setHistory] = useState<TaskHistoryDto[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);

  const [noteTask, setNoteTask] = useState<TaskDto | null>(null);
  const [noteAction, setNoteAction] = useState<NoteAction>('decline');
  const [note, setNote] = useState('');

  const isAdult = isAdultRole(currentRole);
  const activeMembers = useMemo(() => members.filter((member) => member.isActive), [members]);

  const load = useCallback(async (quiet = false) => {
    if (!quiet) setLoading(true);
    setError(null);
    try {
      const [list, familyMembers, me, recurrenceList] = await Promise.all([
        tasksApi.family(),
        familyApi.members(),
        meApi.get(),
        tasksApi.recurrences().catch(() => [] as RecurrenceDto[]),
      ]);
      setTasks(list);
      setRecurrences(recurrenceList);
      setMembers(familyMembers);
      setCurrentMemberId(me.memberId ?? null);
      setCurrentRole(me.role ?? null);
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not load family tasks');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const visibleTasks = useMemo(() => tasks.filter((task) => {
    if (filter === 'Mine') return task.assignedToMemberId === currentMemberId;
    if (filter === 'Needs acceptance') {
      return task.assignedToMemberId === currentMemberId && ['Assigned', 'Reassigned'].includes(task.status);
    }
    if (filter === 'Active') return ['Accepted', 'InProgress', 'Paused', 'Deferred'].includes(task.status);
    if (filter === 'Closed') return ['Completed', 'Cancelled', 'Skipped', 'Declined'].includes(task.status);
    return true;
  }), [tasks, filter, currentMemberId]);

  const run = async (id: string, action: () => Promise<unknown>) => {
    setBusyId(id);
    setError(null);
    try {
      await action();
      await load(true);
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Task action failed');
    } finally {
      setBusyId(null);
    }
  };

  const resetForm = () => {
    setFormTask(null);
    setTitle('');
    setDescription('');
    setAssigneeId('');
    setPriority('Normal');
    setDueDate('');
  };

  const openCreate = () => {
    resetForm();
    setError(null);
    setFormOpen(true);
  };

  const openEdit = (task: TaskDto) => {
    setFormTask(task);
    setTitle(task.title);
    setDescription(task.description ?? '');
    setAssigneeId(task.assignedToMemberId ?? '');
    setPriority((PRIORITIES.includes(task.priority as Priority) ? task.priority : 'Normal') as Priority);
    setDueDate(task.dueDate ? new Date(task.dueDate).toISOString().slice(0, 10) : '');
    setError(null);
    setFormOpen(true);
  };

  const parseDueDate = (): string | undefined | null => {
    if (!dueDate.trim()) return undefined;
    if (!/^\d{4}-\d{2}-\d{2}$/.test(dueDate.trim())) return null;
    const parsed = new Date(`${dueDate.trim()}T12:00:00`);
    if (Number.isNaN(parsed.getTime()) || parsed.toISOString().slice(0, 10) !== dueDate.trim()) return null;
    return parsed.toISOString();
  };

  const saveTask = async () => {
    if (!title.trim()) {
      setError('Give the task a title before saving.');
      return;
    }
    const parsedDue = parseDueDate();
    if (parsedDue === null) {
      setError('Enter a real date using YYYY-MM-DD.');
      return;
    }

    setSaving(true);
    setError(null);
    try {
      if (formTask) {
        await tasksApi.updateDetails(formTask.id, {
          title: title.trim(),
          description: description.trim(),
          priority,
          dueDate: parsedDue ?? null,
          clearDueDate: !parsedDue,
        });
        if (assigneeId && assigneeId !== formTask.assignedToMemberId) {
          if (formTask.assignedToMemberId) await tasksApi.reassign(formTask.id, assigneeId);
          else await tasksApi.assign(formTask.id, assigneeId);
        }
      } else {
        await tasksApi.create({
          title: title.trim(),
          description: description.trim() || undefined,
          assignToMemberId: assigneeId || undefined,
          priority,
          dueDate: parsedDue ?? undefined,
        });
      }
      setFormOpen(false);
      resetForm();
      await load(true);
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not save task');
    } finally {
      setSaving(false);
    }
  };

  const openRecurrenceForm = () => {
    setRecurrenceTitle('');
    setRecurrenceDescription('');
    setRecurrenceFrequency('Weekly');
    setRecurrenceInterval('1');
    setRecurrenceDay('1');
    setRecurrenceAssignee('');
    setRecurrenceCategory('');
    setError(null);
    setRecurrenceOpen(true);
  };

  const saveRecurrence = async () => {
    if (!recurrenceTitle.trim()) {
      setError('Give the recurring task a title.');
      return;
    }
    const interval = Number(recurrenceInterval);
    if (!Number.isInteger(interval) || interval < 1 || interval > 365) {
      setError('Repeat interval must be a whole number from 1 to 365.');
      return;
    }
    setRecurrenceSaving(true);
    setError(null);
    try {
      await tasksApi.createRecurrence({
        title: recurrenceTitle.trim(),
        description: recurrenceDescription.trim() || undefined,
        frequency: recurrenceFrequency,
        interval,
        dayOfWeek: recurrenceFrequency === 'Weekly' ? Number(recurrenceDay) : undefined,
        defaultAssigneeMemberId: recurrenceAssignee || undefined,
        category: recurrenceCategory.trim() || undefined,
        generateFirstInstance: true,
      });
      setRecurrenceOpen(false);
      await load(true);
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not create recurring task');
    } finally {
      setRecurrenceSaving(false);
    }
  };

  const deactivateRecurrence = async (recurrence: RecurrenceDto) => {
    setBusyId(recurrence.id);
    setError(null);
    try {
      await tasksApi.deactivateRecurrence(recurrence.id);
      await load(true);
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not stop recurring task');
    } finally {
      setBusyId(null);
    }
  };

  const openHistory = async (task: TaskDto) => {
    setHistoryTask(task);
    setHistory([]);
    setHistoryLoading(true);
    try {
      setHistory(await tasksApi.history(task.id));
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not load task history');
    } finally {
      setHistoryLoading(false);
    }
  };

  const openNoteAction = (task: TaskDto, action: NoteAction) => {
    setNoteTask(task);
    setNoteAction(action);
    setNote('');
  };

  const confirmNoteAction = async () => {
    if (!noteTask) return;
    const task = noteTask;
    const action = noteAction;
    setNoteTask(null);
    await run(task.id, async () => {
      if (action === 'decline') return tasksApi.decline(task.id, { note: note.trim() || undefined });
      if (action === 'defer') return tasksApi.defer(task.id, note.trim() || undefined);
      if (action === 'cancel') return tasksApi.cancel(task.id, note.trim() || undefined);
      return tasksApi.skip(task.id, note.trim() || undefined);
    });
  };

  const actionsFor = (task: TaskDto) => {
    const isAssignee = !!currentMemberId && task.assignedToMemberId === currentMemberId;
    const actions: Array<{ label: string; onPress: () => void; primary?: boolean; danger?: boolean }> = [];

    if (isAssignee) {
      if (task.status === 'Assigned' || task.status === 'Reassigned') {
        actions.push({ label: 'Accept', onPress: () => void run(task.id, () => tasksApi.accept(task.id)), primary: true });
        actions.push({ label: 'Decline', onPress: () => openNoteAction(task, 'decline'), danger: true });
      } else if (task.status === 'Accepted') {
        actions.push({ label: 'Start', onPress: () => void run(task.id, () => tasksApi.start(task.id)), primary: true });
        actions.push({ label: 'Complete', onPress: () => void run(task.id, () => tasksApi.complete(task.id)) });
        actions.push({ label: 'Defer', onPress: () => openNoteAction(task, 'defer') });
        actions.push({ label: 'Decline', onPress: () => openNoteAction(task, 'decline'), danger: true });
      } else if (task.status === 'InProgress') {
        actions.push({ label: 'Pause', onPress: () => void run(task.id, () => tasksApi.pause(task.id)) });
        actions.push({ label: 'Complete', onPress: () => void run(task.id, () => tasksApi.complete(task.id)), primary: true });
        actions.push({ label: 'Defer', onPress: () => openNoteAction(task, 'defer') });
      } else if (task.status === 'Paused') {
        actions.push({ label: 'Resume', onPress: () => void run(task.id, () => tasksApi.resume(task.id)), primary: true });
        actions.push({ label: 'Complete', onPress: () => void run(task.id, () => tasksApi.complete(task.id)) });
      } else if (task.status === 'Deferred') {
        actions.push({ label: 'Start', onPress: () => void run(task.id, () => tasksApi.start(task.id)), primary: true });
      }
      if (task.isRecurring && !['Completed', 'Cancelled', 'Skipped'].includes(task.status)) {
        actions.push({ label: 'Skip occurrence', onPress: () => openNoteAction(task, 'skip') });
      }
    }

    if (isAdult && !['Completed', 'Cancelled', 'Skipped'].includes(task.status)) {
      actions.push({ label: 'Edit / assign', onPress: () => openEdit(task) });
      actions.push({ label: 'Cancel', onPress: () => openNoteAction(task, 'cancel'), danger: true });
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
            <SectionTitle>Family tasks</SectionTitle>
            <Text style={styles.subtitle}>Clear ownership. Clear next steps.</Text>
          </View>
          {isAdult ? (
            <View style={styles.headerActions}>
              <Button title="Recurring" variant="secondary" onPress={openRecurrenceForm} />
              <Button title="+ New task" onPress={openCreate} />
            </View>
          ) : null}
        </View>

        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.filters}>
          {FILTERS.map((item) => {
            const selected = filter === item;
            return (
              <Pressable
                key={item}
                onPress={() => setFilter(item)}
                style={[styles.filter, selected && styles.filterSelected]}
                accessibilityRole="button"
                accessibilityState={{ selected }}
              >
                <Text style={[styles.filterText, selected && styles.filterTextSelected]}>{item}</Text>
              </Pressable>
            );
          })}
        </ScrollView>

        {isAdult && recurrences.length > 0 ? (
          <Card style={styles.recurrenceCard}>
            <View style={styles.taskHeading}>
              <Text style={typography.heading}>Recurring schedules</Text>
              <Badge label={String(recurrences.filter((item) => item.isActive).length) + ' active'} />
            </View>
            {recurrences.map((item) => (
              <View key={item.id} style={styles.recurrenceRow}>
                <View style={{ flex: 1, gap: 2 }}>
                  <Text style={typography.body}>{item.title}</Text>
                  <Text style={typography.caption}>
                    {item.frequency}{item.interval > 1 ? ' · every ' + item.interval : ''}
                    {item.dayOfWeek != null ? ' · ' + DAYS[item.dayOfWeek] : ''}
                    {item.nextOccurrenceUtc ? ' · Next ' + dueLabel(item.nextOccurrenceUtc) : ''}
                  </Text>
                  <Text style={typography.caption}>{item.isActive ? 'Active' : 'Stopped'}</Text>
                </View>
                {item.isActive ? (
                  <Button
                    title={busyId === item.id ? 'Stopping…' : 'Stop'}
                    variant="secondary"
                    disabled={busyId !== null}
                    onPress={() => void deactivateRecurrence(item)}
                  />
                ) : null}
              </View>
            ))}
          </Card>
        ) : null}

        {error ? <Text style={styles.error}>{error}</Text> : null}
        {loading && tasks.length === 0 ? <ActivityIndicator color={colors.primary} /> : null}
        {!loading && visibleTasks.length === 0 ? (
          <EmptyState
            title={tasks.length === 0 ? 'No tasks yet' : 'Nothing in this view'}
            subtitle={tasks.length === 0 ? 'Create a task or wait for one to be assigned.' : 'Try another filter to see more family tasks.'}
          />
        ) : null}

        {visibleTasks.map((task) => {
          const actions = actionsFor(task);
          const due = dueLabel(task.dueDate);
          const assignee = task.assignedToName || 'Unassigned';
          return (
            <Card key={task.id} style={styles.card}>
              <View style={styles.taskHeading}>
                <Text style={[typography.heading, styles.taskTitle]}>{task.title}</Text>
                {task.isOverdue ? <Badge label="Overdue" tone="danger" /> : null}
                {task.priority && task.priority !== 'Normal' ? (
                  <Badge label={task.priority} tone={task.priority === 'Urgent' ? 'danger' : 'warning'} />
                ) : null}
              </View>
              {task.description ? <Text style={typography.caption}>{task.description}</Text> : null}
              <View style={styles.metaRow}>
                <Badge
                  label={statusLabel(task.status)}
                  tone={task.status === 'Completed' ? 'success' : task.status === 'Declined' || task.status === 'Cancelled' ? 'danger' : 'default'}
                />
                <Text style={typography.caption}>{assignee}</Text>
                {due ? <Text style={typography.caption}>Due {due}</Text> : null}
                {task.isRecurring ? <Badge label={task.recurrenceFrequency ? `Repeats ${task.recurrenceFrequency}` : 'Recurring'} /> : null}
              </View>
              <View style={styles.actionRow}>
                <Pressable onPress={() => void openHistory(task)} style={styles.textAction}>
                  <Text style={styles.textActionLabel}>History</Text>
                </Pressable>
                {actions.map((action) => (
                  <Button
                    key={action.label}
                    title={busyId === task.id ? 'Working…' : action.label}
                    variant={action.danger ? 'danger' : action.primary ? 'primary' : 'secondary'}
                    disabled={busyId !== null}
                    onPress={action.onPress}
                  />
                ))}
              </View>
            </Card>
          );
        })}
      </ScrollView>

      <Modal visible={formOpen} animationType="slide" presentationStyle="pageSheet" onRequestClose={() => setFormOpen(false)}>
        <View style={styles.modal}>
          <View style={styles.modalHeader}>
            <Text style={typography.heading}>{formTask ? 'Edit task' : 'Create a task'}</Text>
            <Pressable onPress={() => setFormOpen(false)} accessibilityRole="button" accessibilityLabel="Close task form">
              <Text style={styles.close}>Close</Text>
            </Pressable>
          </View>
          <ScrollView contentContainerStyle={styles.formContent} keyboardShouldPersistTaps="handled">
            {error ? <Text style={styles.error}>{error}</Text> : null}
            <Text style={styles.label}>Task title *</Text>
            <TextInput value={title} onChangeText={setTitle} placeholder="What needs to get done?" placeholderTextColor={colors.textMuted} style={styles.input} maxLength={120} />
            <Text style={styles.label}>Details</Text>
            <TextInput value={description} onChangeText={setDescription} placeholder="Helpful context (optional)" placeholderTextColor={colors.textMuted} style={[styles.input, styles.multiline]} multiline textAlignVertical="top" maxLength={1000} />
            <Text style={styles.label}>Assign to</Text>
            <View style={styles.choiceWrap}>
              {!formTask || !formTask.assignedToMemberId ? (
                <Pressable onPress={() => setAssigneeId('')} style={[styles.choice, !assigneeId && styles.choiceSelected]}>
                  <Text style={[styles.choiceText, !assigneeId && styles.choiceTextSelected]}>Unassigned</Text>
                </Pressable>
              ) : null}
              {activeMembers.map((member) => (
                <Pressable key={member.id} onPress={() => setAssigneeId(member.id)} style={[styles.choice, assigneeId === member.id && styles.choiceSelected]}>
                  <Text style={[styles.choiceText, assigneeId === member.id && styles.choiceTextSelected]}>{member.displayName}</Text>
                </Pressable>
              ))}
            </View>
            <Text style={styles.helper}>An assignment is a request, not acceptance. The assignee must explicitly accept it.</Text>
            <Text style={styles.label}>Priority</Text>
            <View style={styles.choiceWrap}>
              {PRIORITIES.map((value) => (
                <Pressable key={value} onPress={() => setPriority(value)} style={[styles.choice, priority === value && styles.choiceSelected]}>
                  <Text style={[styles.choiceText, priority === value && styles.choiceTextSelected]}>{value}</Text>
                </Pressable>
              ))}
            </View>
            <Text style={styles.label}>Due date</Text>
            <TextInput value={dueDate} onChangeText={setDueDate} placeholder="YYYY-MM-DD (optional)" placeholderTextColor={colors.textMuted} style={styles.input} keyboardType="numbers-and-punctuation" maxLength={10} />
          </ScrollView>
          <View style={styles.footer}>
            <Button title={saving ? 'Saving…' : formTask ? 'Save changes' : 'Create task'} onPress={() => void saveTask()} disabled={saving || !title.trim()} />
            <Button title="Cancel" variant="secondary" onPress={() => setFormOpen(false)} disabled={saving} />
          </View>
        </View>
      </Modal>

      <Modal visible={recurrenceOpen} animationType="slide" presentationStyle="pageSheet" onRequestClose={() => setRecurrenceOpen(false)}>
        <View style={styles.modal}>
          <View style={styles.modalHeader}>
            <Text style={typography.heading}>New recurring task</Text>
            <Pressable onPress={() => setRecurrenceOpen(false)} accessibilityRole="button" accessibilityLabel="Close recurring task form">
              <Text style={styles.close}>Close</Text>
            </Pressable>
          </View>
          <ScrollView contentContainerStyle={styles.formContent} keyboardShouldPersistTaps="handled">
            {error ? <Text style={styles.error}>{error}</Text> : null}
            <Text style={styles.label}>Task title *</Text>
            <TextInput value={recurrenceTitle} onChangeText={setRecurrenceTitle} placeholder="e.g. Take out trash" placeholderTextColor={colors.textMuted} style={styles.input} maxLength={120} />
            <Text style={styles.label}>Details</Text>
            <TextInput value={recurrenceDescription} onChangeText={setRecurrenceDescription} placeholder="Optional instructions" placeholderTextColor={colors.textMuted} style={[styles.input, styles.multiline]} multiline textAlignVertical="top" />
            <Text style={styles.label}>Repeat</Text>
            <View style={styles.choiceWrap}>
              {RECURRENCE_FREQUENCIES.map((value) => (
                <Pressable key={value} onPress={() => setRecurrenceFrequency(value)} style={[styles.choice, recurrenceFrequency === value && styles.choiceSelected]}>
                  <Text style={[styles.choiceText, recurrenceFrequency === value && styles.choiceTextSelected]}>{value === 'BiWeekly' ? 'Every 2 weeks' : value}</Text>
                </Pressable>
              ))}
            </View>
            <Text style={styles.label}>Every how many periods?</Text>
            <TextInput value={recurrenceInterval} onChangeText={setRecurrenceInterval} keyboardType="number-pad" placeholder="1" placeholderTextColor={colors.textMuted} style={styles.input} />
            {recurrenceFrequency === 'Weekly' ? (
              <>
                <Text style={styles.label}>Day of week</Text>
                <View style={styles.choiceWrap}>
                  {DAYS.map((day, index) => (
                    <Pressable key={day} onPress={() => setRecurrenceDay(String(index))} style={[styles.choice, recurrenceDay === String(index) && styles.choiceSelected]}>
                      <Text style={[styles.choiceText, recurrenceDay === String(index) && styles.choiceTextSelected]}>{day}</Text>
                    </Pressable>
                  ))}
                </View>
              </>
            ) : null}
            <Text style={styles.label}>Assign to</Text>
            <View style={styles.choiceWrap}>
              <Pressable onPress={() => setRecurrenceAssignee('')} style={[styles.choice, !recurrenceAssignee && styles.choiceSelected]}>
                <Text style={[styles.choiceText, !recurrenceAssignee && styles.choiceTextSelected]}>Unassigned</Text>
              </Pressable>
              {activeMembers.map((member) => (
                <Pressable key={member.id} onPress={() => setRecurrenceAssignee(member.id)} style={[styles.choice, recurrenceAssignee === member.id && styles.choiceSelected]}>
                  <Text style={[styles.choiceText, recurrenceAssignee === member.id && styles.choiceTextSelected]}>{member.displayName}</Text>
                </Pressable>
              ))}
            </View>
            <Text style={styles.label}>Category</Text>
            <TextInput value={recurrenceCategory} onChangeText={setRecurrenceCategory} placeholder="Optional category" placeholderTextColor={colors.textMuted} style={styles.input} />
            <Text style={styles.helper}>The first occurrence will be created immediately. Later occurrences are generated as each task is completed or skipped.</Text>
          </ScrollView>
          <View style={styles.footer}>
            <Button title={recurrenceSaving ? 'Creating…' : 'Create schedule'} onPress={() => void saveRecurrence()} disabled={recurrenceSaving || !recurrenceTitle.trim()} />
            <Button title="Cancel" variant="secondary" onPress={() => setRecurrenceOpen(false)} disabled={recurrenceSaving} />
          </View>
        </View>
      </Modal>

      <Modal visible={!!noteTask} transparent animationType="fade" onRequestClose={() => setNoteTask(null)}>
        <View style={styles.scrim}>
          <View style={styles.dialog}>
            <Text style={typography.heading}>{noteAction === 'decline' ? 'Decline task' : noteAction === 'defer' ? 'Defer task until tomorrow' : noteAction === 'cancel' ? 'Cancel task' : 'Skip this occurrence'}</Text>
            <Text style={typography.caption}>{noteTask?.title}</Text>
            <TextInput
              value={note}
              onChangeText={setNote}
              placeholder={noteAction === 'decline' ? 'Reason (optional)' : 'Add a note (optional)'}
              placeholderTextColor={colors.textMuted}
              style={[styles.input, styles.multiline]}
              multiline
              textAlignVertical="top"
              maxLength={500}
            />
            <Button title="Confirm" onPress={() => void confirmNoteAction()} />
            <Button title="Keep task" variant="secondary" onPress={() => setNoteTask(null)} />
          </View>
        </View>
      </Modal>

      <Modal visible={!!historyTask} animationType="slide" presentationStyle="pageSheet" onRequestClose={() => setHistoryTask(null)}>
        <View style={styles.modal}>
          <View style={styles.modalHeader}>
            <View style={{ flex: 1 }}>
              <Text style={typography.heading}>Task history</Text>
              <Text style={typography.caption}>{historyTask?.title}</Text>
            </View>
            <Pressable onPress={() => setHistoryTask(null)} accessibilityRole="button" accessibilityLabel="Close task history">
              <Text style={styles.close}>Close</Text>
            </Pressable>
          </View>
          <ScrollView contentContainerStyle={styles.formContent}>
            {historyLoading ? <ActivityIndicator color={colors.primary} /> : null}
            {!historyLoading && history.length === 0 ? <EmptyState title="No history available" /> : null}
            {history.map((entry) => (
              <Card key={entry.id} style={styles.historyCard}>
                <View style={styles.taskHeading}>
                  <Badge label={statusLabel(entry.status)} />
                  <Text style={typography.caption}>{new Date(entry.timestampUtc).toLocaleString()}</Text>
                </View>
                <Text style={typography.caption}>{members.find((member) => member.id === entry.actorMemberId)?.displayName ?? 'Family member'}</Text>
                <Text style={typography.body}>{entry.detail}</Text>
              </Card>
            ))}
          </ScrollView>
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
  headerActions: { flexDirection: 'row', alignItems: 'center', gap: spacing.xs },
  recurrenceCard: { gap: spacing.sm, marginBottom: spacing.sm },
  recurrenceRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, paddingVertical: spacing.xs, borderTopWidth: 1, borderTopColor: colors.border },
  subtitle: { ...typography.caption, marginTop: spacing.xs },
  filters: { gap: spacing.sm, paddingVertical: spacing.sm },
  filter: { borderWidth: 1, borderColor: colors.border, borderRadius: 18, paddingHorizontal: spacing.md, paddingVertical: spacing.sm, backgroundColor: colors.surface },
  filterSelected: { borderColor: colors.primary, backgroundColor: colors.primary },
  filterText: { ...typography.caption, color: colors.text },
  filterTextSelected: { color: colors.bg, fontWeight: '700' },
  card: { gap: spacing.sm, marginBottom: spacing.sm },
  taskHeading: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, alignItems: 'center' },
  taskTitle: { flex: 1 },
  metaRow: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, alignItems: 'center' },
  actionRow: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: spacing.sm },
  textAction: { minHeight: 44, paddingHorizontal: spacing.sm, justifyContent: 'center' },
  textActionLabel: { ...typography.caption, color: colors.primary, fontWeight: '700' },
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
  scrim: { flex: 1, backgroundColor: '#00000099', justifyContent: 'center', padding: spacing.lg },
  dialog: { backgroundColor: colors.bg, borderRadius: 18, borderWidth: 1, borderColor: colors.border, padding: spacing.md, gap: spacing.md },
  historyCard: { gap: spacing.sm },
});

export default withObserveScreen(TasksScreen);