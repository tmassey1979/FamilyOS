import { useCallback, useEffect, useState } from 'react';
import { RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Badge, Button, Card, EmptyState, SectionTitle } from '../../src/components/ui';
import { pulseApi, type PulseDto } from '../../src/api/client';
import { cacheGet } from '../../src/offline';
import { colors, spacing, typography } from '../../src/theme';

/**
 * Family Pulse — "What matters to me right now?"
 * Live API → offline cache → demo shell (last resort).
 */
export default function HomeScreen() {
  const [pulse, setPulse] = useState<PulseDto | null>(null);
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
      // If we got data while offline, api() returned cache transparently
      const cached = await cacheGet<PulseDto>('/api/pulse');
      if (cached && JSON.stringify(cached.data) === JSON.stringify(data)) {
        // Heuristic only when identical; prefer not flashing cache label on live hits
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
          <Button title={pulse.nextAction.primaryAction} onPress={() => {}} />
        </Card>
      ) : (
        <EmptyState title="You're clear" subtitle="No urgent next action right now." />
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
            <Button title={item.actionLabel} variant="secondary" onPress={() => {}} />
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
            <Button title={item.actionLabel} variant="secondary" onPress={() => {}} />
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
      <Card>
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
