import { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { useRouter } from 'expo-router';
import { Badge, Button, Card, EmptyState, SectionTitle } from '../../src/components/ui';
import { pulseApi, type PulseDto } from '../../src/api/client';
import { outcomesApi, type OutcomesDto } from '../../src/api/outcomes';
import { mealsApi, type MealEntryDto } from '../../src/api/meals';
import { cacheGet } from '../../src/offline';
import { colors, spacing, typography } from '../../src/theme';

function mondayOf(d: Date): Date {
  const x = new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate()));
  const day = x.getUTCDay();
  const diff = day === 0 ? -6 : 1 - day;
  x.setUTCDate(x.getUTCDate() + diff);
  x.setUTCHours(0, 0, 0, 0);
  return x;
}

export default function HomeScreen() {
  const router = useRouter();
  const [pulse, setPulse] = useState<PulseDto | null>(null);
  const [outcomes, setOutcomes] = useState<OutcomesDto | null>(null);
  const [todayMeals, setTodayMeals] = useState<MealEntryDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    setRefreshing(true);
    setError(null);
    try {
      const cached = await cacheGet<PulseDto>('pulse');
      if (cached) setPulse(cached);
      const [p, o] = await Promise.all([
        pulseApi.get(),
        outcomesApi.get().catch(() => null),
      ]);
      setPulse(p);
      setOutcomes(o);
      try {
        const week = await mealsApi.week(mondayOf(new Date()).toISOString());
        const today = new Date().toISOString().slice(0, 10);
        setTodayMeals(week.entries.filter((e) => e.dayUtc.slice(0, 10) === today));
      } catch {
        setTodayMeals([]);
      }
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Unable to load pulse');
    } finally {
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const quiet =
    !pulse ||
    ((pulse.myTasks?.length ?? 0) === 0 &&
      (pulse.pendingApprovals?.length ?? 0) === 0 &&
      (pulse.waitingBlockers?.length ?? 0) === 0 &&
      (pulse.comingUp?.length ?? 0) === 0);

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={load} tintColor={colors.primary} />}
    >
      <Text style={typography.title}>Family Pulse</Text>
      {pulse?.role ? (
        <Text style={typography.caption}>
          {pulse.role}
          {pulse.roleHint ? ` · ${pulse.roleHint}` : ''}
        </Text>
      ) : null}
      {error ? <Text style={styles.error}>{error}</Text> : null}
      {refreshing && !pulse ? <ActivityIndicator color={colors.primary} /> : null}

      {quiet && !refreshing ? (
        <Card style={styles.cardGap}>
          <Text style={typography.heading}>Getting started</Text>
          <Text style={typography.caption}>
            Invite family from Settings, add a task, or plan meals for the week.
          </Text>
          <View style={styles.row}>
            <Button title="Settings" variant="secondary" onPress={() => router.push('/(tabs)/settings')} />
            <Button title="Meals" variant="secondary" onPress={() => router.push('/(tabs)/meals')} />
            <Button title="Tasks" onPress={() => router.push('/(tabs)/tasks')} />
          </View>
        </Card>
      ) : null}

      {todayMeals.length > 0 ? (
        <>
          <SectionTitle>Today's meals</SectionTitle>
          <Card style={styles.cardGap}>
            {todayMeals.map((m) => (
              <Text key={m.id} style={typography.body}>
                {m.slot}: {m.dishName}
              </Text>
            ))}
            <Button title="Meal plan" variant="secondary" onPress={() => router.push('/(tabs)/meals')} />
          </Card>
        </>
      ) : null}

      {(pulse?.waitingBlockers?.length ?? 0) > 0 ? (
        <>
          <SectionTitle>Waiting / blockers</SectionTitle>
          {pulse!.waitingBlockers!.map((b, i) => (
            <Card key={`${b.kind}-${b.id ?? i}`} style={styles.cardGap}>
              <Text style={typography.heading}>{b.title}</Text>
              {b.detail ? <Text style={typography.caption}>{b.detail}</Text> : null}
              <Badge label={b.kind} tone="warning" />
            </Card>
          ))}
        </>
      ) : null}

      {(pulse?.pendingApprovals?.length ?? 0) > 0 ? (
        <>
          <SectionTitle>Needs your decision</SectionTitle>
          {pulse!.pendingApprovals!.map((a) => (
            <Pressable key={a.id} onPress={() => router.push('/(tabs)/requests')}>
              <Card style={styles.cardGap}>
                <Text style={typography.heading}>{a.title}</Text>
                <Text style={typography.caption}>{a.status}</Text>
              </Card>
            </Pressable>
          ))}
        </>
      ) : null}

      {(pulse?.myTasks?.length ?? 0) > 0 ? (
        <>
          <SectionTitle>My tasks</SectionTitle>
          {pulse!.myTasks!.map((t) => (
            <Pressable key={t.id} onPress={() => router.push('/(tabs)/tasks')}>
              <Card style={styles.cardGap}>
                <Text style={typography.heading}>{t.title}</Text>
                <View style={styles.row}>
                  <Badge label={t.status} />
                  {t.isOverdue ? <Badge label="Overdue" tone="danger" /> : null}
                </View>
              </Card>
            </Pressable>
          ))}
        </>
      ) : null}

      {(pulse?.comingUp?.length ?? 0) > 0 ? (
        <>
          <SectionTitle>Coming up</SectionTitle>
          {pulse!.comingUp!.map((c, i) => (
            <Card key={`${c.title}-${i}`} style={styles.cardGap}>
              <Text style={typography.body}>{c.title}</Text>
              {c.when ? <Text style={typography.caption}>{c.when}</Text> : null}
            </Card>
          ))}
        </>
      ) : null}

      {outcomes ? (
        <>
          <SectionTitle>Outcomes</SectionTitle>
          <Card style={styles.cardGap}>
            <Text style={typography.caption}>Last 7 / 30 days</Text>
            {outcomes.tasksCompletedLast7Days === 0 &&
            outcomes.requestsApprovedLast7Days === 0 &&
            outcomes.shoppingItemsPurchasedLast7Days === 0 ? (
              <Text style={typography.caption}>
                Quiet week so far — complete a task or approve a request to build history.
              </Text>
            ) : null}
            <Text style={typography.body}>
              Tasks done: {outcomes.tasksCompletedLast7Days} · {outcomes.tasksCompletedLast30Days}
            </Text>
            <Text style={typography.body}>
              Requests ✓ {outcomes.requestsApprovedLast7Days} · ✗ {outcomes.requestsDeniedLast7Days}
              {outcomes.approvalRateLast7Days != null
                ? ` · ${outcomes.approvalRateLast7Days}% approved`
                : ''}
            </Text>
            <Text style={typography.body}>
              Requests 30d ✓ {outcomes.requestsApprovedLast30Days ?? 0} · ✗{' '}
              {outcomes.requestsDeniedLast30Days ?? 0}
            </Text>
            <Text style={typography.body}>
              Shopping purchased (7d): {outcomes.shoppingItemsPurchasedLast7Days}
            </Text>
            {outcomes.avgTaskCompletionMinutes != null ? (
              <Text style={typography.body}>
                Avg task time: {Math.round(outcomes.avgTaskCompletionMinutes)} min
              </Text>
            ) : null}
            <Text style={typography.caption}>
              Open now — tasks {outcomes.openTasks} · requests {outcomes.pendingRequests} · queue{' '}
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
  cardGap: { marginBottom: spacing.md, gap: spacing.xs },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, alignItems: 'center' },
  error: { ...typography.caption, color: colors.danger, marginBottom: spacing.md },
});
