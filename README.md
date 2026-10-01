# Family OS

**Make the next meaningful action obvious.**

## Quick start (Docker)

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

## Versioning & CI

- CI on every push/PR: restore, build, test, coverage
- Release: Actions → Release (patch/minor/major) or tag `v1.2.3`
- See ROADMAP.md and FEATURE_STORIES.md
