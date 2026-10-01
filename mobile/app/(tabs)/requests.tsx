import { StyleSheet, Text, View } from 'react-native';
import { Button, Card, EmptyState, SectionTitle } from '../../src/components/ui';
import { colors, spacing, typography } from '../../src/theme';

const REQUEST_TYPES = [
  'Ride', 'Money', 'Purchase', 'Groceries', 'Food',
  'School', 'Medical', 'Household', 'Personal', 'Something else',
];

export default function RequestsScreen() {
  return (
    <View style={styles.container}>
      <SectionTitle>What do you need?</SectionTitle>
      <Text style={[typography.caption, { marginBottom: spacing.md }]}>
        Pick a type — questions are loaded dynamically from the API.
      </Text>
      <View style={styles.grid}>
        {REQUEST_TYPES.map((t) => (
          <Card key={t} style={styles.chip}>
            <Text style={typography.body}>{t}</Text>
          </Card>
        ))}
      </View>
      <SectionTitle>My requests</SectionTitle>
      <EmptyState title="No open requests" subtitle="New requests will show here after submit." />
      <Button title="Open approval queue" variant="secondary" onPress={() => {}} />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg, padding: spacing.md },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, marginBottom: spacing.lg },
  chip: { paddingVertical: spacing.sm, paddingHorizontal: spacing.md, minWidth: '45%' },
});
