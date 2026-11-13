import { useEffect, type ComponentType } from 'react';
import { useObserve } from 'expo-observe';

/** Adds a per-route time-to-interactive measurement after the screen commits. */
export function withObserveScreen<P extends object>(Screen: ComponentType<P>): ComponentType<P> {
  function ObservedScreen(props: P) {
    const { markInteractive } = useObserve();

    useEffect(() => {
      markInteractive();
    }, [markInteractive]);

    return <Screen {...props} />;
  }

  ObservedScreen.displayName = `Observed(${Screen.displayName || Screen.name || 'Screen'})`;
  return ObservedScreen;
}