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

/** Local fallbacks so typecheck never depends on optional theme keys. */
const C = {
  bg: colors.bg,
  surface: colors.surface,
  surfaceAlt: (colors as { surfaceAlt?: string }).surfaceAlt ?? '#334155',
  border: colors.border,
  primary: colors.primary,
  text: colors.text,
  textMuted: colors.textMuted,
  danger: colors.danger,
  success: colors.success,
  warning: (colors as { warning?: string }).warning ?? '#FBBF24',
};

type ButtonProps = {
  title: string;
  onPress?: () => void;
  variant?: 'primary' | 'secondary' | 'danger';
  disabled?: boolean;
  loading?: boolean;
};

export function Button({ title, onPress, variant = 'primary', disabled, loading }: ButtonProps) {
  const bg =
    variant === 'primary' ? C.primary : variant === 'danger' ? C.danger : C.surfaceAlt;
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
        <ActivityIndicator color={C.bg} />
      ) : (
        <Text style={styles.buttonText}>{title}</Text>
      )}
    </Pressable>
  );
}

export function Card({ children, style }: { children: React.ReactNode; style?: ViewStyle }) {
  return <View style={[styles.card, style]}>{children}</View>;
}

export function Badge({
  label,
  tone = 'default',
}: {
  label: string;
  tone?: 'default' | 'success' | 'warning' | 'danger';
}) {
  const bg =
    tone === 'success'
      ? C.success
      : tone === 'warning'
        ? C.warning
        : tone === 'danger'
          ? C.danger
          : C.surfaceAlt;
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
    color: C.bg,
  },
  card: {
    backgroundColor: C.surface,
    borderRadius: 16,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: C.border,
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
    color: C.text,
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
