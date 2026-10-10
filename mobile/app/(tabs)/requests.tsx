import { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator,
  FlatList,
  Modal,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { Badge, Button, Card, EmptyState, SectionTitle } from '../../src/components/ui';
import {
  conditionsApi,
  requestsApi,
  type ConditionDto,
  type ExecutionPlanDto,
  type RequestDto,
  type RequestTypeDto,
} from '../../src/api/client';
import { colors, spacing, typography } from '../../src/theme';

type Tab = 'mine' | 'queue';

function statusTone(status: string): 'default' | 'warning' | 'success' | 'danger' {
  if (status === 'Submitted' || status === 'UnderReview' || status === 'WaitingForInformation') return 'warning';
  if (status === 'Approved' || status === 'ConditionallyApproved' || status === 'Executable' || status === 'Completed') {
    return 'success';
  }
  if (status === 'Denied' || status === 'Cancelled') return 'danger';
  return 'default';
}

const QUEUEABLE = new Set(['Submitted', 'UnderReview', 'WaitingForInformation']);
const APPROVABLE = new Set(['Submitted', 'UnderReview']);
const PLANABLE = new Set(['Approved', 'ConditionallyApproved', 'Executable']);

export default function RequestsScreen() {
  const [types, setTypes] = useState<RequestTypeDto[]>([]);
  const [mine, setMine] = useState<RequestDto[]>([]);
  const [queue, setQueue] = useState<RequestDto[]>([]);
  const [tab, setTab] = useState<Tab>('mine');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [creating, setCreating] = useState<string | null>(null);
  const [actionId, setActionId] = useState<string | null>(null);

  // Ask question modal
  const [askFor, setAskFor] = useState<RequestDto | null>(null);
  const [questionText, setQuestionText] = useState('');
  // Answer modal
  const [answerFor, setAnswerFor] = useState<{ req: RequestDto; questionId: string; prompt: string } | null>(null);
  const [answerText, setAnswerText] = useState('');
  // Plan modal
  const [plan, setPlan] = useState<ExecutionPlanDto | null>(null);
  const [planTitle, setPlanTitle] = useState('');

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

  const run = async (id: string, fn: () => Promise<unknown>) => {
    setActionId(id);
    setError(null);
    try {
      await fn();
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Action failed');
    } finally {
      setActionId(null);
    }
  };

  const startRequest = async (type: RequestTypeDto) => {
    setCreating(type.code);
    try {
      const answers: Record<string, unknown> = {};
      if (type.code === 'Ride' || type.code.toLowerCase() === 'ride') {
        answers.where = 'School';
        answers.when = 'Today after practice';
      }
      const created = await requestsApi.create({
        type: type.code,
        title: type.code === 'Ride' || type.code.toLowerCase() === 'ride'
          ? 'Ride needed'
          : type.name,
        answers,
      });
      await requestsApi.submit(created.id);
      await load();
      setTab('mine');
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Create failed');
    } finally {
      setCreating(null);
    }
  };

  const submitAsk = async () => {
    if (!askFor || !questionText.trim()) return;
    await run(askFor.id, () => requestsApi.askQuestion(askFor.id, questionText.trim()));
    setAskFor(null);
    setQuestionText('');
  };

  const submitAnswer = async () => {
    if (!answerFor || !answerText.trim()) return;
    await run(answerFor.req.id, () =>
      requestsApi.answer(answerFor.req.id, answerFor.questionId, answerText.trim()),
    );
    setAnswerFor(null);
    setAnswerText('');
  };

  const openPlan = async (item: RequestDto) => {
    setActionId(item.id);
    try {
      const p = await requestsApi.generatePlan(item.id);
      setPlan(p);
      setPlanTitle(item.title);
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not generate plan');
    } finally {
      setActionId(null);
    }
  };

  const commitPlan = async () => {
    if (!plan) return;
    setActionId(plan.id);
    try {
      await requestsApi.commitPlan(plan.id);
      setPlan(null);
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Commit failed');
    } finally {
      setActionId(null);
    }
  };

  const renderActions = (item: RequestDto, isQueue: boolean) => {
    const nodes: React.ReactNode[] = [];
    const s = item.status;

    if (isQueue && APPROVABLE.has(s)) {
      nodes.push(
        <Button key="ap" title="Approve" onPress={() => void run(item.id, () => requestsApi.approve(item.id))} />,
      );
      nodes.push(
        <Button
          key="cond"
          title="Conditional"
          variant="secondary"
          onPress={() => void run(item.id, () => requestsApi.approve(item.id, true))}
        />,
      );
      nodes.push(
        <Button
          key="deny"
          title="Deny"
          variant="secondary"
          onPress={() => void run(item.id, () => requestsApi.deny(item.id, 'Not right now'))}
        />,
      );
      nodes.push(
        <Button
          key="ask"
          title="Ask"
          variant="secondary"
          onPress={() => {
            setQuestionText('');
            setAskFor(item);
          }}
        />,
      );
      nodes.push(
        <Button
          key="pol"
          title="Policy"
          variant="secondary"
          onPress={() =>
            void run(item.id, async () => {
              const pol = await requestsApi.evaluatePolicy(item.id);
              setError(
                `Policy: ${pol.autoApproved ? 'auto-approve' : pol.isAllowed ? 'allowed' : 'needs approval'}`
                  + (pol.explanation ? ` — ${pol.explanation}` : ''),
              );
            })
          }
        />,
      );
    }

    if (!isQueue && s === 'WaitingForInformation') {
      const openQs = (item.pendingQuestions ?? []).filter((q) => !q.isAnswered);
      for (const q of openQs) {
        nodes.push(
          <Button
            key={`ans-${q.id}`}
            title="Answer"
            onPress={() => {
              setAnswerText('');
              setAnswerFor({ req: item, questionId: q.id, prompt: q.questionText });
            }}
          />,
        );
      }
    }

    if (isQueue && (APPROVABLE.has(s) || s === 'WaitingForInformation' || PLANABLE.has(s))) {
      nodes.push(
        <Button
          key="cond"
          title="Add condition"
          variant="secondary"
          onPress={() =>
            void run(item.id, () =>
              conditionsApi.create({
                name: 'Info needed',
                type: 'Information',
                requestId: item.id,
                description: 'Structured condition blocker',
              }),
            )
          }
        />,
      );
    }

    if (PLANABLE.has(s)) {
      nodes.push(
        <Button key="plan" title="Gen plan" variant="secondary" onPress={() => void openPlan(item)} />,
      );
      if (item.executionPlanId) {
        nodes.push(
          <Button
            key="commit"
            title="Commit plan"
            onPress={() =>
              void run(item.id, () => requestsApi.commitPlan(item.executionPlanId!))
            }
          />,
        );
      }
    }

    return nodes;
  };

  const list = tab === 'mine' ? mine : queue;

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      <SectionTitle>What do you need?</SectionTitle>
      <Text style={[typography.caption, { marginBottom: spacing.md }]}>
        Ride and other types create + submit in one tap (REQUEST → queue).
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
              <Badge label={item.status} tone={statusTone(item.status)} />
              <Badge label={String(item.type)} />
            </View>
            {item.requesterName ? (
              <Text style={typography.caption}>From {item.requesterName}</Text>
            ) : null}
            {(item.pendingQuestions ?? []).filter((q) => !q.isAnswered).map((q) => (
              <Text key={q.id} style={typography.caption}>Q: {q.questionText}</Text>
            ))}
            {item.denialReason ? (
              <Text style={typography.caption}>Denied: {item.denialReason}</Text>
            ) : null}
            <View style={styles.actions}>
              {actionId === item.id ? (
                <ActivityIndicator color={colors.primary} />
              ) : (
                renderActions(item, tab === 'queue')
              )}
            </View>
          </Card>
        )}
      />

      <Modal visible={!!askFor} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>Ask · {askFor?.title}</Text>
            <TextInput
              style={styles.input}
              placeholder="What do you need to know?"
              placeholderTextColor={colors.textMuted}
              value={questionText}
              onChangeText={setQuestionText}
            />
            <View style={styles.modalActions}>
              <Button title="Cancel" variant="secondary" onPress={() => setAskFor(null)} />
              <Button title="Send question" onPress={() => void submitAsk()} />
            </View>
          </View>
        </View>
      </Modal>

      <Modal visible={!!answerFor} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>Answer</Text>
            <Text style={typography.caption}>{answerFor?.prompt}</Text>
            <TextInput
              style={styles.input}
              placeholder="Your answer"
              placeholderTextColor={colors.textMuted}
              value={answerText}
              onChangeText={setAnswerText}
            />
            <View style={styles.modalActions}>
              <Button title="Cancel" variant="secondary" onPress={() => setAnswerFor(null)} />
              <Button title="Submit answer" onPress={() => void submitAnswer()} />
            </View>
          </View>
        </View>
      </Modal>

      <Modal visible={!!plan} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>Plan · {planTitle}</Text>
            <Text style={typography.caption}>Status: {plan?.status}</Text>
            {(plan?.items ?? []).map((i) => (
              <Text key={i.id} style={typography.body}>
                • [{i.type}] {i.title}
              </Text>
            ))}
            <View style={styles.modalActions}>
              <Button title="Close" variant="secondary" onPress={() => setPlan(null)} />
              <Button title="Commit" onPress={() => void commitPlan()} />
            </View>
          </View>
        </View>
      </Modal>
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
  row: { flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' },
  actions: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, marginTop: spacing.sm },
  hint: { ...typography.caption, marginBottom: spacing.md, color: colors.danger },
  modalBackdrop: { flex: 1, backgroundColor: 'rgba(0,0,0,0.5)', justifyContent: 'flex-end' },
  modalCard: {
    backgroundColor: colors.surface, borderTopLeftRadius: 16, borderTopRightRadius: 16,
    padding: spacing.lg, gap: spacing.md, maxHeight: '80%',
  },
  input: {
    borderWidth: 1, borderColor: colors.border, borderRadius: 8, padding: spacing.sm,
    color: colors.text, backgroundColor: colors.bg,
  },
  modalActions: { flexDirection: 'row', justifyContent: 'flex-end', gap: spacing.sm },
});
