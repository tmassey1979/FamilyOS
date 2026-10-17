/**
 * PostHog: feature flags, analytics, exception capture.
 * No-ops when EXPO_PUBLIC_POSTHOG_KEY is unset.
 */
import PostHog from 'posthog-react-native';

type Props = Record<string, string | number | boolean | null | undefined>;

let client: PostHog | null = null;

const KEY = process.env.EXPO_PUBLIC_POSTHOG_KEY ?? '';
const HOST = process.env.EXPO_PUBLIC_POSTHOG_HOST ?? 'https://us.i.posthog.com';

export function isPostHogEnabled(): boolean {
  return KEY.length > 0;
}

export async function initPostHog(): Promise<void> {
  if (!KEY || client) return;
  try {
    client = new PostHog(KEY, {
      host: HOST,
      captureAppLifecycleEvents: true,
      preloadFeatureFlags: true,
    });
  } catch {
    client = null;
  }
}

export function identifyUser(distinctId: string, props?: Props): void {
  client?.identify(distinctId, props);
}

export function capture(event: string, props?: Props): void {
  client?.capture(event, props);
}

export function isFeatureEnabled(flag: string, defaultValue = false): boolean {
  if (!client) return defaultValue;
  try {
    const v = client.isFeatureEnabled(flag);
    if (v === undefined) return defaultValue;
    return !!v;
  } catch {
    return defaultValue;
  }
}

export function getFeatureFlag(flag: string): string | boolean | undefined {
  try {
    return client?.getFeatureFlag(flag);
  } catch {
    return undefined;
  }
}

export function captureException(error: unknown, props?: Props): void {
  if (!client) return;
  try {
    const anyClient = client as PostHog & {
      captureException?: (err: unknown, p?: Props) => void;
    };
    if (typeof anyClient.captureException === 'function') {
      anyClient.captureException(error, props);
      return;
    }
    const message =
      error instanceof Error ? error.message : typeof error === 'string' ? error : 'Unknown error';
    const stack = error instanceof Error ? error.stack : undefined;
    client.capture('$exception', {
      ...props,
      $exception_message: message,
      $exception_stack_trace_raw: stack,
    });
  } catch {
    /* swallow */
  }
}
