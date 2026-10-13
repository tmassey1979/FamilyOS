import { useCallback, useEffect, useState } from 'react';
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
  conditionsApi,
  requestsApi,
  type ConditionDto,
  type RequestDto,
  type RequestHistoryDto,
  type RequestTypeDto,
} from '../../src/api/client';
import { colors, spacing, typography } from '../../src/theme';

type Tab = 'mine' | 'queue';

const OPEN = new Set(['Draft', 'Submitted', 'UnderReview', 'WaitingForInformation']);
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
  const [editDraft, setEditDraft] = useState<RequestDto | null>(null);
  const [editTitle, setEditTitle] = useState('');
  const [editAmount, setEditAmount] = useState('');
  const [trailFor, setTrailFor] = useState<RequestDto | null>(null);
  const [trail, setTrail] = useState<RequestHistoryDto[]>([]);
  const [conditionsByRequest, setConditionsByRequest] = useState<Record<string, ConditionDto[]>>({});

  const [moneyFor, setMoneyFor] = useState<RequestTypeDto | null>(null);
  const [moneyAmount, setMoneyAmount] = useState('');
  const [moneyPurpose, setMoneyPurpose] = useState('');
  const [moneySaving, setMoneySaving] = useState(false);

  const [composeFor, setComposeFor] = useState<RequestTypeDto | null>(null);
  const [composeTitle, setComposeTitle] = useState('');
  const [composeItems, setComposeItems] = useState('');
  const [composeStore, setComposeStore] = useState('');
  const [composeWhere, setComposeWhere] = useState('');
  const [composeWhen, setComposeWhen] = useState('');
  const [composeAmount, setComposeAmount] = useState('');
  const [composeSaving, setComposeSaving] = useState(false);

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
        const entries = await Promise.all(
          conditionalIds.map(async (id) => {
            try {
              const list = await conditionsApi.byRequest(id);
              return [id, list] as const;
            } catch {
              return [id, [] as ConditionDto[]] as const;
            }
          }),
        );
        setConditionsByRequest(Object.fromEntries(entries));
      } else {
        setConditionsByRequest({});
      }
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Unable to load requests');
    } finally {
      setBusy(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const startCreate = async (type: RequestTypeDto) => {
    const code = type.code;
    if (code === 'Money' || code.toLowerCase() === 'money') {
      setMoneyAmount('');
      setMoneyPurpose('');
      setMoneyFor(type);
      return;
    }
    const lower = code.toLowerCase();
    if (
      lower === 'grocery' ||
      lower === 'purchase' ||
      lower === 'snackfood' ||
      lower === 'snack' ||
      lower === 'ride' ||
      lower === 'school' ||
      lower === 'medical'
    ) {
      setComposeTitle('');
      setComposeItems('');
      setComposeStore('');
      setComposeWhere('');
      setComposeWhen('');
      setComposeAmount('');
      setComposeFor(type);
      return;
    }
    setCreating(code);
    try {
      await requestsApi.create({
        type: code,
        title: type.name,
        answers: {},
      });
      await load();
      setTab('mine');
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Create failed');
    } finally {
      setCreating(null);
    }
  };

  const submitCompose = async () => {
    if (!composeFor) return;
    const code = composeFor.code;
    const lower = code.toLowerCase();
    setComposeSaving(true);
    setError(null);
    try {
      const answers: Record<string, unknown> = {};
      let title = composeTitle.trim();
      let amount: number | undefined;
      if (lower === 'ride' || lower === 'school' || lower === 'medical') {
        if (composeWhere.trim()) answers.where = composeWhere.trim();
        if (composeWhere.trim()) answers.location = composeWhere.trim();
        if (composeWhen.trim()) answers.when = composeWhen.trim();
        title = title || (lower === 'ride' ? 'Ride needed' : composeFor.name);
      } else {
        if (composeItems.trim()) answers.items = composeItems.trim();
        if (composeStore.trim()) {
          answers.store = composeStore.trim();
          answers.preferredStore = composeStore.trim();
        }
        const amt = parseFloat(composeAmount);
        if (Number.isFinite(amt) && amt >= 0) {
          amount = amt;
          answers.amount = amt;
        }
        title = title || composeItems.trim().split(/[,;\n]/)[0]?.trim() || composeFor.name;
      }
      await requestsApi.create({
        type: code,
        title,
        amount,
        answers,
      });
      setComposeFor(null);
      await load();
      setTab('mine');
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Create failed');
    } finally {
      setComposeSaving(false);
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

  const openTrail = async (req: RequestDto) => {
    setTrailFor(req);
    setTrail([]);
    try {
      setTrail(await requestsApi.history(req.id));
    } catch {
      setTrail([]);
    }
  };

  const list = tab === 'mine' ? mine : queue;

  const renderCard = (item: RequestDto) => {
    const s = item.status;
    const busyRow = actionId === item.id;
    const nodes: React.ReactNode[] = [];

    if (s === 'Draft' && tab === 'mine') {
      nodes.push(
        <Button
          key="edit"
          title="Edit"
          variant="secondary"
          onPress={() => {
            setEditTitle(item.title);
            setEditAmount(item.amount != null ? String(item.amount) : '');
            setEditDraft(item);
          }}
        />,
        <Button
          key="submit"
          title="Submit"
          onPress={() => void run(item.id, () => requestsApi.submit(item.id))}
        />,
        <Button
          key="withdraw"
          title="Withdraw"
          variant="secondary"
          onPress={() => void run(item.id, () => requestsApi.cancel(item.id, 'Withdrawn'))}
        />,
      );
    }

    if (OPEN.has(s) && s !== 'Draft' && tab === 'mine') {
      nodes.push(
        <Button
          key="cancel"
          title="Withdraw"
          variant="secondary"
          onPress={() => void run(item.id, () => requestsApi.cancel(item.id))}
        />,
      );
    }

    if (tab === 'queue' && (s === 'Submitted' || s === 'UnderReview' || s === 'WaitingForInformation')) {
      nodes.push(
        <Button
          key="approve"
          title="Approve"
          onPress={() => void run(item.id, () => requestsApi.approve(item.id, false))}
        />,
        <Button
          key="cond"
          title="Conditional"
          variant="secondary"
          onPress={() => void run(item.id, () => requestsApi.approve(item.id, true))}
        />,
        <Button
          key="deny"
          title="Deny"
          variant="secondary"
          onPress={() => void run(item.id, () => requestsApi.deny(item.id, 'Not approved'))}
        />,
        <Button key="ask" title="Ask" variant="secondary" onPress={() => setAskFor(item)} />,
        <Button
          key="override"
          title="Override✓"
          variant="secondary"
          onPress={() =>
            void run(item.id, () => requestsApi.overridePolicy(item.id, { approve: true, reason: 'Owner override' }))
          }
        />,
      );
    }

    if (s === 'ConditionallyApproved') {
      const conds = conditionsByRequest[item.id] ?? [];
      const pending = conds.filter((c) => c.status === 'Pending');
      nodes.push(
        <Text key="cond-label" style={typography.caption}>
          Conditions: {pending.length} pending
        </Text>,
      );
      pending.forEach((c) => {
        nodes.push(
          <Button
            key={`sat-${c.id}`}
            title={`Satisfy: ${c.name}`}
            variant="secondary"
            onPress={() => void run(item.id, () => conditionsApi.satisfy(c.id))}
          />,
        );
      });
      nodes.push(
        <Button
          key="add-cond"
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
        <Button
          key="plan"
          title="Gen plan"
          variant="secondary"
          onPress={() => void run(item.id, () => requestsApi.generatePlan(item.id))}
        />,
      );
    }

    nodes.push(
      <Button key="trail" title="Trail" variant="secondary" onPress={() => void openTrail(item)} />,
    );

    const pendingQs = (item.pendingQuestions ?? []).filter((q) => !q.isAnswered);

    return (
      <Card key={item.id} style={styles.card}>
        <View style={styles.rowBetween}>
          <Text style={typography.heading}>{item.title}</Text>
          <Badge
            label={item.status}
            tone={
              item.status === 'Approved'
                ? 'success'
                : item.status === 'Denied' || item.status === 'Cancelled'
                  ? 'danger'
                  : 'warning'
            }
          />
        </View>
        <Text style={typography.caption}>
          {item.type} · {item.requesterName}
          {item.amount != null ? ` · $${item.amount}` : ''}
        </Text>
        {pendingQs.length > 0 ? (
          <View style={{ gap: 4 }}>
            {pendingQs.map((q) => (
              <Pressable
                key={q.id}
                onPress={() => {
                  setAnswerText('');
                  setAnswerFor({ req: item, questionId: q.id, prompt: q.questionText });
                }}
              >
                <Text style={[typography.caption, { color: colors.primary }]}>Q: {q.questionText}</Text>
              </Pressable>
            ))}
          </View>
        ) : null}
        <View style={styles.row}>{busyRow ? <ActivityIndicator color={colors.primary} /> : nodes}</View>
      </Card>
    );
  };

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={busy} onRefresh={load} tintColor={colors.primary} />}
    >
      <SectionTitle>New request</SectionTitle>
      <View style={styles.chips}>
        {types.map((t) => (
          <Pressable key={t.id} onPress={() => void startCreate(t)} disabled={!!creating}>
            <Card style={styles.chip}>
              <Text style={typography.body}>{creating === t.code ? '…' : t.name}</Text>
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
      {busy && list.length === 0 ? <ActivityIndicator color={colors.primary} /> : null}
      {list.length === 0 && !busy ? (
        <EmptyState title="Nothing here" subtitle="Create a request or wait for the queue." />
      ) : (
        list.map(renderCard)
      )}

      <Modal visible={!!composeFor} transparent animationType="slide">
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>{composeFor?.name ?? 'New request'}</Text>
            <Text style={typography.caption}>
              {(composeFor?.code ?? '').toLowerCase() === 'ride' ||
              (composeFor?.code ?? '').toLowerCase() === 'school' ||
              (composeFor?.code ?? '').toLowerCase() === 'medical'
                ? 'Where and when — becomes a calendar event on approve'
                : 'Items go on the shopping list when approved'}
            </Text>
            <TextInput
              style={styles.input}
              placeholder="Title (optional)"
              placeholderTextColor={colors.textMuted}
              value={composeTitle}
              onChangeText={setComposeTitle}
            />
            {(composeFor?.code ?? '').toLowerCase() === 'ride' ||
            (composeFor?.code ?? '').toLowerCase() === 'school' ||
            (composeFor?.code ?? '').toLowerCase() === 'medical' ? (
              <>
                <TextInput
                  style={styles.input}
                  placeholder="Where / location"
                  placeholderTextColor={colors.textMuted}
                  value={composeWhere}
                  onChangeText={setComposeWhere}
                />
                <TextInput
                  style={styles.input}
                  placeholder="When (e.g. Tomorrow 3pm)"
                  placeholderTextColor={colors.textMuted}
                  value={composeWhen}
                  onChangeText={setComposeWhen}
                />
              </>
            ) : (
              <>
                <TextInput
                  style={styles.input}
                  placeholder="Items (comma-separated)"
                  placeholderTextColor={colors.textMuted}
                  value={composeItems}
                  onChangeText={setComposeItems}
                />
                <TextInput
                  style={styles.input}
                  placeholder="Preferred store (optional)"
                  placeholderTextColor={colors.textMuted}
                  value={composeStore}
                  onChangeText={setComposeStore}
                />
                <TextInput
                  style={styles.input}
                  placeholder="Est. total (optional)"
                  placeholderTextColor={colors.textMuted}
                  keyboardType="decimal-pad"
                  value={composeAmount}
                  onChangeText={setComposeAmount}
                />
              </>
            )}
            <View style={styles.modalActions}>
              <Button title="Cancel" variant="secondary" onPress={() => setComposeFor(null)} />
              <Button
                title={composeSaving ? 'Saving…' : 'Create draft'}
                onPress={() => void submitCompose()}
              />
            </View>
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
              <Button title={moneySaving ? 'Saving…' : 'Create draft'} onPress={() => void submitMoney()} />
            </View>
          </View>
        </View>
      </Modal>

      <Modal visible={!!askFor} transparent animationType="slide">
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>Ask a question</Text>
            <TextInput
              style={styles.input}
              placeholder="Question"
              placeholderTextColor={colors.textMuted}
              value={questionText}
              onChangeText={setQuestionText}
            />
            <View style={styles.modalActions}>
              <Button title="Cancel" variant="secondary" onPress={() => setAskFor(null)} />
              <Button
                title="Send"
                onPress={() => {
                  if (!askFor || !questionText.trim()) return;
                  void run(askFor.id, () => requestsApi.askQuestion(askFor.id, questionText.trim())).then(() => {
                    setAskFor(null);
                    setQuestionText('');
                  });
                }}
              />
            </View>
          </View>
        </View>
      </Modal>

      <Modal visible={!!answerFor} transparent animationType="slide">
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
              <Button
                title="Send"
                onPress={() => {
                  if (!answerFor || !answerText.trim()) return;
                  void run(answerFor.req.id, () =>
                    requestsApi.answer(answerFor.req.id, answerFor.questionId, answerText.trim()),
                  ).then(() => setAnswerFor(null));
                }}
              />
            </View>
          </View>
        </View>
      </Modal>

      <Modal visible={!!editDraft} transparent animationType="slide">
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>Edit draft</Text>
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
              keyboardType="decimal-pad"
              value={editAmount}
              onChangeText={setEditAmount}
            />
            <View style={styles.modalActions}>
              <Button title="Cancel" variant="secondary" onPress={() => setEditDraft(null)} />
              <Button
                title="Save"
                onPress={() => {
                  if (!editDraft) return;
                  const amt = parseFloat(editAmount);
                  void run(editDraft.id, () =>
                    requestsApi.updateDraft(editDraft.id, {
                      title: editTitle.trim() || editDraft.title,
                      amount: Number.isFinite(amt) ? amt : undefined,
                    }),
                  ).then(() => setEditDraft(null));
                }}
              />
            </View>
          </View>
        </View>
      </Modal>

      <Modal visible={!!trailFor} transparent animationType="slide">
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>Decision trail</Text>
            <Text style={typography.caption}>{trailFor?.title}</Text>
            <ScrollView style={{ maxHeight: 320 }}>
              {trail.map((h) => (
                <View key={h.id} style={{ marginBottom: spacing.sm }}>
                  <Text style={typography.body}>{h.status}</Text>
                  <Text style={typography.caption}>{h.detail}</Text>
                  <Text style={typography.caption}>{new Date(h.timestampUtc).toLocaleString()}</Text>
                </View>
              ))}
              {trail.length === 0 ? <Text style={typography.caption}>No history yet</Text> : null}
            </ScrollView>
            <Button title="Close" variant="secondary" onPress={() => setTrailFor(null)} />
          </View>
        </View>
      </Modal>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  content: { padding: spacing.md, paddingBottom: spacing.xl * 2 },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, marginBottom: spacing.md },
  chip: { paddingVertical: spacing.xs, paddingHorizontal: spacing.sm },
  tabs: { flexDirection: 'row', gap: spacing.sm, marginBottom: spacing.md },
  tab: { paddingVertical: spacing.xs, paddingHorizontal: spacing.sm, borderRadius: 8, backgroundColor: colors.surface },
  tabActive: { backgroundColor: colors.primary },
  card: { marginBottom: spacing.sm, gap: spacing.xs },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs, alignItems: 'center' },
  rowBetween: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', gap: spacing.sm },
  hint: { ...typography.caption, marginBottom: spacing.md, color: colors.danger },
  modalBackdrop: { flex: 1, backgroundColor: 'rgba(0,0,0,0.5)', justifyContent: 'flex-end' },
  modalCard: {
    backgroundColor: colors.surface,
    borderTopLeftRadius: 16,
    borderTopRightRadius: 16,
    padding: spacing.lg,
    gap: spacing.md,
  },
  input: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 8,
    padding: spacing.sm,
    color: colors.text,
    backgroundColor: colors.bg,
  },
  modalActions: { flexDirection: 'row', justifyContent: 'flex-end', gap: spacing.sm },
});
