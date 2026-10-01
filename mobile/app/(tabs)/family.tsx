import { StyleSheet, Text, View } from 'react-native';
import { Badge, Card, SectionTitle } from '../../src/components/ui';
import { colors, spacing, typography } from '../../src/theme';

const MEMBERS = [
  { name: 'Terry', role: 'Owner' },
  { name: 'Michelle', role: 'Adult' },
  { name: 'Mia', role: 'Teen' },
  { name: 'Eli', role: 'Child' },
];

export default function FamilyScreen() {
  return (
    <View style={styles.container}>
      <Text style={typography.title}>The Hendersons</Text>
      <SectionTitle>Members</SectionTitle>
      {MEMBERS.map((m) => (
        <Card key={m.name} style={styles.card}>
          <Text style={typography.heading}>{m.name}</Text>
          <Badge label={m.role} />
        </Card>
      ))}
      <Text style={[typography.caption, { marginTop: spacing.lg }]}>
        Roles and permissions are enforced on the API — UI never is the security boundary.
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg, padding: spacing.md },
  card: { marginBottom: spacing.sm, flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
});
