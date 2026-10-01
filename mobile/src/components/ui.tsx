import React from 'react';
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  View,
  ViewStyle,
} from 'react-native';
import { colors, spacing, typography } from '../theme';

type ButtonProps = {
  title: string;
  onPress?: () => void;
  variant?: 'primary' | 'secondary' | 'danger';
  disabled?: boolean;
  loading?: boolean;
};

export function Button({ title, onPress, variant = 'primary', disabled, loading }: ButtonProps) {
  const bg =
    variant === 'primary' ? colors.primary : variant === 'danger' ? colors.danger : colors.surfaceAlt;
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={title}
      onPress={onPress}
      disabled={disabled || loading}
      style={({ pressed }) => [
        styles.button,
        { backgroundColor: bg, opacity: pressed || disabled ? 0.7 : 1 },
      ]}
    >
      {loading ? (
        <ActivityIndicator color={colors.bg} />
      ) : (
        <Text style={styles.buttonText}>{title}</Text>
      )}
    </Pressable>
  );
}

export function Card({ children, style }: { children: React.ReactNode; style?: ViewStyle }) {
  return <View style={[styles.card, style]}>{children}</View>;
}

export function Badge({ label, tone = 'default' }: { label: string; tone?: 'default' | 'success' | 'warning' | 'danger' }) {
  const bg =
    tone === 'success'
      ? colors.success
      : tone === 'warning'
        ? colors.warning
        : tone === 'danger'
          ? colors.danger
          : colors.surfaceAlt;
  return (
    <View style={[styles.badge, { backgroundColor: bg }]}>
      <Text style={styles.badgeText}>{label}</Text>
    </View>
  );
}

export function EmptyState({ title, subtitle }: { title: string; subtitle?: string }) {
  return (
    <View style={styles.empty} accessibilityRole="summary">
      <Text style={typography.heading}>{title}</Text>
      {subtitle ? <Text style={[typography.caption, { marginTop: spacing.sm }]}>{subtitle}</Text> : null}
    </View>
  );
}

export function SectionTitle({ children }: { children: string }) {
  return <Text style={[typography.caption, styles.section]}>{children.toUpperCase()}</Text>;
}

const styles = StyleSheet.create({
  button: {
    minHeight: 48,
    borderRadius: 12,
    paddingHorizontal: spacing.lg,
    alignItems: 'center',
    justifyContent: 'center',
  },
  buttonText: {
    ...typography.body,
    fontWeight: '600',
    color: colors.bg,
  },
  card: {
    backgroundColor: colors.surface,
    borderRadius: 16,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.border,
  },
  badge: {
    alignSelf: 'flex-start',
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    borderRadius: 8,
  },
  badgeText: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text,
  },
  empty: {
    padding: spacing.xl,
    alignItems: 'center',
  },
  section: {
    marginBottom: spacing.sm,
    letterSpacing: 1,
  },
});
