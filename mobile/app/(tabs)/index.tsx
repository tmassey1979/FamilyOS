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
import { colors, spacing, typography } from '../../src/theme';

export default function HomeScreen() {
  const router = useRouter();
  const [pulse, setPulse] = useState<PulseDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const p = await pulseApi.get();
      setPulse(p);
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not load pulse');
      setPulse(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const attentionItems = pulse?.needsAttention ?? [];
  const next = pulse?.nextAction;
  const blockers = pulse?.waitingBlockers ?? [];
  const comingUp = pulse?.comingUp ?? [];

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={loading} onRefresh={() => void load()} />}
    >
      <SectionTitle>Home</SectionTitle>
      {error ? <Text style={styles.error}>{error}</Text> : null}
      {loading && !pulse ? <ActivityIndicator color={colors.primary} /> : null}

      {pulse ? (
        <>
          <Card style={styles.card}>
            <Text style={typography.heading}>{pulse.greeting}</Text>
            {pulse.role ? (
              <View style={styles.row}>
                <Badge label={pulse.role} />
                {pulse.roleHint ? <Text style={typography.caption}>{pulse.roleHint}</Text> : null}
              </View>
            ) : null}
          </Card>

          {next ? (
            <Card style={styles.card}>
              <SectionTitle>Next best action</SectionTitle>
              <Text style={typography.heading}>{next.title}</Text>
              {next.subtitle ? <Text style={typography.caption}>{next.subtitle}</Text> : null}
              <View style={styles.row}>
                <Badge label={next.kind} />
                {next.dueLabel ? <Text style={typography.caption}>{next.dueLabel}</Text> : null}
              </View>
              <Button
                title={next.primaryAction}
                onPress={() => {
                  if (next.kind.toLowerCase().includes('task')) router.push('/(tabs)/tasks');
                  else if (next.kind.toLowerCase().includes('request')) router.push('/(tabs)/requests');
                  else router.push('/(tabs)/family');
                }}
              />
            </Card>
          ) : null}

          <SectionTitle>Needs attention</SectionTitle>
          {attentionItems.length === 0 ? (
            <EmptyState title="All clear" subtitle="Nothing needs you right now." />
          ) : (
            attentionItems.map((a) => (
              <Card key={a.entityId} style={styles.card}>
                <Text style={typography.heading}>{a.title}</Text>
                <Text style={typography.caption}>{a.reason}</Text>
                <View style={styles.row}>
                  <Badge label={a.kind} tone="danger" />
                  <Text style={typography.caption}>{a.actionLabel}</Text>
                </View>
              </Card>
            ))
          )}

          {blockers.length > 0 ? (
            <>
              <SectionTitle>Waiting / blockers</SectionTitle>
              {blockers.map((b) => (
                <Card key={b.entityId} style={styles.card}>
                  <Text style={typography.heading}>{b.title}</Text>
                  <Text style={typography.caption}>{b.reason}</Text>
                  <Badge label={b.kind} />
                </Card>
              ))}
            </>
          ) : null}

          {comingUp.length > 0 ? (
            <>
              <SectionTitle>Coming up</SectionTitle>
              {comingUp.map((c) => (
                <Card key={c.entityId} style={styles.card}>
                  <Text style={typography.heading}>{c.title}</Text>
                  <Text style={typography.caption}>
                    {new Date(c.whenUtc).toLocaleString()}
                    {c.location ? ` · ${c.location}` : ''}
                  </Text>
                </Card>
              ))}
            </>
          ) : null}

          {pulse.household ? (
            <Card style={styles.card}>
              <SectionTitle>Household</SectionTitle>
              <Text style={typography.caption}>
                Approvals: {pulse.household.requestsAwaitingApproval} · Tasks due today:{' '}
                {pulse.household.tasksDueToday} · Need accept: {pulse.household.tasksNeedingAcceptance}
              </Text>
            </Card>
          ) : null}
        </>
      ) : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  content: { padding: spacing.md, paddingBottom: spacing.xl * 2, gap: spacing.sm },
  card: { gap: spacing.xs, marginBottom: spacing.sm },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, alignItems: 'center' },
  error: { ...typography.caption, color: colors.danger },
});
