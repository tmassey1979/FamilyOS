import { StyleSheet, Text, View } from 'react-native';
import { Card, EmptyState, SectionTitle } from '../../src/components/ui';
import { colors, spacing, typography } from '../../src/theme';

export default function CalendarScreen() {
  return (
    <View style={styles.container}>
      <SectionTitle>Today</SectionTitle>
      <Card style={styles.card}>
        <Text style={typography.heading}>3:30 PM · Pick up Eli</Text>
        <Text style={typography.caption}>Lincoln Elementary</Text>
      </Card>
      <Card style={styles.card}>
        <Text style={typography.heading}>6:00 PM · Dinner</Text>
      </Card>
      <SectionTitle>Upcoming</SectionTitle>
      <EmptyState title="Calendar API next" subtitle="Wire to /api/calendar when endpoints land." />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg, padding: spacing.md },
  card: { marginBottom: spacing.md, gap: spacing.xs },
});
