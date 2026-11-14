import { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { Card, EmptyState, SectionTitle } from '../../src/components/ui';
import { outcomesApi, type OutcomesDto } from '../../src/api/client';
import { colors, spacing, typography } from '../../src/theme';
import { withObserveScreen } from '../../src/analytics/withObserveScreen';

function Metric({ label, value, detail }: { label: string; value: string | number; detail?: string }) {
  return (
    <Card style={styles.metric}>
      <Text style={styles.metricValue}>{value}</Text>
      <Text style={styles.metricLabel}>{label}</Text>
      {detail ? <Text style={typography.caption}>{detail}</Text> : null}
    </Card>
  );
}

function WeeklySeries({ title, values, tint }: { title: string; values: number[]; tint: string }) {
  const maximum = Math.max(1, ...values);
  return (
    <Card style={styles.seriesCard}>
      <Text style={typography.heading}>{title}</Text>
      {values.map((value, index) => {
        const weekStart = new Date();
        weekStart.setDate(weekStart.getDate() - 7 * (7 - index));
        return (
          <View key={index} style={styles.seriesRow}>
            <Text style={styles.seriesLabel}>{weekStart.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}</Text>
            <View style={styles.track}>
              <View style={[styles.fill, { width: `${Math.round((value / maximum) * 100)}%`, backgroundColor: tint }]} />
            </View>
            <Text style={styles.seriesValue}>{value}</Text>
          </View>
        );
      })}
    </Card>
  );
}

function OutcomesScreen() {
  const [data, setData] = useState<OutcomesDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (refresh = false) => {
    if (refresh) setRefreshing(true);
    else setLoading(true);
    setError(null);
    try {
      setData(await outcomesApi.get());
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not load family outcomes');
    } finally {
      setLoading(false);
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
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void load(true)} tintColor={colors.primary} />}
    >
      <SectionTitle>Family outcomes</SectionTitle>
      <Text style={typography.caption}>A shared view of what the household has completed and what still needs attention.</Text>
      {error ? <Text style={styles.error}>{error}</Text> : null}
      {loading && !data ? <ActivityIndicator color={colors.primary} /> : null}
      {!loading && !data ? <EmptyState title="Outcomes unavailable" subtitle="Pull down to try again." /> : null}
      {data ? (
        <>
          <Text style={typography.heading}>Current workload</Text>
          <View style={styles.metricsGrid}>
            <Metric label="Open tasks" value={data.openTasks} />
            <Metric label="Pending requests" value={data.pendingRequests} />
            <Metric label="Shopping queue" value={data.queuedShoppingItems} />
          </View>

          <Text style={typography.heading}>Last 7 days</Text>
          <View style={styles.metricsGrid}>
            <Metric label="Tasks completed" value={data.tasksCompletedLast7Days} />
            <Metric label="Requests approved" value={data.requestsApprovedLast7Days} />
            <Metric label="Requests denied" value={data.requestsDeniedLast7Days} />
            <Metric label="Items purchased" value={data.shoppingItemsPurchasedLast7Days} />
            <Metric label="Approval rate" value={data.approvalRateLast7Days == null ? '—' : `${data.approvalRateLast7Days}%`} />
            <Metric label="Avg. task time" value={data.avgTaskCompletionMinutes == null ? '—' : `${Math.round(data.avgTaskCompletionMinutes)} min`} />
          </View>

          <Text style={typography.heading}>Last 30 days</Text>
          <View style={styles.metricsGrid}>
            <Metric label="Tasks completed" value={data.tasksCompletedLast30Days} />
            <Metric label="Requests approved" value={data.requestsApprovedLast30Days} />
            <Metric label="Requests denied" value={data.requestsDeniedLast30Days} />
          </View>

          <Text style={typography.heading}>Eight-week activity</Text>
          <WeeklySeries title="Tasks completed" values={data.weeklyTasksCompleted ?? []} tint={colors.primary} />
          <WeeklySeries title="Requests approved" values={data.weeklyRequestsApproved ?? []} tint={colors.success} />
          <WeeklySeries title="Shopping purchased" values={data.weeklyShoppingPurchased ?? []} tint={colors.warning} />
        </>
      ) : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  content: { padding: spacing.md, paddingBottom: spacing.xl * 2, gap: spacing.md },
  metricsGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  metric: { flexGrow: 1, flexBasis: '30%', minWidth: 100, gap: spacing.xs },
  metricValue: { ...typography.heading, color: colors.primary },
  metricLabel: { ...typography.caption, color: colors.text, fontWeight: '700' },
  seriesCard: { gap: spacing.sm },
  seriesRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  seriesLabel: { ...typography.caption, width: 58, color: colors.textMuted },
  track: { flex: 1, height: 10, borderRadius: 6, overflow: 'hidden', backgroundColor: colors.surfaceAlt },
  fill: { height: '100%', borderRadius: 6 },
  seriesValue: { ...typography.caption, width: 24, textAlign: 'right', color: colors.text },
  error: { ...typography.caption, color: colors.danger },
});

export default withObserveScreen(OutcomesScreen);
