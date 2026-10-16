import { useCallback, useEffect, useState } from 'react';
import { useRouter } from 'expo-router';
import {
  ActivityIndicator, Modal, Pressable, RefreshControl, ScrollView, StyleSheet, Text, TextInput, View,
} from 'react-native';
import { Badge, Button, Card, EmptyState, SectionTitle } from '../../src/components/ui';
import {
  familyApi, meApi, procurementApi,
  type CartDto, type MeDto, type ProcurementItemDto, type ProductDto,
} from '../../src/api/client';
import { colors, spacing, typography } from '../../src/theme';

type Member = { id: string; displayName: string; role: string; isActive: boolean };

const ROLES = ['Owner', 'Adult', 'Teen', 'Child'] as const;

export default function FamilyScreen() {
  const router = useRouter();
  const [familyName, setFamilyName] = useState('Family');
  const [members, setMembers] = useState<Member[]>([]);
  const [me, setMe] = useState<MeDto | null>(null);
  const [queue, setQueue] = useState<ProcurementItemDto[]>([]);
  const [carts, setCarts] = useState<CartDto[]>([]);
  const [products, setProducts] = useState<ProductDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const [addOpen, setAddOpen] = useState(false);
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [role, setRole] = useState<string>('Child');
  const [saving, setSaving] = useState(false);
  const [storeName, setStoreName] = useState('');
  const [startingTrip, setStartingTrip] = useState<string | null>(null);
  const [purchaseItem, setPurchaseItem] = useState<ProcurementItemDto | null>(null);
  const [purchasePrice, setPurchasePrice] = useState('');
  const [purchaseStore, setPurchaseStore] = useState('');
  const [purchasing, setPurchasing] = useState(false);

  const [addItemOpen, setAddItemOpen] = useState(false);
  const [itemName, setItemName] = useState('');
  const [itemStore, setItemStore] = useState('');
  const [itemPrice, setItemPrice] = useState('');
  const [itemCategory, setItemCategory] = useState('Grocery');
  const [addingItem, setAddingItem] = useState(false);

  const [roleMember, setRoleMember] = useState<Member | null>(null);
  const [nextRole, setNextRole] = useState<string>('Adult');

  const isOwner = me?.role === 'Owner';
  const isAdultOrOwner = me?.role === 'Owner' || me?.role === 'Adult';

  const actionable = queue.filter(
    (i) =>
      i.status === 'Queued' ||
      i.status === 'Approved' ||
      i.status === 'Held' ||
      i.status === 'Deferred',
  );
  const byStore: Record<string, ProcurementItemDto[]> = {};
  for (const item of actionable) {
    const key = (item.preferredStore || 'Any store').trim() || 'Any store';
    (byStore[key] ??= []).push(item);
  }
  const storeGroups = Object.entries(byStore).sort(([a], [b]) => a.localeCompare(b));

  const startTrip = async (store: string, items: ProcurementItemDto[]) => {
    if (!items.length) return;
    setStartingTrip(store);
    setError(null);
    try {
      const cart = await procurementApi.createCart(store === 'Any store' ? 'Trip' : store);
      for (const it of items) {
        try {
          await procurementApi.addToCart(it.id, cart.id);
        } catch {
          /* continue */
        }
      }
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not start trip');
    } finally {
      setStartingTrip(null);
    }
  };

  const load = useCallback(async () => {
    setRefreshing(true);
    setError(null);
    try {
      const [fam, mems, who, items, activeCarts, catalog] = await Promise.all([
        familyApi.get(),
        familyApi.members(),
        meApi.get().catch(() => null),
        procurementApi.queue().catch(() => [] as ProcurementItemDto[]),
        procurementApi.carts().catch(() => [] as CartDto[]),
        procurementApi.products().catch(() => [] as ProductDto[]),
      ]);
      setFamilyName(fam.name);
      setMembers(mems);
      setMe(who);
      setQueue(items);
      setCarts(activeCarts.filter((c) => c.isActive));
      setProducts(catalog);
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Unable to load family');
    } finally {
      setRefreshing(false);
    }
  }, []);

  const submitPurchase = async () => {
    if (!purchaseItem) return;
    const price = parseFloat(purchasePrice);
    if (!Number.isFinite(price) || price < 0) {
      setError('Enter a valid price');
      return;
    }
    setPurchasing(true);
    try {
      await procurementApi.purchase(purchaseItem.id, price, purchaseStore.trim() || undefined);
      setPurchaseItem(null);
      setPurchasePrice('');
      setPurchaseStore('');
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Purchase failed');
    } finally {
      setPurchasing(false);
    }
  };

  const submitAddItem = async () => {
    if (!itemName.trim()) {
      setError('Item name is required');
      return;
    }
    setAddingItem(true);
    setError(null);
    try {
      const price = parseFloat(itemPrice);
      await procurementApi.createQueueItem({
        name: itemName.trim(),
        preferredStore: itemStore.trim() || undefined,
        estimatedPrice: Number.isFinite(price) && price >= 0 ? price : undefined,
        category: itemCategory || 'Grocery',
      });
      setAddItemOpen(false);
      setItemName('');
      setItemStore('');
      setItemPrice('');
      setItemCategory('Grocery');
      await load();
    } catch (e: unknown) {
      setError((e as { error?: string })?.error ?? 'Could not add item');
    } finally {
      setAddingItem(false);
    }
  };

  useEffect(() => {
    void load();
  }, [load]);

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
        <Text style={typography.caption}>
          Signed in as {me.externalIdentityId} · {me.role}
        </Text>
      ) : null}
      {error ? <Text style={styles.hint}>{error}</Text> : null}

      <View style={styles.toolbar}>
        <SectionTitle>Members</SectionTitle>
        {isAdultOrOwner ? (
          <View style={styles.row}>
            <Button title="Invite" variant="secondary" onPress={() => router.push('/(tabs)/settings')} />
            <Button title="Add member" onPress={() => setAddOpen(true)} />
          </View>
        ) : null}
      </View>
      {isAdultOrOwner && members.filter((m) => m.isActive !== false).length <= 1 ? (
        <Card style={styles.cardCol}>
          <Text style={typography.caption}>
            You&apos;re the only member so far. Invite family from Settings, or add someone by email below.
          </Text>
        </Card>
      ) : null}
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

      <View style={styles.toolbar}>
        <SectionTitle>Shopping by store</SectionTitle>
        {isAdultOrOwner ? <Button title="Add to list" onPress={() => setAddItemOpen(true)} /> : null}
      </View>
      <Text style={[typography.caption, { marginBottom: spacing.sm }]}>
        Queue grouped by preferred store — start a trip to build a cart.
      </Text>
      {isAdultOrOwner ? (
        <View style={[styles.row, { marginBottom: spacing.sm, gap: spacing.sm, flexWrap: 'wrap' }]}>
          <TextInput
            style={[styles.input, { flex: 1, minWidth: 140 }]}
            placeholder="Store for empty cart"
            placeholderTextColor={colors.textMuted}
            value={storeName}
            onChangeText={setStoreName}
          />
          <Button
            title="New cart"
            variant="secondary"
            onPress={() => {
              if (!storeName.trim()) return;
              procurementApi
                .createCart(storeName.trim())
                .then(() => {
                  setStoreName('');
                  return load();
                })
                .catch(() => {});
            }}
          />
        </View>
      ) : null}

      {carts.length > 0 ? (
        <>
          <Text style={[typography.caption, { marginBottom: spacing.xs }]}>Active carts / trips</Text>
          {carts.map((cart) => (
            <Card key={cart.id} style={styles.cardCol}>
              <Text style={typography.heading}>{cart.storeName}</Text>
              <Text style={typography.caption}>{cart.items?.length ?? 0} item(s)</Text>
              {(cart.items ?? []).map((it) => (
                <View key={it.id} style={[styles.row, { justifyContent: 'space-between' }]}>
                  <Text style={typography.body}>{it.name}</Text>
                  {isAdultOrOwner ? (
                    <View style={styles.row}>
                      <Pressable
                        onPress={() => {
                          setPurchasePrice(it.estimatedPrice != null ? String(it.estimatedPrice) : '');
                          setPurchaseStore(cart.storeName);
                          setPurchaseItem(it);
                        }}
                      >
                        <Text style={[typography.caption, { color: colors.primary }]}>Bought</Text>
                      </Pressable>
                      <Pressable onPress={() => procurementApi.removeFromCart(it.id).then(load).catch(() => {})}>
                        <Text style={typography.caption}>Remove</Text>
                      </Pressable>
                    </View>
                  ) : null}
                </View>
              ))}
              {isAdultOrOwner ? (
                <Button
                  title="Abandon cart"
                  variant="secondary"
                  onPress={() => procurementApi.abandonCart(cart.id).then(load).catch(() => {})}
                />
              ) : null}
            </Card>
          ))}
        </>
      ) : null}

      {storeGroups.length === 0 ? (
        <EmptyState title="Queue empty" subtitle="Add items or approve grocery requests." />
      ) : (
        storeGroups.map(([store, items]) => {
          const est = items.reduce((s, i) => s + (i.estimatedPrice ?? 0), 0);
          return (
            <Card key={store} style={styles.cardCol}>
              <View style={styles.rowBetween}>
                <Text style={typography.heading}>{store}</Text>
                <Badge label={`${items.length} item${items.length === 1 ? '' : 's'}`} />
              </View>
              {est > 0 ? <Text style={typography.caption}>Est. total ~${est.toFixed(2)}</Text> : null}
              {items.map((item) => (
                <View key={item.id} style={{ gap: 4, marginTop: spacing.xs }}>
                  <View style={styles.rowBetween}>
                    <Text style={typography.body}>{item.name}</Text>
                    {item.estimatedPrice != null ? (
                      <Text style={typography.caption}>${item.estimatedPrice.toFixed(2)}</Text>
                    ) : null}
                  </View>
                  <View style={styles.row}>
                    <Badge label={item.status} tone="warning" />
                    {item.requestId ? <Badge label="from request" /> : null}
                  </View>
                  {isAdultOrOwner ? (
                    <View style={[styles.row, { flexWrap: 'wrap', gap: spacing.xs }]}>
                      {carts.find((c) => c.storeName.toLowerCase() === store.toLowerCase()) ? (
                        <Button
                          title="Add to cart"
                          variant="secondary"
                          onPress={() => {
                            const c = carts.find((x) => x.storeName.toLowerCase() === store.toLowerCase())!;
                            procurementApi.addToCart(item.id, c.id).then(load).catch(() => {});
                          }}
                        />
                      ) : null}
                      <Button title="Hold" variant="secondary" onPress={() => procurementApi.hold(item.id).then(load).catch(() => {})} />
                      <Button title="Defer" variant="secondary" onPress={() => procurementApi.defer(item.id).then(load).catch(() => {})} />
                      <Button
                        title="Not needed"
                        variant="secondary"
                        onPress={() => procurementApi.noLongerNeeded(item.id).then(load).catch(() => {})}
                      />
                    </View>
                  ) : null}
                </View>
              ))}
              {isAdultOrOwner ? (
                <Button
                  title={startingTrip === store ? 'Starting…' : `Start trip · ${store}`}
                  onPress={() => void startTrip(store, items)}
                />
              ) : null}
            </Card>
          );
        })
      )}

      <SectionTitle>Product catalog</SectionTitle>
      <Text style={[typography.caption, { marginBottom: spacing.sm }]}>
        Prices learned from purchases — typical price and count.
      </Text>
      {products.length === 0 ? (
        <EmptyState title="No products yet" subtitle="Mark cart items Bought to build the catalog." />
      ) : (
        products.map((pr) => (
          <Card key={pr.id} style={styles.cardCol}>
            <Text style={typography.heading}>{pr.name}</Text>
            <View style={styles.row}>
              {pr.brand ? <Badge label={pr.brand} /> : null}
              {pr.category ? <Badge label={pr.category} /> : null}
              {pr.size ? <Badge label={pr.size} /> : null}
            </View>
            <Text style={typography.body}>
              {pr.typicalPrice != null ? `Typical $${pr.typicalPrice.toFixed(2)}` : 'No price yet'}
              {' · '}
              {pr.purchaseCount} purchase{pr.purchaseCount === 1 ? '' : 's'}
            </Text>
          </Card>
        ))
      )}

      <Modal visible={addOpen} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>Add member</Text>
            <TextInput
              style={styles.input}
              placeholder="Display name"
              placeholderTextColor={colors.textMuted}
              value={displayName}
              onChangeText={setDisplayName}
            />
            <TextInput
              style={styles.input}
              placeholder="Email"
              placeholderTextColor={colors.textMuted}
              autoCapitalize="none"
              value={email}
              onChangeText={setEmail}
            />
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

      <Modal visible={addItemOpen} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>Add to shopping list</Text>
            <TextInput style={styles.input} placeholder="Item name" placeholderTextColor={colors.textMuted} value={itemName} onChangeText={setItemName} />
            <TextInput style={styles.input} placeholder="Preferred store (optional)" placeholderTextColor={colors.textMuted} value={itemStore} onChangeText={setItemStore} />
            <TextInput style={styles.input} placeholder="Est. price (optional)" placeholderTextColor={colors.textMuted} keyboardType="decimal-pad" value={itemPrice} onChangeText={setItemPrice} />
            <View style={styles.row}>
              {['Grocery', 'Snack', 'Purchase', 'Household'].map((c) => (
                <Pressable key={c} style={[styles.chip, itemCategory === c && styles.chipOn]} onPress={() => setItemCategory(c)}>
                  <Text style={styles.chipText}>{c}</Text>
                </Pressable>
              ))}
            </View>
            <View style={styles.modalActions}>
              <Button title="Cancel" variant="secondary" onPress={() => setAddItemOpen(false)} />
              <Button title={addingItem ? 'Adding…' : 'Add'} onPress={() => void submitAddItem()} />
            </View>
          </View>
        </View>
      </Modal>

      <Modal visible={!!purchaseItem} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>Mark purchased</Text>
            <Text style={typography.caption}>{purchaseItem?.name}</Text>
            <TextInput style={styles.input} placeholder="Actual price" placeholderTextColor={colors.textMuted} keyboardType="decimal-pad" value={purchasePrice} onChangeText={setPurchasePrice} />
            <TextInput style={styles.input} placeholder="Store" placeholderTextColor={colors.textMuted} value={purchaseStore} onChangeText={setPurchaseStore} />
            <View style={styles.modalActions}>
              <Button title="Cancel" variant="secondary" onPress={() => setPurchaseItem(null)} />
              <Button title={purchasing ? '…' : 'Save'} onPress={() => void submitPurchase()} />
            </View>
          </View>
        </View>
      </Modal>

      <Modal visible={!!roleMember} animationType="slide" transparent>
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <Text style={typography.heading}>Change role · {roleMember?.displayName}</Text>
            <View style={styles.row}>
              {ROLES.filter((r) => r !== 'Owner').map((r) => (
                <Pressable key={r} style={[styles.chip, nextRole === r && styles.chipOn]} onPress={() => setNextRole(r)}>
                  <Text style={styles.chipText}>{r}</Text>
                </Pressable>
              ))}
            </View>
            <View style={styles.modalActions}>
              <Button title="Cancel" variant="secondary" onPress={() => setRoleMember(null)} />
              <Button title={saving ? '…' : 'Save'} onPress={() => void changeRole()} />
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
  cardCol: { marginBottom: spacing.md, gap: spacing.sm },
  row: { flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap', alignItems: 'center' },
  rowBetween: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', gap: spacing.sm },
  hint: { ...typography.caption, color: colors.danger, marginBottom: spacing.sm },
  input: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 8,
    padding: spacing.sm,
    color: colors.text,
    backgroundColor: colors.surface,
  },
  modalBackdrop: { flex: 1, backgroundColor: 'rgba(0,0,0,0.55)', justifyContent: 'flex-end' },
  modalCard: {
    backgroundColor: colors.surface,
    padding: spacing.lg,
    borderTopLeftRadius: 16,
    borderTopRightRadius: 16,
    gap: spacing.sm,
  },
  modalActions: { flexDirection: 'row', gap: spacing.sm, justifyContent: 'flex-end' },
  chip: {
    paddingVertical: spacing.xs,
    paddingHorizontal: spacing.sm,
    borderRadius: 8,
    backgroundColor: colors.bg,
    borderWidth: 1,
    borderColor: colors.border,
  },
  chipOn: { borderColor: colors.primary, backgroundColor: '#1E293B' },
  chipText: { ...typography.caption, color: colors.text },
});
