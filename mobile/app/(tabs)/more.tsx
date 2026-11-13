import { useRouter } from 'expo-router';
import { CalendarCheck, ChevronRight, ClipboardList, MessageCircle, Settings, Utensils } from 'lucide-react-native';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Card, SectionTitle } from '../../src/components/ui';
import { colors, spacing, typography } from '../../src/theme';
import { withObserveScreen } from '../../src/analytics/withObserveScreen';

const LINKS = [
  { title: 'Requests & approvals', description: 'Ask for something, review requests, and track decisions.', route: '/(tabs)/requests', Icon: ClipboardList },
  { title: 'Meals', description: 'Plan meals and keep the household food plan together.', route: '/(tabs)/meals', Icon: Utensils },
  { title: 'Family chat', description: 'Keep household conversations in one place.', route: '/(tabs)/chat', Icon: MessageCircle },
  { title: 'Settings', description: 'Notifications, invites, quiet hours, and account settings.', route: '/(tabs)/settings', Icon: Settings },
] as const;

function MoreScreen() {
  const router = useRouter();

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      <SectionTitle>More from FamilyOS</SectionTitle>
      <Text style={typography.heading}>Everything your family needs, in one place.</Text>
      {LINKS.map(({ title, description, route, Icon }) => (
        <Pressable key={route} accessibilityRole="button" onPress={() => router.push(route)}>
          <Card style={styles.card}>
            <View style={styles.iconWrap}>
              <Icon color={colors.primary} size={22} strokeWidth={2} />
            </View>
            <View style={styles.copy}>
              <Text style={styles.title}>{title}</Text>
              <Text style={styles.description}>{description}</Text>
            </View>
            <ChevronRight color={colors.textMuted} size={20} />
          </Card>
        </Pressable>
      ))}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg },
  content: { padding: spacing.md, paddingBottom: spacing.xl * 2, gap: spacing.md },
  card: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  iconWrap: { width: 42, height: 42, borderRadius: 12, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.surfaceAlt },
  copy: { flex: 1, gap: 3 },
  title: { ...typography.body, fontWeight: '700' },
  description: { ...typography.caption, lineHeight: 18 },
});

export default withObserveScreen(MoreScreen);