import { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator, Modal, RefreshControl, ScrollView, StyleSheet, Text, TextInput, View,
} from 'react-native';
import { Button, Card, EmptyState, SectionTitle } from '../../src/components/ui';
import { calendarApi, type CalendarEventDto } from '../../src/api/client';
import { colors, spacing, typography } from '../../src/theme';
import { withObserveScreen } from '../../src/analytics/withObserveScreen';

function CalendarScreen() {
  const [events, setEvents] = useState<CalendarEventDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [editorOpen, setEditorOpen] = useState(false);
  const [editingEvent, setEditingEvent] = useState<CalendarEventDto | null>(null);
  const [title, setTitle] = useState('');
  const [location, setLocation] = useState('');
  const [startLocal, setStartLocal] = useState('');
  const [endLocal, setEndLocal] = useState('');
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

  const toLocalInput = (value: Date) => {
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())} ${pad(value.getHours())}:${pad(value.getMinutes())}`;
  };

  const parseLocalInput = (value: string): string | null => {
    const match = /^(\\d{4})-(\\d{2})-(\\d{2}) (\\d{2}):(\\d{2})$/.exec(value.trim());
    if (!match) return null;
    const [, year, month, day, hour, minute] = match;
    const parsed = new Date(Number(year), Number(month) - 1, Number(day), Number(hour), Number(minute));
    if (
      parsed.getFullYear() !== Number(year) ||
      parsed.getMonth() !== Number(month) - 1 ||
      parsed.getDate() !== Number(day) ||
      parsed.getHours() !== Number(hour) ||
      parsed.getMinutes() !== Number(minute)
    ) return null;
    return parsed.toISOString();
  };

  const openCreate = () => {
    const start = new Date(Date.now() + 2 * 3600_000);
    const end = new Date(start.getTime() + 3600_000);
    setEditingEvent(null);
    setTitle('');
    setLocation('');
    setStartLocal(toLocalInput(start));
    setEndLocal(toLocalInput(end));
    setError(null);
    setEditorOpen(true);
  };

  const openEdit = (event: CalendarEventDto) => {
    setEditingEvent(event);
    setTitle(event.title);
    setLocation(event.location ?? '');
    setStartLocal(toLocalInput(new Date(event.startUtc)));
    setEndLocal(event.endUtc ? toLocalInput(new Date(event.endUtc)) : '');
    setError(null);
    setEditorOpen(true);
  };

  const saveEvent = async () => {
    if (!title.trim()) {
      setError('Enter an event title.');
      return;
    }
    const startUtc = parseLocalInput(startLocal);
    const endUtc = parseLocalInput(endLocal);
    if (!startUtc || !endUtc) {
      setError('Enter valid local dates and times using YYYY-MM-DD HH:MM.');
      return;
    }
    if (new Date(endUtc).getTime() <= new Date(startUtc).getTime()) {
      setError('The event end must be after its start.');
      return;
    }

    setSaving(true);
    setError(null);
    try {
      const body = {
        title: title.trim(),
        startUtc,
        endUtc,
        allDay: false,
        location: location.trim() || null,
      };
      if (editingEvent) await calendarApi.update(editingEvent.id, body);
      else await calendarApi.create(body);
      setEditorOpen(false);
      setEditingEvent(null);
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? (editingEvent ? 'Update failed' : 'Create failed'));
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
      <View style={styles.eventActions}>
        <Button title="Edit" variant="secondary" onPress={() => openEdit(e)} />
        <Button title="Cancel event" variant="secondary" onPress={() => void cancelEvent(e.id)} />
      </View>
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
        <Button title="New event" onPress={openCreate} />
      </View>
      {error ? <Text style={styles.hint}>{error}</Text> : null}
      {refreshing && events.length === 0 ? <ActivityIndicator color={colors.primary} /> : null}

      <SectionTitle>Today</SectionTitle>
      {today.length === 0 ? <EmptyState title="Nothing on the calendar today" /> : today.map(renderEvent)}

      <SectionTitle>Upcoming</SectionTitle>
      {upcoming.length === 0 ? (
        <EmptyState title="No upcoming events" subtitle="Next 14 days are clear — or commit a Ride plan." />
      ) : upcoming.map(renderEvent)}

      <Modal visible={editorOpen} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>{editingEvent ? 'Edit event' : 'New event'}</Text>
            <TextInput style={styles.input} placeholder="Title" placeholderTextColor={colors.textMuted} value={title} onChangeText={setTitle} />
            <TextInput style={styles.input} placeholder="Location (optional)" placeholderTextColor={colors.textMuted} value={location} onChangeText={setLocation} />
            <Text style={typography.caption}>Times use your device's local time.</Text>
            <TextInput
              style={styles.input}
              placeholder="Start · YYYY-MM-DD HH:MM"
              placeholderTextColor={colors.textMuted}
              value={startLocal}
              onChangeText={setStartLocal}
              autoCapitalize="none"
            />
            <TextInput
              style={styles.input}
              placeholder="End · YYYY-MM-DD HH:MM"
              placeholderTextColor={colors.textMuted}
              value={endLocal}
              onChangeText={setEndLocal}
              autoCapitalize="none"
            />
            {error ? <Text style={styles.hint}>{error}</Text> : null}
            <View style={styles.modalActions}>
              <Button title="Cancel" variant="secondary" onPress={() => { setEditorOpen(false); setEditingEvent(null); }} />
              <Button title={saving ? 'Saving…' : editingEvent ? 'Save changes' : 'Create'} onPress={() => void saveEvent()} disabled={saving} />
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
  eventActions: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
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

export default withObserveScreen(CalendarScreen);