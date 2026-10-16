import { Platform, Share } from 'react-native';

export function inviteDeepLink(code: string) {
  return `familyos://invite/${code}`;
}

export function inviteShareMessage(code: string, familyName?: string) {
  const title = familyName ? `Join ${familyName} on Family OS` : 'Join our household on Family OS';
  return `${title}\n\nInvite code: ${code}\n\nOpen in app: ${inviteDeepLink(code)}`;
}

function tryClipboardSet(text: string): boolean {
  try {
    // RN 0.76+ may not export Clipboard; optional require keeps typecheck happy
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    const rn = require('react-native') as { Clipboard?: { setString?: (s: string) => void } };
    if (rn.Clipboard?.setString) {
      rn.Clipboard.setString(text);
      return true;
    }
  } catch {
    /* ignore */
  }
  return false;
}

/** Copy code (+ deep link) to the clipboard when possible. */
export async function copyInviteCode(code: string): Promise<boolean> {
  const text = `${code}\n${inviteDeepLink(code)}`;
  if (tryClipboardSet(text)) return true;
  // Fallback: open share sheet so user can copy manually
  try {
    await Share.share({ message: text });
    return true;
  } catch {
    return false;
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
