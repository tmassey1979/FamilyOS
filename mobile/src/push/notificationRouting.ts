import * as Notifications from 'expo-notifications';

/** Map notification payload → Expo Router path (in-app deep link). */
export function routeForNotificationData(
  data: Record<string, unknown> | undefined | null,
): string {
  const kind = String(data?.relatedEntityType ?? data?.type ?? '').toLowerCase();
  if (kind.includes('request')) return '/(tabs)/requests';
  if (kind.includes('task')) return '/(tabs)/tasks';
  if (
    kind.includes('procure') ||
    kind.includes('product') ||
    kind.includes('cart') ||
    kind.includes('shop')
  )
    return '/(tabs)/family';
  if (kind.includes('calendar') || kind.includes('event')) return '/(tabs)/calendar';
  if (kind.includes('chat')) return '/(tabs)/chat';
  if (kind.includes('meal')) return '/(tabs)/meals';
  return '/(tabs)';
}

/**
 * Listen for notification taps (foreground/background) and cold-start last response.
 * Returns unsubscribe.
 */
export function setupNotificationNavigation(navigate: (href: string) => void): () => void {
  const sub = Notifications.addNotificationResponseReceivedListener((response) => {
    const data = response.notification.request.content.data as
      | Record<string, unknown>
      | undefined;
    navigate(routeForNotificationData(data));
  });

  void Notifications.getLastNotificationResponseAsync().then((response) => {
    if (!response) return;
    const data = response.notification.request.content.data as
      | Record<string, unknown>
      | undefined;
    navigate(routeForNotificationData(data));
  });

  return () => sub.remove();
}
