import { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator, Modal, Pressable, RefreshControl, ScrollView, StyleSheet, Text, TextInput, View,
} from 'react-native';
import { Badge, Button, Card, EmptyState, SectionTitle } from '../../src/components/ui';
import {
  familyApi, meApi, procurementApi,
  type MeDto, type ProcurementItemDto,
} from '../../src/api/client';
import { colors, spacing, typography } from '../../src/theme';

type Member = { id: string; displayName: string; role: string; isActive: boolean };

const ROLES = ['Owner', 'Adult', 'Teen', 'Child'] as const;

export default function FamilyScreen() {
  const [familyName, setFamilyName] = useState('Family');
  const [members, setMembers] = useState<Member[]>([]);
  const [me, setMe] = useState<MeDto | null>(null);
  const [queue, setQueue] = useState<ProcurementItemDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const [addOpen, setAddOpen] = useState(false);
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [role, setRole] = useState<string>('Child');
  const [saving, setSaving] = useState(false);

  const [roleMember, setRoleMember] = useState<Member | null>(null);
  const [nextRole, setNextRole] = useState<string>('Adult');

  const isOwner = me?.role === 'Owner';
  const isAdultOrOwner = me?.role === 'Owner' || me?.role === 'Adult';

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
      setError((e as { error?: string })?.error ?? 'Unable to load family');
    } finally {
      setRefreshing(false);
    }
  }, []);

  useEffect(() => { void load(); }, [load]);

  const addMember = async () => {
    if (!displayName.trim() || !email.trim()) return;
    setSaving(true);
    try {
      await familyApi.addMember({
        email: email.trim(),
        displayName: displayName.trim(),
        role,
        externalIdentityId: email.trim().toLowerCase(),
      });
      setAddOpen(false);
      setDisplayName('');
      setEmail('');
      setRole('Child');
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Add member failed');
    } finally {
      setSaving(false);
    }
  };

  const changeRole = async () => {
    if (!roleMember) return;
    setSaving(true);
    try {
      await familyApi.changeRole(roleMember.id, nextRole);
      setRoleMember(null);
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Change role failed');
    } finally {
      setSaving(false);
    }
  };

  const deactivate = async (m: Member) => {
    try {
      await familyApi.deactivate(m.id);
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Deactivate failed');
    }
  };

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
      {error ? <Text style={styles.hint}>{error}</Text> : null}

      <View style={styles.toolbar}>
        <SectionTitle>Members</SectionTitle>
        {isAdultOrOwner ? <Button title="Add member" onPress={() => setAddOpen(true)} /> : null}
      </View>
      {refreshing && members.length === 0 ? <ActivityIndicator color={colors.primary} /> : null}
      {members.map((m) => (
        <Card key={m.id} style={styles.cardCol}>
          <View style={styles.rowBetween}>
            <Text style={typography.heading}>{m.displayName}</Text>
            <Badge label={m.role} tone={m.role === 'Owner' ? 'success' : 'default'} />
          </View>
          {isOwner && m.role !== 'Owner' ? (
            <View style={styles.row}>
              <Button
                title="Change role"
                variant="secondary"
                onPress={() => {
                  setNextRole(m.role === 'Adult' ? 'Teen' : 'Adult');
                  setRoleMember(m);
                }}
              />
              <Button title="Deactivate" variant="secondary" onPress={() => void deactivate(m)} />
            </View>
          ) : null}
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

      <Modal visible={addOpen} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>Add member</Text>
            <TextInput style={styles.input} placeholder="Display name" placeholderTextColor={colors.textMuted} value={displayName} onChangeText={setDisplayName} />
            <TextInput style={styles.input} placeholder="Email" placeholderTextColor={colors.textMuted} autoCapitalize="none" value={email} onChangeText={setEmail} />
            <View style={styles.row}>
              {ROLES.filter((r) => r !== 'Owner').map((r) => (
                <Pressable key={r} style={[styles.chip, role === r && styles.chipOn]} onPress={() => setRole(r)}>
                  <Text style={styles.chipText}>{r}</Text>
                </Pressable>
              ))}
            </View>
            <View style={styles.modalActions}>
              <Button title="Cancel" variant="secondary" onPress={() => setAddOpen(false)} />
              <Button title={saving ? 'Saving…' : 'Add'} onPress={() => void addMember()} />
            </View>
          </View>
        </View>
      </Modal>

      <Modal visible={!!roleMember} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>Role · {roleMember?.displayName}</Text>
            <View style={styles.row}>
              {ROLES.map((r) => (
                <Pressable key={r} style={[styles.chip, nextRole === r && styles.chipOn]} onPress={() => setNextRole(r)}>
                  <Text style={styles.chipText}>{r}</Text>
                </Pressable>
              ))}
            </View>
            <View style={styles.modalActions}>
              <Button title="Cancel" variant="secondary" onPress={() => setRoleMember(null)} />
              <Button title={saving ? 'Saving…' : 'Save role'} onPress={() => void changeRole()} />
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
  toolbar: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  cardCol: { marginBottom: spacing.sm, gap: spacing.sm },
  row: { flexDirection: 'row', gap: spacing.sm, alignItems: 'center', flexWrap: 'wrap' },
  rowBetween: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
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
  chip: { paddingHorizontal: spacing.sm, paddingVertical: 6, borderRadius: 16, backgroundColor: colors.bg },
  chipOn: { backgroundColor: colors.primary },
  chipText: { ...typography.caption, color: colors.text },
});
