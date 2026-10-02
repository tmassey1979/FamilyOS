import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Card, EmptyState, SectionTitle } from '../../src/components/ui';
import { api } from '../../src/api/client';
import { colors, spacing, typography } from '../../src/theme';

type CalEvent = {
  id: string;
  title: string;
  description?: string;
  startUtc: string;
  endUtc?: string;
  allDay: boolean;
  location?: string;
};

export default function CalendarScreen() {
  const [events, setEvents] = useState<CalEvent[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    setRefreshing(true);
    setError(null);
    try {
      const data = await api<CalEvent[]>('/api/calendar/upcoming?days=14');
      setEvents(data);
    } catch (e: unknown) {
      const msg =
        e && typeof e === 'object' && 'error' in e
          ? String((e as { error: string }).error)
          : 'Unable to load calendar';
      setError(msg);
      setEvents([]);
    } finally {
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const startOfTomorrow = new Date();
  startOfTomorrow.setHours(24, 0, 0, 0);
  const today = events.filter((e) => new Date(e.startUtc).getTime() < startOfTomorrow.getTime());
  const upcoming = events.filter((e) => new Date(e.startUtc).getTime() >= startOfTomorrow.getTime());

  const formatWhen = (iso: string, allDay: boolean) => {
    const d = new Date(iso);
    if (allDay) return d.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' });
    return d.toLocaleString(undefined, {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
    });
  };

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={load} tintColor={colors.primary} />}
    >
      {error ? <Text style={styles.hint}>{error}</Text> : null}
      {refreshing && events.length === 0 ? <ActivityIndicator color={colors.primary} /> : null}

      <SectionTitle>Today</SectionTitle>
      {today.length === 0 ? (
        <EmptyState title="Nothing on the calendar today" />
      ) : (
        today.map((e) => (
          <Card key={e.id} style={styles.card}>
            <Text style={typography.heading}>{formatWhen(e.startUtc, e.allDay)} · {e.title}</Text>
            {e.location ? <Text style={typography.caption}>{e.location}</Text> : null}
          </Card>
        ))
      )}

      <SectionTitle>Upcoming</SectionTitle>
      {upcoming.length === 0 ? (
        <EmptyState title="No upcoming events" subtitle="Next 14 days are clear." />
      ) : (
        upcoming.map((e) => (
          <Card key={e.id} style={styles.card}>
            <Text style={typography.heading}>{formatWhen(e.startUtc, e.allDay)} · {e.title}</Text>
            {e.location ? <Text style={typography.caption}>{e.location}</Text> : null}
          </Card>
        ))
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  content: { padding: spacing.md, paddingBottom: spacing.xl * 2 },
  card: { marginBottom: spacing.md, gap: spacing.xs },
  hint: { ...typography.caption, marginBottom: spacing.md },
});
