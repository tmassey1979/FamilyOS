# Portainer continuous deploy (AWS)

Backend runs on EC2 behind Traefik; Portainer manages the **familyos** Git stack.

## Live endpoints

| Service | URL |
|---------|-----|
| API health | https://api.18.226.226.118.sslip.io/health |
| Keycloak | https://auth.18.226.226.118.sslip.io |
| Portainer | https://portainer.18.226.226.118.sslip.io |

## Free HTTPS certificates (Let's Encrypt)

- Traefik obtains and renews trusted TLS certificates automatically using the free Let's Encrypt ACME HTTP-01 challenge.
- `deploy/traefik/docker-compose.yml` passes the ACME email, persistent certificate store (`traefik_letsencrypt`), and HTTP challenge entrypoint to Traefik.
- Set `ACME_EMAIL` in `deploy/traefik/.env` before starting or updating the edge stack. Use an email address you can access for expiry/security notices.
- Keep inbound TCP ports **80 and 443** open in the AWS security group and host firewall. Port 80 is required for HTTP-01 validation; Traefik redirects ordinary HTTP traffic to HTTPS after the challenge is handled.
- The existing `*.18.226.226.118.sslip.io` service hostnames can be used without buying a domain, provided public DNS resolves to this EC2 host and Let's Encrypt can reach port 80.
- Never delete the `traefik_letsencrypt` volume; it stores ACME account and certificate state.

Create the local environment file:

```bash
cp deploy/traefik/.env.example deploy/traefik/.env
# Edit ACME_EMAIL, DOMAIN, and any other required values
```

Then redeploy the edge stack from the directory containing the compose file so its relative mounts resolve correctly:

```bash
docker compose --env-file deploy/traefik/.env -f deploy/traefik/docker-compose.yml up -d
```

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

The coordinated `FamilyOS Coordinated Release` workflow calls the webhook only after the API tests, both GHCR image builds, and the Android APK build/verification have succeeded. It publishes the GitHub APK release only after the API and Keycloak health checks pass.

## Keycloak production database and realm

- PostgreSQL remains a single server/container with the existing `postgres_data` volume.
- Family OS API continues using the `familyos` database (`POSTGRES_DB`).
- A one-shot `keycloak-db-init` service idempotently creates a separate `keycloak` database on that same PostgreSQL server. It also works when the volume already exists; it does not reset or recreate the Family OS database.
- Keycloak uses the published `ghcr.io/tmassey1979/familyos-keycloak:latest` image, connects to `postgres:5432/keycloak`, and imports the `familyos` realm on first creation. Keycloak keeps its data in PostgreSQL, not its development H2 database.
- Keycloak's realm import is create-if-missing; it does not overwrite an already existing realm on later deploys. Existing Keycloak users/realm data are not deleted by this configuration.
- The checked-in realm no longer seeds shared `password` demo accounts. New users register through the enabled realm registration flow.

## How CI deploys

Every push to `main` starts one **FamilyOS Coordinated Release**:

1. Restore, build, and test the .NET solution.
2. Build and publish the API and Keycloak images to GHCR, tagged with the source commit and `latest`.
3. Typecheck and validate the Expo Android app, build the production APK with EAS, download it, and verify the APK archive.
4. Only after both image builds and the APK build succeed, redeploy the Portainer stack.
5. Require HTTP 200 from both the API health endpoint and Keycloak realm discovery endpoint.
6. Only after both health checks pass, publish the APK as a GitHub Release.

A failed build or health check prevents the coordinated GitHub Release from being published. The older standalone API/Keycloak image workflows are manual-only; they do not trigger partial automatic deployments.

## Manual redeploy

Actions → **Deploy stack (Portainer)** → Run workflow

## Notes

- Both `familyos-api` and `familyos-keycloak` are published to GHCR by the coordinated workflow and pulled by Portainer during redeployment.
- Env vars (Postgres, RabbitMQ, Keycloak) live on the Portainer stack; API redeploy must include them.
