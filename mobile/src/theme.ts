export const colors = {
  bg: '#0F172A',
  surface: '#1E293B',
  border: '#334155',
  primary: '#38BDF8',
  text: '#F8FAFC',
  textMuted: '#94A3B8',
  danger: '#F87171',
  success: '#4ADE80',
  warning: '#FBBF24',
} as const;

export type ColorName = keyof typeof colors;

export const spacing = {
  xs: 4,
  sm: 8,
  md: 16,
  lg: 24,
  xl: 32,
} as const;

export const typography = {
  title: { fontSize: 28, fontWeight: '700' as const, color: colors.text },
  heading: { fontSize: 18, fontWeight: '600' as const, color: colors.text },
  body: { fontSize: 16, fontWeight: '400' as const, color: colors.text },
  caption: { fontSize: 13, fontWeight: '400' as const, color: colors.textMuted },
} as const;
