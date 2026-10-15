import { Clipboard, Platform, Share } from 'react-native';

export function inviteDeepLink(code: string) {
  return `familyos://invite/${code}`;
}

export function inviteShareMessage(code: string, familyName?: string) {
  const title = familyName ? `Join ${familyName} on Family OS` : 'Join our household on Family OS';
  return `${title}\n\nInvite code: ${code}\n\nOpen in app: ${inviteDeepLink(code)}`;
}

/** Copy code (+ optional deep link) to the clipboard. */
export async function copyInviteCode(code: string): Promise<void> {
  const text = `${code}\n${inviteDeepLink(code)}`;
  try {
    // RN Clipboard is available on both platforms in this Expo SDK
    Clipboard.setString(text);
  } catch {
    /* ignore */
  }
}

/** Open the OS share sheet with code + deep link. */
export async function shareInviteCode(code: string, familyName?: string) {
  const title = familyName ? `Join ${familyName} on Family OS` : 'Join our household on Family OS';
  const message = inviteShareMessage(code, familyName);
  try {
    await Share.share(
      Platform.OS === 'ios' ? { message, url: inviteDeepLink(code) } : { message, title },
    );
  } catch {
    /* user dismissed */
  }
}
