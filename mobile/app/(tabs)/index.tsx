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
import { Badge, Button, Card, SectionTitle } from '../../src/components/ui';
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

function routeForKind(kind: string): string {
  const k = kind.toLowerCase();
  if (k.includes('request')) return '/(tabs)/requests';
  if (k.includes('task')) return '/(tabs)/tasks';
  if (k.includes('shop') || k.includes('procure') || k.includes('cart')) return '/(tabs)/family';
  if (k.includes('meal')) return '/(tabs)/meals';
  if (k.includes('chat')) return '/(tabs)/chat';
  return '/(tabs)';
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

  const attention = pulse?.attention ?? [];
  const blockers = pulse?.waitingBlockers ?? [];
  const comingUp = pulse?.comingUp ?? [];
  const quiet =
    !pulse ||
    (attention.length === 0 && blockers.length === 0 && comingUp.length === 0 && todayMeals.length === 0);

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={load} tintColor={colors.primary} />}
    >
      <Text style={typography.title}>Family Pulse</Text>
      {pulse?.greeting ? <Text style={typography.caption}>{pulse.greeting}</Text> : null}
      {pulse?.role ? (
        <Text style={typography.caption}>
          {pulse.role}
          {pulse.roleHint ? ` · ${pulse.roleHint}` : ''}
        </Text>
      ) : null}
      {error ? <Text style={styles.error}>{error}</Text> : null}
      {refreshing && !pulse ? <ActivityIndicator color={colors.primary} /> : null}

      {pulse?.nextBestAction ? (
        <Card style={styles.cardGap}>
          <Text style={typography.heading}>{pulse.nextBestAction.title}</Text>
          {pulse.nextBestAction.subtitle ? (
            <Text style={typography.caption}>{pulse.nextBestAction.subtitle}</Text>
          ) : null}
          <Button
            title={pulse.nextBestAction.actionLabel || 'Open'}
            onPress={() => router.push(routeForKind(pulse.nextBestAction!.kind) as never)}
          />
        </Card>
      ) : null}

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

      {blockers.length > 0 ? (
        <>
          <SectionTitle>Waiting / blockers</SectionTitle>
          {blockers.map((b, i) => (
            <Pressable
              key={`${b.kind}-${b.relatedEntityId ?? i}`}
              onPress={() => router.push(routeForKind(b.kind) as never)}
            >
              <Card style={styles.cardGap}>
                <Text style={typography.heading}>{b.title}</Text>
                {b.subtitle ? <Text style={typography.caption}>{b.subtitle}</Text> : null}
                <Badge label={b.kind} tone="danger" />
              </Card>
            </Pressable>
          ))}
        </>
      ) : null}

      {attention.length > 0 ? (
        <>
          <SectionTitle>Needs attention</SectionTitle>
          {attention.map((a, i) => (
            <Pressable
              key={`${a.kind}-${a.relatedEntityId ?? i}`}
              onPress={() => router.push(routeForKind(a.kind) as never)}
            >
              <Card style={styles.cardGap}>
                <Text style={typography.heading}>{a.title}</Text>
                {a.subtitle ? <Text style={typography.caption}>{a.subtitle}</Text> : null}
                <View style={styles.row}>
                  <Badge label={a.kind} />
                  {a.dueLabel ? <Badge label={a.dueLabel} tone="danger" /> : null}
                </View>
              </Card>
            </Pressable>
          ))}
        </>
      ) : null}

      {comingUp.length > 0 ? (
        <>
          <SectionTitle>Coming up</SectionTitle>
          {comingUp.map((c, i) => (
            <Card key={`${c.title}-${i}`} style={styles.cardGap}>
              <Text style={typography.body}>{c.title}</Text>
              {c.whenLabel ? <Text style={typography.caption}>{c.whenLabel}</Text> : null}
            </Card>
          ))}
        </>
      ) : null}

      {pulse?.counts ? (
        <Text style={[typography.caption, { marginBottom: spacing.sm }]}>
          Household — tasks {pulse.counts.openTasks} · requests {pulse.counts.pendingRequests} · shopping{' '}
          {pulse.counts.queuedShopping}
        </Text>
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
