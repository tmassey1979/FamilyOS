import { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator, Modal, RefreshControl, ScrollView, StyleSheet, Text, TextInput, View,
} from 'react-native';
import { Button, Card, EmptyState, SectionTitle } from '../../src/components/ui';
import { calendarApi, type CalendarEventDto } from '../../src/api/client';
import { colors, spacing, typography } from '../../src/theme';

export default function CalendarScreen() {
  const [events, setEvents] = useState<CalendarEventDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [title, setTitle] = useState('');
  const [location, setLocation] = useState('');
  const [hoursFromNow, setHoursFromNow] = useState('2');
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setRefreshing(true);
    setError(null);
    try {
      setEvents(await calendarApi.upcoming(14));
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Unable to load calendar');
      setEvents([]);
    } finally {
      setRefreshing(false);
    }
  }, []);

  useEffect(() => { void load(); }, [load]);

  const startOfTomorrow = new Date();
  startOfTomorrow.setHours(24, 0, 0, 0);
  const today = events.filter((e) => new Date(e.startUtc).getTime() < startOfTomorrow.getTime());
  const upcoming = events.filter((e) => new Date(e.startUtc).getTime() >= startOfTomorrow.getTime());

  const formatWhen = (iso: string, allDay: boolean) => {
    const d = new Date(iso);
    if (allDay) return d.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' });
    return d.toLocaleString(undefined, {
      weekday: 'short', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit',
    });
  };

  const createEvent = async () => {
    if (!title.trim()) return;
    setSaving(true);
    try {
      const hrs = Math.max(0, Number(hoursFromNow) || 2);
      const start = new Date(Date.now() + hrs * 3600_000);
      const end = new Date(start.getTime() + 3600_000);
      await calendarApi.create({
        title: title.trim(),
        startUtc: start.toISOString(),
        endUtc: end.toISOString(),
        allDay: false,
        location: location.trim() || undefined,
      });
      setTitle('');
      setLocation('');
      setCreateOpen(false);
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Create failed');
    } finally {
      setSaving(false);
    }
  };

  const cancelEvent = async (id: string) => {
    try {
      await calendarApi.cancel(id);
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Cancel failed');
    }
  };

  const renderEvent = (e: CalendarEventDto) => (
    <Card key={e.id} style={styles.card}>
      <Text style={typography.heading}>{formatWhen(e.startUtc, e.allDay)} · {e.title}</Text>
      {e.location ? <Text style={typography.caption}>{e.location}</Text> : null}
      {e.linkedRequestId ? <Text style={typography.caption}>Linked request</Text> : null}
      <Button title="Cancel event" variant="secondary" onPress={() => void cancelEvent(e.id)} />
    </Card>
  );

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={load} tintColor={colors.primary} />}
    >
      <View style={styles.toolbar}>
        <SectionTitle>Calendar</SectionTitle>
        <Button title="New event" onPress={() => setCreateOpen(true)} />
      </View>
      {error ? <Text style={styles.hint}>{error}</Text> : null}
      {refreshing && events.length === 0 ? <ActivityIndicator color={colors.primary} /> : null}

      <SectionTitle>Today</SectionTitle>
      {today.length === 0 ? <EmptyState title="Nothing on the calendar today" /> : today.map(renderEvent)}

      <SectionTitle>Upcoming</SectionTitle>
      {upcoming.length === 0 ? (
        <EmptyState title="No upcoming events" subtitle="Next 14 days are clear — or commit a Ride plan." />
      ) : upcoming.map(renderEvent)}

      <Modal visible={createOpen} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>New event</Text>
            <TextInput style={styles.input} placeholder="Title" placeholderTextColor={colors.textMuted} value={title} onChangeText={setTitle} />
            <TextInput style={styles.input} placeholder="Location (optional)" placeholderTextColor={colors.textMuted} value={location} onChangeText={setLocation} />
            <TextInput
              style={styles.input}
              placeholder="Hours from now"
              placeholderTextColor={colors.textMuted}
              keyboardType="numeric"
              value={hoursFromNow}
              onChangeText={setHoursFromNow}
            />
            <View style={styles.modalActions}>
              <Button title="Cancel" variant="secondary" onPress={() => setCreateOpen(false)} />
              <Button title={saving ? 'Saving…' : 'Create'} onPress={() => void createEvent()} />
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
  toolbar: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: spacing.sm },
  card: { marginBottom: spacing.md, gap: spacing.xs },
  hint: { ...typography.caption, marginBottom: spacing.md, color: colors.danger },
  modalBackdrop: { flex: 1, backgroundColor: 'rgba(0,0,0,0.5)', justifyContent: 'flex-end' },
  modalCard: {
    backgroundColor: colors.surface, borderTopLeftRadius: 16, borderTopRightRadius: 16,
    padding: spacing.lg, gap: spacing.md,
  },
  input: {
    borderWidth: 1, borderColor: colors.border, borderRadius: 8, padding: spacing.sm,
    color: colors.text, backgroundColor: colors.bg,
  },
  modalActions: { flexDirection: 'row', justifyContent: 'flex-end', gap: spacing.sm },
});
