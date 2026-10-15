import { Share } from 'react-native';

/** Open the OS share sheet with code + deep link. */
export async function shareInviteCode(code: string, familyName?: string) {
  const title = familyName ? `Join ${familyName} on Family OS` : 'Join our household on Family OS';
  const message = `${title}\n\nInvite code: ${code}\n\nOpen in app: familyos://invite/${code}`;
  try {
    await Share.share({ message, title });
  } catch {
    /* user dismissed */
  }
}
