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
  type RequestHistoryDto,
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

  const [askFor, setAskFor] = useState<RequestDto | null>(null);
  const [questionText, setQuestionText] = useState('');
  const [answerFor, setAnswerFor] = useState<{ req: RequestDto; questionId: string; prompt: string } | null>(null);
  const [answerText, setAnswerText] = useState('');
  const [plan, setPlan] = useState<ExecutionPlanDto | null>(null);
  const [planTitle, setPlanTitle] = useState('');
  const [condsByReq, setCondsByReq] = useState<Record<string, ConditionDto[]>>({});
  const [historyOpen, setHistoryOpen] = useState(false);
  const [historyTitle, setHistoryTitle] = useState('');
  const [history, setHistory] = useState<RequestHistoryDto[]>([]);
  const [editFor, setEditFor] = useState<RequestDto | null>(null);
  const [editTitle, setEditTitle] = useState('');
  const [editAmount, setEditAmount] = useState('');
  const [editSaving, setEditSaving] = useState(false);
  /** #22 Money request create sheet */
  const [moneyFor, setMoneyFor] = useState<RequestTypeDto | null>(null);
  const [moneyAmount, setMoneyAmount] = useState('');
  const [moneyPurpose, setMoneyPurpose] = useState('');
  const [moneySaving, setMoneySaving] = useState(false);

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
      const conditionalIds = [...m, ...q]
        .filter((r) => r.status === 'ConditionallyApproved')
        .map((r) => r.id);
      if (conditionalIds.length > 0) {
        const pairs = await Promise.all(
          conditionalIds.map(async (id) => {
            try {
              const list = await conditionsApi.byRequest(id);
              return [id, list] as const;
            } catch {
              return [id, [] as ConditionDto[]] as const;
            }
          }),
        );
        const map: Record<string, ConditionDto[]> = {};
        for (const [id, list] of pairs) map[id] = list;
        setCondsByReq(map);
      } else {
        setCondsByReq({});
      }
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
    const code = type.code;
    // #22 Money — collect amount + purpose before draft
    if (code === 'Money' || code.toLowerCase() === 'money') {
      setMoneyAmount('');
      setMoneyPurpose('');
      setMoneyFor(type);
      return;
    }
    setCreating(code);
    try {
      const answers: Record<string, unknown> = {};
      if (code === 'Ride' || code.toLowerCase() === 'ride') {
        answers.where = 'School';
        answers.when = 'Today after practice';
      }
      await requestsApi.create({
        type: code,
        title: code === 'Ride' || code.toLowerCase() === 'ride' ? 'Ride needed' : type.name,
        answers,
      });
      await load();
      setTab('mine');
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Create failed');
    } finally {
      setCreating(null);
    }
  };

  const submitMoney = async () => {
    if (!moneyFor) return;
    const amt = parseFloat(moneyAmount);
    if (!Number.isFinite(amt) || amt <= 0) {
      setError('Enter a valid amount greater than 0');
      return;
    }
    const purpose = moneyPurpose.trim() || 'Money request';
    setMoneySaving(true);
    setError(null);
    try {
      await requestsApi.create({
        type: moneyFor.code,
        title: purpose,
        amount: amt,
        answers: { amount: amt, purpose },
      });
      setMoneyFor(null);
      await load();
      setTab('mine');
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Create failed');
    } finally {
      setMoneySaving(false);
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

  const openEdit = (item: RequestDto) => {
    setEditFor(item);
    setEditTitle(item.title);
    setEditAmount(item.amount != null ? String(item.amount) : '');
  };

  const saveEdit = async () => {
    if (!editFor || !editTitle.trim()) return;
    setEditSaving(true);
    try {
      const amt = parseFloat(editAmount);
      await requestsApi.updateDraft(editFor.id, {
        title: editTitle.trim(),
        amount: Number.isFinite(amt) ? amt : undefined,
      });
      setEditFor(null);
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Save failed');
    } finally {
      setEditSaving(false);
    }
  };

  const openHistory = async (item: RequestDto) => {
    setHistoryTitle(item.title);
    setHistoryOpen(true);
    setHistory([]);
    try {
      setHistory(await requestsApi.history(item.id));
    } catch {
      setHistory([]);
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
              const label = pol.autoApproved
                ? 'auto-approve'
                : pol.requiresOwner
                  ? 'requires owner'
                  : pol.isAllowed
                    ? 'allowed'
                    : 'needs approval';
              setError(
                `Policy: ${label}` + (pol.explanation ? ` — ${pol.explanation}` : ''),
              );
            })
          }
        />,
      );
      nodes.push(
        <Button
          key="ov-ap"
          title="Override✓"
          variant="secondary"
          onPress={() =>
            void run(item.id, () =>
              requestsApi.overridePolicy(item.id, {
                approve: true,
                reason: 'Owner override from app',
              }),
            )
          }
        />,
      );
      nodes.push(
        <Button
          key="ov-dn"
          title="Override✗"
          variant="secondary"
          onPress={() =>
            void run(item.id, () =>
              requestsApi.overridePolicy(item.id, {
                approve: false,
                reason: 'Owner override deny from app',
              }),
            )
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

    if (!isQueue && s === 'Draft') {
      nodes.push(
        <Button key="edit" title="Edit" variant="secondary" onPress={() => openEdit(item)} />,
      );
      nodes.push(
        <Button
          key="submit"
          title="Submit"
          onPress={() => void run(item.id, () => requestsApi.submit(item.id))}
        />,
      );
    }

    if (
      !isQueue &&
      (s === 'Draft' || s === 'Submitted' || s === 'WaitingForInformation' || s === 'UnderReview')
    ) {
      nodes.push(
        <Button
          key="cancel"
          title="Withdraw"
          variant="secondary"
          onPress={() => void run(item.id, () => requestsApi.cancel(item.id))}
        />,
      );
    }

    if (isQueue && (APPROVABLE.has(s) || s === 'WaitingForInformation' || PLANABLE.has(s))) {
      nodes.push(
        <Button
          key="addcond"
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

    nodes.push(
      <Button
        key="hist"
        title="Trail"
        variant="secondary"
        onPress={() => void openHistory(item)}
      />,
    );

    return nodes;
  };

  const list = tab === 'mine' ? mine : queue;

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      <SectionTitle>What do you need?</SectionTitle>
      <Text style={[typography.caption, { marginBottom: spacing.md }]}>
        Tap a type to create a Draft — Edit details, then Submit to the queue.
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
            {item.amount != null ? (
              <Text style={[typography.heading, { color: colors.primary }]}>
                ${Number(item.amount).toFixed(2)}
              </Text>
            ) : null}
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
            {(condsByReq[item.id] ?? [])
              .filter((c) => c.status === 'Pending')
              .map((c) => (
                <View key={c.id} style={styles.row}>
                  <Badge label={`Condition: ${c.name}`} tone="warning" />
                  <Button
                    title="Satisfy"
                    variant="secondary"
                    onPress={() => void run(item.id, () => conditionsApi.satisfy(c.id))}
                  />
                </View>
              ))}
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

      <Modal visible={!!editFor} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>Edit draft · {editFor?.type}</Text>
            <TextInput
              style={styles.input}
              placeholder="Title"
              placeholderTextColor={colors.textMuted}
              value={editTitle}
              onChangeText={setEditTitle}
            />
            <TextInput
              style={styles.input}
              placeholder="Amount (optional)"
              placeholderTextColor={colors.textMuted}
              value={editAmount}
              onChangeText={setEditAmount}
              keyboardType="decimal-pad"
            />
            <View style={styles.modalActions}>
              <Button title="Cancel" variant="secondary" onPress={() => setEditFor(null)} />
              <Button title={editSaving ? 'Saving…' : 'Save'} onPress={() => void saveEdit()} />
            </View>
          </View>
        </View>
      </Modal>

      <Modal visible={historyOpen} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>Decision trail · {historyTitle}</Text>
            {history.length === 0 ? (
              <Text style={typography.caption}>No history yet.</Text>
            ) : (
              history.map((h) => (
                <View key={h.id} style={{ marginBottom: spacing.sm }}>
                  <Text style={typography.body}>{h.status}</Text>
                  <Text style={typography.caption}>{h.detail}</Text>
                  <Text style={typography.caption}>{new Date(h.timestampUtc).toLocaleString()}</Text>
                </View>
              ))
            )}
            <Button title="Close" onPress={() => setHistoryOpen(false)} />
          </View>
        </View>
      </Modal>

      <Modal visible={!!moneyFor} transparent animationType="slide">
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>Request money</Text>
            <Text style={typography.caption}>Amount and what it is for</Text>
            <TextInput
              style={styles.input}
              placeholder="Amount (e.g. 20)"
              placeholderTextColor={colors.textMuted}
              keyboardType="decimal-pad"
              value={moneyAmount}
              onChangeText={setMoneyAmount}
            />
            <TextInput
              style={styles.input}
              placeholder="Purpose (e.g. Lunch money)"
              placeholderTextColor={colors.textMuted}
              value={moneyPurpose}
              onChangeText={setMoneyPurpose}
            />
            <View style={styles.modalActions}>
              <Button title="Cancel" variant="secondary" onPress={() => setMoneyFor(null)} />
              <Button
                title={moneySaving ? 'Saving…' : 'Create draft'}
                onPress={() => void submitMoney()}
              />
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
