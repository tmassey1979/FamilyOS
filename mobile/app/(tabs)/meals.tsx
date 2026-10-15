import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Modal,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { Badge, Button, Card, EmptyState, SectionTitle } from '../../src/components/ui';
import { mealsApi, type MealEntryDto, type MealPlanDto } from '../../src/api/meals';
import { colors, spacing, typography } from '../../src/theme';

const SLOTS = ['Breakfast', 'Lunch', 'Dinner', 'Snack'] as const;

function mondayOf(d: Date): Date {
  const x = new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate()));
  const day = x.getUTCDay();
  const diff = day === 0 ? -6 : 1 - day;
  x.setUTCDate(x.getUTCDate() + diff);
  x.setUTCHours(0, 0, 0, 0);
  return x;
}

function addDays(d: Date, n: number): Date {
  const x = new Date(d);
  x.setUTCDate(x.getUTCDate() + n);
  return x;
}

function dayLabel(d: Date): string {
  return d.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric', timeZone: 'UTC' });
}

export default function MealsScreen() {
  const [weekStart, setWeekStart] = useState(() => mondayOf(new Date()));
  const [plan, setPlan] = useState<MealPlanDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [msg, setMsg] = useState<string | null>(null);
  const [edit, setEdit] = useState<{ day: Date; slot: string; existing?: MealEntryDto } | null>(null);
  const [dish, setDish] = useState('');
  const [ingredients, setIngredients] = useState('');
  const [busy, setBusy] = useState(false);

  const weekIso = weekStart.toISOString();

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setPlan(await mealsApi.week(weekIso));
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not load meal plan');
      setPlan(null);
    } finally {
      setLoading(false);
    }
  }, [weekIso]);

  useEffect(() => {
    void load();
  }, [load]);

  const days = useMemo(() => Array.from({ length: 7 }, (_, i) => addDays(weekStart, i)), [weekStart]);

  const entryFor = (day: Date, slot: string): MealEntryDto | undefined => {
    if (!plan) return undefined;
    const dayStr = day.toISOString().slice(0, 10);
    return plan.entries.find(
      (e) => e.dayUtc.slice(0, 10) === dayStr && e.slot.toLowerCase() === slot.toLowerCase(),
    );
  };

  const openEdit = (day: Date, slot: string) => {
    const existing = entryFor(day, slot);
    setEdit({ day, slot, existing });
    setDish(existing?.dishName ?? '');
    setIngredients(existing?.ingredientsCsv ?? '');
  };

  const save = async () => {
    if (!edit || !dish.trim()) return;
    setBusy(true);
    setError(null);
    try {
      await mealsApi.upsert({
        dayUtc: edit.day.toISOString(),
        slot: edit.slot,
        dishName: dish.trim(),
        ingredientsCsv: ingredients.trim() || undefined,
        weekStartUtc: weekIso,
      });
      setEdit(null);
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not save');
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    if (!edit?.existing) return;
    setBusy(true);
    try {
      await mealsApi.remove(edit.existing.id);
      setEdit(null);
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not remove');
    } finally {
      setBusy(false);
    }
  };

  const pushShopping = async () => {
    setBusy(true);
    setMsg(null);
    try {
      const r = await mealsApi.toShopping(weekIso);
      setMsg(r.added === 0 ? 'No new ingredients to add' : `Added ${r.added} item(s) to shopping queue`);
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not push to shopping');
    } finally {
      setBusy(false);
    }
  };

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={loading} onRefresh={() => void load()} />}
    >
      <SectionTitle>This week</SectionTitle>
      <View style={styles.row}>
        <Button
          title="← Prev"
          variant="secondary"
          onPress={() => setWeekStart((w) => addDays(w, -7))}
        />
        <Text style={typography.caption}>{dayLabel(weekStart)} – {dayLabel(addDays(weekStart, 6))}</Text>
        <Button title="Next →" variant="secondary" onPress={() => setWeekStart((w) => addDays(w, 7))} />
      </View>
      <View style={styles.row}>
        <Button title="This week" variant="secondary" onPress={() => setWeekStart(mondayOf(new Date()))} />
        <Button title={busy ? '…' : 'Ingredients → shopping'} onPress={() => void pushShopping()} />
      </View>
      {error ? <Text style={styles.error}>{error}</Text> : null}
      {msg ? <Text style={styles.msg}>{msg}</Text> : null}
      {loading && !plan ? <ActivityIndicator color={colors.primary} /> : null}
      {!loading && !plan ? (
        <EmptyState title="No meal plan" subtitle="Pull to refresh or check your connection." />
      ) : null}

      {days.map((day) => (
        <Card key={day.toISOString()} style={styles.dayCard}>
          <Text style={typography.heading}>{dayLabel(day)}</Text>
          {SLOTS.map((slot) => {
            const e = entryFor(day, slot);
            return (
              <Pressable key={slot} onPress={() => openEdit(day, slot)} style={styles.slotRow}>
                <Badge label={slot} />
                <Text style={[typography.body, { flex: 1 }]} numberOfLines={1}>
                  {e?.dishName ?? '—'}
                </Text>
                {e?.ingredientsCsv ? (
                  <Text style={typography.caption} numberOfLines={1}>
                    {e.ingredientsCsv}
                  </Text>
                ) : null}
              </Pressable>
            );
          })}
        </Card>
      ))}

      <Modal visible={!!edit} transparent animationType="slide">
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>
              {edit?.slot} · {edit ? dayLabel(edit.day) : ''}
            </Text>
            <TextInput
              style={styles.input}
              placeholder="Dish name"
              placeholderTextColor={colors.textMuted}
              value={dish}
              onChangeText={setDish}
            />
            <TextInput
              style={styles.input}
              placeholder="Ingredients (comma-separated)"
              placeholderTextColor={colors.textMuted}
              value={ingredients}
              onChangeText={setIngredients}
            />
            <View style={styles.row}>
              <Button title="Cancel" variant="secondary" onPress={() => setEdit(null)} />
              {edit?.existing ? (
                <Button title="Remove" variant="secondary" onPress={() => void remove()} />
              ) : null}
              <Button title={busy ? '…' : 'Save'} onPress={() => void save()} />
            </View>
          </View>
        </View>
      </Modal>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  content: { padding: spacing.md, paddingBottom: spacing.xl * 2, gap: spacing.sm },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, alignItems: 'center', marginBottom: spacing.sm },
  dayCard: { gap: spacing.xs, marginBottom: spacing.sm },
  slotRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    paddingVertical: spacing.xs,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: colors.border,
  },
  error: { ...typography.caption, color: colors.danger },
  msg: { ...typography.caption, color: colors.primary },
  modalBackdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.55)',
    justifyContent: 'flex-end',
  },
  modalCard: {
    backgroundColor: colors.surface,
    padding: spacing.lg,
    borderTopLeftRadius: 16,
    borderTopRightRadius: 16,
    gap: spacing.sm,
  },
  input: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 8,
    padding: spacing.sm,
    color: colors.text,
    backgroundColor: colors.bg,
  },
});
