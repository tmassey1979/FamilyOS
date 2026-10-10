# Continuous deploy via Portainer + GitHub Actions

After **CI** (push to `main`) and **Release** succeed, GitHub Actions calls Portainer to redeploy the Family OS stack.

Workflow: `.github/workflows/deploy-stack.yml`

## Option A — Stack webhook (recommended)

1. Open https://portainer.YOUR_DOMAIN  
2. **Stacks** → add/import the Family OS stack if it is not listed:
   - Build method: **Repository**
   - Repository URL: `https://github.com/tmassey1979/FamilyOS`
   - Compose path: `deploy/familyos/docker-compose.yml`
   - Auto-update / webhook: enable  
3. Open the stack → **Create webhook** (or Webhooks) → copy the URL  
4. GitHub repo → **Settings → Secrets and variables → Actions** → New secret:

| Secret | Value |
|--------|--------|
| `PORTAINER_WEBHOOK_URL` | full webhook URL from Portainer |

Redeploy is a single `POST` to that URL (pull + recreate).

## Option B — Portainer API

1. Portainer → your user → **Access tokens** → create token  
2. Stacks → note numeric **stack id** (URL or details)  
3. Endpoints → note **endpoint id** (local Docker is often `1` or `3`)  

| Secret | Example |
|--------|---------|
| `PORTAINER_URL` | `https://portainer.18.226.226.118.sslip.io` |
| `PORTAINER_API_KEY` | token string |
| `PORTAINER_STACK_ID` | `2` |
| `PORTAINER_ENDPOINT_ID` | `1` |

Uses `PUT /api/stacks/{id}/git/redeploy`.

## Option C — SSH fallback

Used only if webhook/API secrets are missing or fail.

| Secret | Example |
|--------|---------|
| `DEPLOY_SSH_HOST` | `ubuntu@18.226.226.118` |
| `DEPLOY_SSH_KEY` | full PEM private key |

Runs on the host:

```bash
cd /opt/familyos && git pull &&
docker compose -f deploy/familyos/docker-compose.yml --env-file deploy/familyos/.env up -d --build
```

## When it runs

| Trigger | Redeploy? |
|---------|-----------|
| Push / CI on **`main`** (after build-and-test) | Yes |
| **Release** workflow (after release-api) | Yes |
| Push to `develop` / pull requests | No |
| Manual **Actions → Deploy stack (Portainer)** | Yes |

## Verify

1. Actions → latest **CI** or **Release** on `main` → job **Portainer / server redeploy**  
2. Portainer → stack → containers restarted / updated  
3. `curl -sk https://api.YOUR_DOMAIN/health` → `Healthy`
