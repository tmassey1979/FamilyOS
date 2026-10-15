import { useCallback, useEffect, useState } from 'react';
import { RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useRouter } from 'expo-router';
import { Badge, Button, Card, EmptyState, SectionTitle } from '../../src/components/ui';
import { pulseApi, type PulseDto } from '../../src/api/client';
import { outcomesApi, type OutcomesDto } from '../../src/api/outcomes';
import { mealsApi, type MealEntryDto } from '../../src/api/meals';
import { cacheGet } from '../../src/offline';
import { colors, spacing, typography } from '../../src/theme';

function routeForKind(kind: string): string {
  const k = kind.toLowerCase();
  if (k.includes('request') || k.includes('approval')) return '/(tabs)/requests';
  if (k.includes('task')) return '/(tabs)/tasks';
  if (k.includes('meal')) return '/(tabs)/meals';
  if (k.includes('calendar') || k.includes('event')) return '/(tabs)/calendar';
  if (k.includes('shop') || k.includes('procure') || k.includes('cart') || k.includes('product'))
    return '/(tabs)/family';
  if (k.includes('chat')) return '/(tabs)/chat';
  return '/(tabs)/tasks';
}

function todayUtcDateKey(): string {
  const n = new Date();
  return new Date(Date.UTC(n.getUTCFullYear(), n.getUTCMonth(), n.getUTCDate())).toISOString().slice(0, 10);
}

export default function HomeScreen() {
  const router = useRouter();
  const [pulse, setPulse] = useState<PulseDto | null>(null);
  const [outcomes, setOutcomes] = useState<OutcomesDto | null>(null);
  const [todayMeals, setTodayMeals] = useState<MealEntryDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [fromCache, setFromCache] = useState(false);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    setRefreshing(true);
    setError(null);
    setFromCache(false);
    try {
      const data = await pulseApi.get();
      setPulse(data);
      try {
        setOutcomes(await outcomesApi.get());
      } catch {
        setOutcomes(null);
      }
      try {
        const plan = await mealsApi.week();
        const key = todayUtcDateKey();
        setTodayMeals(plan.entries.filter((e) => e.dayUtc.slice(0, 10) === key));
      } catch {
        setTodayMeals([]);
      }
    } catch (e: unknown) {
      const msg =
        e && typeof e === 'object' && 'error' in e
          ? String((e as { error: string }).error)
          : 'Unable to load Pulse';

      const cached = await cacheGet<PulseDto>('/api/pulse');
      if (cached) {
        setPulse(cached.data);
        setFromCache(true);
        setError(`Offline · last synced ${new Date(cached.savedAtUtc).toLocaleString()}`);
      } else {
        setError(msg);
        setPulse({
          greeting: 'Welcome to Family OS',
          role: 'Owner',
          roleHint: 'You see the full household — approvals, shopping, and blockers.',
          nextAction: {
            kind: 'Task',
            entityId: 'demo',
            title: 'Take out trash',
            subtitle: 'Due today',
            primaryAction: 'Start',
            dueLabel: 'Due today',
          },
          needsAttention: [
            {
              kind: 'Request',
              entityId: 'demo-req',
              title: "Mia's ride request",
              reason: 'Waiting for your review',
              actionLabel: 'Review',
            },
          ],
          waitingBlockers: [
            {
              kind: 'Condition',
              entityId: 'demo-cond',
              title: 'Permission slip',
              reason: 'Pending condition',
              actionLabel: 'Satisfy',
            },
          ],
          comingUp: [],
          household: {
            requestsAwaitingApproval: 2,
            tasksDueToday: 3,
            tasksNeedingAcceptance: 1,
          },
        });
      }
    } finally {
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const go = (kind: string) => router.push(routeForKind(kind) as never);

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={load} tintColor={colors.primary} />}
    >
      <Text style={typography.title}>{pulse?.greeting ?? 'Family OS'}</Text>
      {pulse?.role ? (
        <View style={styles.row}>
          <Badge label={pulse.role} tone={pulse.role === 'Owner' ? 'success' : 'default'} />
          {pulse.roleHint ? <Text style={typography.caption}>{pulse.roleHint}</Text> : null}
        </View>
      ) : null}
      {error ? (
        <Text style={styles.hint}>
          {error}
          {fromCache ? '' : ' · showing demo shell'}
        </Text>
      ) : null}

      <SectionTitle>Next action</SectionTitle>
      {pulse?.nextAction ? (
        <Card style={styles.cardGap}>
          <Text style={typography.heading}>{pulse.nextAction.title}</Text>
          {pulse.nextAction.subtitle ? (
            <Text style={typography.caption}>{pulse.nextAction.subtitle}</Text>
          ) : null}
          <View style={styles.row}>
            <Badge label={pulse.nextAction.kind} />
            {pulse.nextAction.dueLabel ? <Badge label={pulse.nextAction.dueLabel} tone="warning" /> : null}
          </View>
          <Button title={pulse.nextAction.primaryAction} onPress={() => go(pulse.nextAction!.kind)} />
        </Card>
      ) : (
        <EmptyState title="You're clear" subtitle="No urgent next action right now." />
      )}

      <SectionTitle>Today's meals</SectionTitle>
      {todayMeals.length > 0 ? (
        <Card style={styles.cardGap}>
          {todayMeals.map((m) => (
            <View key={m.id} style={styles.row}>
              <Badge label={m.slot} />
              <Text style={typography.body}>{m.dishName}</Text>
            </View>
          ))}
          <Button title="Open meal plan" variant="secondary" onPress={() => router.push('/(tabs)/meals')} />
        </Card>
      ) : (
        <EmptyState
          title="Nothing planned for today"
          subtitle="Add dishes on the Meals tab."
        />
      )}

      <SectionTitle>Needs your attention</SectionTitle>
      {pulse?.needsAttention?.length ? (
        pulse.needsAttention.map((item) => (
          <Card key={`att-${item.entityId}`} style={styles.cardGap}>
            <Text style={typography.heading}>{item.title}</Text>
            <Text style={typography.caption}>{item.reason}</Text>
            <View style={styles.row}>
              <Badge label={item.kind} />
            </View>
            <Button title={item.actionLabel} variant="secondary" onPress={() => go(item.kind)} />
          </Card>
        ))
      ) : (
        <EmptyState title="Nothing waiting" />
      )}

      <SectionTitle>Waiting / blockers</SectionTitle>
      {pulse?.waitingBlockers?.length ? (
        pulse.waitingBlockers.map((item) => (
          <Card key={`blk-${item.entityId}-${item.kind}`} style={styles.cardGap}>
            <Text style={typography.heading}>{item.title}</Text>
            <Text style={typography.caption}>{item.reason}</Text>
            <View style={styles.row}>
              <Badge label={item.kind} tone="warning" />
            </View>
            <Button title={item.actionLabel} variant="secondary" onPress={() => go(item.kind)} />
          </Card>
        ))
      ) : (
        <EmptyState title="No blockers" subtitle="Nothing is waiting on you right now." />
      )}

      {(pulse?.comingUp?.length ?? 0) > 0 ? (
        <>
          <SectionTitle>Coming up</SectionTitle>
          {pulse!.comingUp.map((item) => (
            <Card key={`up-${item.entityId}`} style={styles.cardGap}>
              <Text style={typography.heading}>{item.title}</Text>
              <Text style={typography.caption}>
                {new Date(item.whenUtc).toLocaleString()}
                {item.location ? ` · ${item.location}` : ''}
              </Text>
              <Badge label={item.kind} />
            </Card>
          ))}
        </>
      ) : null}

      <SectionTitle>Household</SectionTitle>
      <Card style={styles.cardGap}>
        {(pulse?.role === 'Owner' || pulse?.role === 'Adult' || !pulse?.role) ? (
          <Text style={typography.body}>
            {pulse?.household.requestsAwaitingApproval ?? 0} requests awaiting approval
          </Text>
        ) : null}
        <Text style={typography.body}>{pulse?.household.tasksDueToday ?? 0} tasks due today</Text>
        {(pulse?.role === 'Owner' || pulse?.role === 'Adult' || !pulse?.role) ? (
          <Text style={typography.body}>
            {pulse?.household.tasksNeedingAcceptance ?? 0} tasks need acceptance
          </Text>
        ) : null}
        {pulse?.procurement ? (
          <Text style={typography.body}>
            Shopping: {pulse.procurement.approvedItems} items
            {pulse.procurement.primaryStore ? ` · ${pulse.procurement.primaryStore}` : ''}
          </Text>
        ) : null}
      </Card>

      {outcomes ? (
        <>
          <SectionTitle>Outcomes</SectionTitle>
          <Card style={styles.cardGap}>
            <Text style={typography.body}>
              Tasks done · 7d {outcomes.tasksCompletedLast7Days} · 30d {outcomes.tasksCompletedLast30Days}
            </Text>
            <Text style={typography.body}>
              Requests · approved {outcomes.requestsApprovedLast7Days} · denied{' '}
              {outcomes.requestsDeniedLast7Days} (7d)
            </Text>
            <Text style={typography.body}>
              Shopping purchased (7d) · {outcomes.shoppingItemsPurchasedLast7Days}
            </Text>
            {outcomes.avgTaskCompletionMinutes != null ? (
              <Text style={typography.caption}>
                Avg task time · {Math.round(outcomes.avgTaskCompletionMinutes)} min
              </Text>
            ) : null}
            <Text style={typography.caption}>
              Open tasks {outcomes.openTasks} · pending requests {outcomes.pendingRequests} · queue{' '}
              {outcomes.queuedShoppingItems}
            </Text>
          </Card>
        </>
      ) : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  content: { padding: spacing.md, paddingBottom: spacing.xl * 2 },
  cardGap: { marginBottom: spacing.md, gap: spacing.sm },
  row: { flexDirection: 'row', gap: spacing.sm, marginVertical: spacing.sm, flexWrap: 'wrap', alignItems: 'center' },
  hint: { ...typography.caption, marginBottom: spacing.md },
});
