declare module 'posthog-react-native' {
  export default class PostHog {
    constructor(
      apiKey: string,
      options?: {
        host?: string;
        captureAppLifecycleEvents?: boolean;
        preloadFeatureFlags?: boolean;
      },
    );
    capture(event: string, properties?: Record<string, unknown>): void;
    identify(distinctId: string, properties?: Record<string, unknown>): void;
    isFeatureEnabled(key: string): boolean | undefined;
    getFeatureFlag(key: string): string | boolean | undefined;
    captureException?(error: unknown, properties?: Record<string, unknown>): void;
    shutdown?(): Promise<void>;
  }
}
