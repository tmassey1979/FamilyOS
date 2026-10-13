# Portainer continuous deploy (AWS)

Backend runs on EC2 behind Traefik; Portainer manages the **familyos** Git stack.

## Live endpoints

| Service | URL |
|---------|-----|
| API health | https://api.18.226.226.118.sslip.io/health |
| Keycloak | https://auth.18.226.226.118.sslip.io |
| Portainer | https://portainer.18.226.226.118.sslip.io |

## Stack identity

| Field | Value |
|-------|-------|
| Stack name | `familyos` |
| Stack id | `2` |
| Endpoint id | `2` (`local`) |
| Compose path | `deploy/familyos/docker-compose.yml` |
| Git branch | `main` |

## GitHub secrets

| Secret | Required | Notes |
|--------|----------|-------|
| `PORTAINER_WEBHOOK_URL` | **Preferred** | Full webhook URL from Portainer |
| `PORTAINER_URL` | Fallback | `https://portainer.18.226.226.118.sslip.io` |
| `PORTAINER_API_KEY` | Fallback | Access token (rotate if leaked) |
| `PORTAINER_STACK_ID` | Fallback | `2` |
| `PORTAINER_ENDPOINT_ID` | Fallback | `2` |

## Enable webhook (recommended, one-time)

1. Open Portainer → **Stacks** → **familyos**
2. **Editor** (or Git details) → **Automatic updates / GitOps**
3. Enable **Webhook**
4. Copy the webhook URL
5. GitHub → Settings → Secrets → Actions → `PORTAINER_WEBHOOK_URL` = that URL

CI on every push to `main` POSTs the webhook after a green API build.

## How CI deploys

1. `CI` → Build & Test (API)
2. On `main` only → `Deploy stack (Portainer)`
   - Tries **webhook** first
   - Falls back to **API git/redeploy** with stored stack Env and `PullImage: false`
3. Health-checks `https://api.18.226.226.118.sslip.io/health`

## Manual redeploy

Actions → **Deploy stack (Portainer)** → Run workflow

## Notes

- `familyos-api` is **built on the host**, not pulled from a registry — do not force `PullImage: true` unless you publish the image.
- Env vars (Postgres, RabbitMQ, Keycloak) live on the Portainer stack; API redeploy must include them.
