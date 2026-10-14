import { Platform } from 'react-native';
import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import * as SecureStore from 'expo-secure-store';

const DEVICE_TOKEN_KEY = 'familyos.device.localToken';
const EXPO_PUSH_TOKEN_KEY = 'familyos.device.expoPushToken';

Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowAlert: true,
    shouldPlaySound: true,
    shouldSetBadge: true,
  }),
});

async function getOrCreateLocalDeviceToken(): Promise<string> {
  const existing = await SecureStore.getItemAsync(DEVICE_TOKEN_KEY);
  if (existing) return existing;
  const token = `local-${Platform.OS}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
  await SecureStore.setItemAsync(DEVICE_TOKEN_KEY, token);
  return token;
}

/**
 * Prefer a real Expo push token (ExponentPushToken[…] / ExpoPushToken[…]).
 * Falls back to a local-* id so Settings can still exercise device registration in simulators.
 */
export async function resolveDevicePushToken(): Promise<{
  token: string;
  isExpoPush: boolean;
  platform: string;
  deviceName: string;
}> {
  const platform = Platform.OS;
  const deviceName = Device.modelName
    ? `${Device.modelName}`
    : `${platform} device`;

  // Physical device preferred; still try on simulators (may fail).
  try {
    const { status: existing } = await Notifications.getPermissionsAsync();
    let finalStatus = existing;
    if (existing !== 'granted') {
      const { status } = await Notifications.requestPermissionsAsync();
      finalStatus = status;
    }
    if (finalStatus === 'granted') {
      const projectId =
        Constants.easConfig?.projectId ??
        (Constants.expoConfig?.extra as { eas?: { projectId?: string } } | undefined)?.eas?.projectId;
      const push = await Notifications.getExpoPushTokenAsync(
        projectId ? { projectId } : undefined,
      );
      if (push?.data) {
        await SecureStore.setItemAsync(EXPO_PUSH_TOKEN_KEY, push.data);
        return { token: push.data, isExpoPush: true, platform, deviceName };
      }
    }
  } catch {
    /* Expo Go without project / simulator — fall through */
  }

  const cached = await SecureStore.getItemAsync(EXPO_PUSH_TOKEN_KEY);
  if (cached?.startsWith('ExponentPushToken[') || cached?.startsWith('ExpoPushToken[')) {
    return { token: cached, isExpoPush: true, platform, deviceName };
  }

  const local = await getOrCreateLocalDeviceToken();
  return { token: local, isExpoPush: false, platform, deviceName };
}

export async function getStoredDeviceToken(): Promise<string | null> {
  return (
    (await SecureStore.getItemAsync(EXPO_PUSH_TOKEN_KEY)) ??
    (await SecureStore.getItemAsync(DEVICE_TOKEN_KEY))
  );
}
