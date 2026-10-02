import { useCallback, useEffect, useState } from 'react';
import { RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Badge, Button, Card, EmptyState, SectionTitle } from '../../src/components/ui';
import { pulseApi, type PulseDto } from '../../src/api/client';
import { colors, spacing, typography } from '../../src/theme';

/**
 * Family Pulse — "What matters to me right now?"
 * Uses live API when authenticated; falls back to demo shell offline.
 */
export default function HomeScreen() {
  const [pulse, setPulse] = useState<PulseDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    setRefreshing(true);
    setError(null);
    try {
      const data = await pulseApi.get();
      setPulse(data);
    } catch (e: unknown) {
      const msg =
        e && typeof e === 'object' && 'error' in e
          ? String((e as { error: string }).error)
          : 'Unable to load Pulse';
      setError(msg);
      setPulse({
        greeting: 'Welcome to Family OS',
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
        comingUp: [],
        household: {
          requestsAwaitingApproval: 2,
          tasksDueToday: 3,
          tasksNeedingAcceptance: 1,
        },
      });
    } finally {
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={load} tintColor={colors.primary} />}
    >
      <Text style={typography.title}>{pulse?.greeting ?? 'Family OS'}</Text>
      {error ? (
        <Text style={styles.hint} accessibilityLiveRegion="polite">
          Offline demo mode · {error}
        </Text>
      ) : null}

      <SectionTitle>Your next action</SectionTitle>
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
          <Button title={pulse.nextAction.primaryAction} onPress={() => {}} />
        </Card>
      ) : (
        <EmptyState title="You're clear" subtitle="No urgent next action right now." />
      )}

      <SectionTitle>Needs your attention</SectionTitle>
      {pulse?.needsAttention?.length ? (
        pulse.needsAttention.map((item) => (
          <Card key={item.entityId} style={styles.cardGap}>
            <Text style={typography.heading}>{item.title}</Text>
            <Text style={typography.caption}>{item.reason}</Text>
            <Button title={item.actionLabel} variant="secondary" onPress={() => {}} />
          </Card>
        ))
      ) : (
        <EmptyState title="Nothing waiting" />
      )}

      <SectionTitle>Household</SectionTitle>
      <Card>
        <Text style={typography.body}>
          {pulse?.household.requestsAwaitingApproval ?? 0} requests awaiting approval
        </Text>
        <Text style={typography.body}>{pulse?.household.tasksDueToday ?? 0} tasks due today</Text>
        <Text style={typography.body}>
          {pulse?.household.tasksNeedingAcceptance ?? 0} tasks need acceptance
        </Text>
      </Card>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  content: { padding: spacing.md, paddingBottom: spacing.xl * 2 },
  cardGap: { marginBottom: spacing.md, gap: spacing.sm },
  row: { flexDirection: 'row', gap: spacing.sm, marginVertical: spacing.sm },
  hint: { ...typography.caption, marginBottom: spacing.md },
});
