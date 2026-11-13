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

## Keycloak production database and realm

- PostgreSQL remains a single server/container with the existing `postgres_data` volume.
- Family OS API continues using the `familyos` database (`POSTGRES_DB`).
- A one-shot `keycloak-db-init` service idempotently creates a separate `keycloak` database on that same PostgreSQL server. It also works when the volume already exists; it does not reset or recreate the Family OS database.
- Keycloak uses the published `ghcr.io/tmassey1979/familyos-keycloak:latest` image, connects to `postgres:5432/keycloak`, and imports the `familyos` realm on first creation. Keycloak keeps its data in PostgreSQL, not its development H2 database.
- Keycloak's realm import is create-if-missing; it does not overwrite an already existing realm on later deploys. Existing Keycloak users/realm data are not deleted by this configuration.
- The checked-in realm no longer seeds shared `password` demo accounts. New users register through the enabled realm registration flow.

## How CI deploys

1. Changes to `docker/keycloak/**` or `deploy/familyos/keycloak/**` build and publish the Keycloak image to GHCR.
2. After a successful **Publish Keycloak image** run, `Deploy stack (Portainer)` automatically redeploys the stack and pulls the new image.
3. Portainer deployment tries the **webhook** first, then falls back to **API git/redeploy** with stored stack environment values and `PullImage: true`.
4. The workflow health-checks `https://api.18.226.226.118.sslip.io/health`.

## Manual redeploy

Actions → **Deploy stack (Portainer)** → Run workflow

## Notes

- `familyos-api` is **built on the host**, not pulled from a registry — do not force `PullImage: true` unless you publish the image.
- Env vars (Postgres, RabbitMQ, Keycloak) live on the Portainer stack; API redeploy must include them.
