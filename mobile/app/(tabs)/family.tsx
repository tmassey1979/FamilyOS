import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Badge, Button, Card, EmptyState, SectionTitle } from '../../src/components/ui';
import {
  familyApi,
  meApi,
  procurementApi,
  type MeDto,
  type ProcurementItemDto,
} from '../../src/api/client';
import { colors, spacing, typography } from '../../src/theme';

type Member = { id: string; displayName: string; role: string; isActive: boolean };

export default function FamilyScreen() {
  const [familyName, setFamilyName] = useState('Family');
  const [members, setMembers] = useState<Member[]>([]);
  const [me, setMe] = useState<MeDto | null>(null);
  const [queue, setQueue] = useState<ProcurementItemDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    setRefreshing(true);
    setError(null);
    try {
      const [fam, mems, who, items] = await Promise.all([
        familyApi.get(),
        familyApi.members(),
        meApi.get().catch(() => null),
        procurementApi.queue().catch(() => [] as ProcurementItemDto[]),
      ]);
      setFamilyName(fam.name);
      setMembers(mems);
      setMe(who);
      setQueue(items);
    } catch (e: unknown) {
      const msg =
        e && typeof e === 'object' && 'error' in e
          ? String((e as { error: string }).error)
          : 'Unable to load family';
      setError(msg);
      setFamilyName('The Hendersons');
      setMembers([
        { id: '1', displayName: 'Terry', role: 'Owner', isActive: true },
        { id: '2', displayName: 'Michelle', role: 'Adult', isActive: true },
        { id: '3', displayName: 'Mia', role: 'Teen', isActive: true },
        { id: '4', displayName: 'Eli', role: 'Child', isActive: true },
      ]);
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
      <Text style={typography.title}>{familyName}</Text>
      {me?.role ? (
        <Text style={typography.caption}>Signed in as {me.externalIdentityId} · {me.role}</Text>
      ) : null}
      {error ? <Text style={styles.hint}>Offline demo · {error}</Text> : null}

      <SectionTitle>Members</SectionTitle>
      {refreshing && members.length === 0 ? <ActivityIndicator color={colors.primary} /> : null}
      {members.map((m) => (
        <Card key={m.id} style={styles.card}>
          <Text style={typography.heading}>{m.displayName}</Text>
          <Badge label={m.role} tone={m.role === 'Owner' ? 'success' : 'default'} />
        </Card>
      ))}

      <SectionTitle>Shopping queue</SectionTitle>
      {queue.length === 0 ? (
        <EmptyState title="Queue empty" subtitle="Approved purchases show up here." />
      ) : (
        queue.map((item) => (
          <Card key={item.id} style={styles.cardCol}>
            <Text style={typography.heading}>{item.name}</Text>
            <View style={styles.row}>
              <Badge label={item.status} tone="warning" />
              {item.preferredStore ? <Badge label={item.preferredStore} /> : null}
              {item.estimatedPrice != null ? (
                <Text style={typography.caption}>~${item.estimatedPrice.toFixed(2)}</Text>
              ) : null}
            </View>
            {item.status === 'Queued' || item.status === 'Approved' ? (
              <Button
                title="Hold"
                variant="secondary"
                onPress={() => procurementApi.hold(item.id).then(load).catch(() => {})}
              />
            ) : null}
          </Card>
        ))
      )}

      <Text style={[typography.caption, { marginTop: spacing.lg }]}>
        Roles and permissions are enforced on the API — UI is never the security boundary.
      </Text>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  content: { padding: spacing.md, paddingBottom: spacing.xl * 2 },
  card: {
    marginBottom: spacing.sm,
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  cardCol: { marginBottom: spacing.sm, gap: spacing.sm },
  row: { flexDirection: 'row', gap: spacing.sm, alignItems: 'center', flexWrap: 'wrap' },
  hint: { ...typography.caption, marginBottom: spacing.md },
});
