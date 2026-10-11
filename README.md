# Family OS

**Make the next meaningful action obvious.**

Household management app: ASP.NET Core API + React Native (Expo) mobile, Postgres, RabbitMQ, Keycloak.

## Quick start (local Docker)

```bash
git clone https://github.com/tmassey1979/FamilyOS.git
cd FamilyOS
chmod +x scripts/dev-up.sh
./scripts/dev-up.sh
```

Or: `docker compose up --build -d`

| Service | URL |
|---------|-----|
| API / Swagger | http://localhost:5080/swagger |
| Health | http://localhost:5080/health |
| Keycloak | http://localhost:8080 (admin / admin) |
| RabbitMQ | http://localhost:15672 |
| Postgres | localhost:5432 |

Dev auth: send header `X-Dev-User: terry.owner` (or `michelle.adult`, `mia.teen`, `eli.child`).

---

## Production (AWS + Traefik + Portainer)

**Live host (no purchased domain — sslip.io):**

| Service | URL |
|---------|-----|
| API health | https://api.18.226.226.118.sslip.io/health |
| **Swagger** | https://api.18.226.226.118.sslip.io/swagger |
| Keycloak | https://auth.18.226.226.118.sslip.io |
| Portainer | https://portainer.18.226.226.118.sslip.io |
| Traefik | https://traefik.18.226.226.118.sslip.io |

EC2: `ec2-18-226-226-118.us-east-2.compute.amazonaws.com` · private `172.31.3.91`  
App path on server: `/opt/familyos`

### Bootstrap a new server

```bash
curl -fsSL https://raw.githubusercontent.com/tmassey1979/FamilyOS/main/deploy/setup-server.sh -o /tmp/setup.sh
sudo bash /tmp/setup.sh --domain 18.226.226.118.sslip.io --email you@example.com
```

See `deploy/AWS.md` for security groups (22, 80, 443), swap, and DNS.

### Compose layouts

| Path | Purpose |
|------|---------|
| `deploy/traefik/` | Traefik + Portainer (`proxy` network) |
| `deploy/familyos/` | API, Postgres, RabbitMQ, Keycloak |
| `deploy/familyos/.env` | Secrets on server only (not in git) |
| `deploy/traefik/dynamic/routes.yml` | File routes (Docker Engine 29 + Traefik) |

Mobile points at production API via `EXPO_PUBLIC_API_URL` (see `mobile/eas.json`).

---

## Continuous deploy (GitHub Actions → Portainer)

After a **green build on `main`** and after **Release**, Actions runs **Deploy stack (Portainer)**.

Workflow: `.github/workflows/deploy-stack.yml` · details: `deploy/PORTAINER_CD.md`

| Trigger | Redeploy? |
|---------|-----------|
| Push to **`main`** (CI after build-and-test) | Yes |
| **Release** workflow | Yes |
| `develop` / PRs | No |

### GitHub Actions secrets

**Portainer (preferred)**

| Secret | Value |
|--------|--------|
| `PORTAINER_WEBHOOK_URL` | Stack webhook URL from Portainer |
| *or* `PORTAINER_URL` + `PORTAINER_API_KEY` + `PORTAINER_STACK_ID` | API redeploy |
| `PORTAINER_ENDPOINT_ID` | Usually `1` or `3` |

**SSH fallback**

| Secret | Value |
|--------|--------|
| `DEPLOY_SSH_HOST` | `ubuntu@18.226.226.118` |
| `DEPLOY_SSH_KEY` | PEM private key |

**Mobile / EAS**

| Secret | Value |
|--------|--------|
| `EXPO_TOKEN` | Expo access token (https://expo.dev/settings/access-tokens) |

Until Portainer/SSH secrets exist, the deploy job **warns and skips** (does not fail CI).

---

## Mobile (Expo / EAS)

- Project: `@tmassey1979/familyos` · projectId in `mobile/app.json` → `extra.eas.projectId`
- Profiles: `development`, `preview`, `production` (AAB), `production-apk` (APK)
- Production env embeds API: `https://api.18.226.226.118.sslip.io` + `EXPO_PUBLIC_DEV_USER=terry.owner`

```bash
cd mobile
npm ci
# one-time: ensure EXPO_TOKEN in GH secrets, then run Actions → "Generate mobile build config"
npx eas-cli build --profile production-apk --platform android
```

See `mobile/EAS.md`.

**Generate mobile build JSON (one-time / on-demand):** Actions → **Generate mobile build config** → Run workflow.  
Produces artifact `mobile-build-config` (`expo-config.*.json`, `eas.json`, `mobile-build-summary.json`).

---

## Versioning & CI

- **CI** (`ci.yml`): restore, build, test, coverage; mobile typecheck; on `main` → Portainer redeploy
- **Release** (`release.yml`): version bump, API zip, EAS Android AAB+APK when `EXPO_TOKEN` set, Portainer redeploy
- **Deploy stack**: manual or called from CI/Release
- **Generate mobile build config**: Expo resolved config + EAS configure

Docs: `ROADMAP.md`, `FEATURE_STORIES.md`, `deploy/AWS.md`, `deploy/PORTAINER_CD.md`
