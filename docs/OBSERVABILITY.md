# Observability (Prometheus, Grafana, PostHog)

## Prometheus + Grafana

- API exposes **Prometheus** metrics at `GET /metrics` (no auth; keep on internal network).
- `prometheus-net.AspNetCore` records HTTP request duration/count via `UseHttpMetrics`.
- Compose services (optional, memory-heavy on micro instances):
  - `prometheus` scrapes `api:5080/metrics`
  - `grafana` at `https://grafana.${DOMAIN}` (default admin password from env)

Enable by deploying the updated `deploy/familyos/docker-compose.yml`. On ≤2GB RAM VMs, prefer leaving Grafana off or use a larger instance.

### Env

```
GRAFANA_ADMIN_PASSWORD=change-me
```

## PostHog

Used for **feature flags**, **product analytics**, and **exception capture**.

### API

```
PostHog__ApiKey=phc_...
PostHog__Host=https://us.i.posthog.com
```

When set, unhandled **5xx** exceptions are sent as `$exception` events.

### Mobile

```
EXPO_PUBLIC_POSTHOG_KEY=phc_...
EXPO_PUBLIC_POSTHOG_HOST=https://us.i.posthog.com
```

`mobile/src/analytics/posthog.ts`:
- `initPostHog()` on app boot
- `isFeatureEnabled(flag)` / `getFeatureFlag(flag)`
- `captureException(error)` + `capture(event, props)`

Without keys, all calls no-op so local/dev stays quiet.

### Feature flags

Create flags in the PostHog project (e.g. `meals_recipes_v2`, `outcomes_charts`). Gate UI with `isFeatureEnabled`.
