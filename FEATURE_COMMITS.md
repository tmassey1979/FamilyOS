# Feature commit history (local)

Ordered commits on `main` (local clone under `artifacts/FamilyOS`):

```
019dc51 feat(api): REST controllers, JWT auth, Swagger, Program host
f373309 feat(infrastructure): EF Core DbContext, MassTransit, Keycloak identity, seed data
f67b40d feat(application): CQRS handlers for tasks, requests, pulse, family, procurement
b761a76 feat(procurement): procurement items, products, carts
120a8b6 feat(requests): dynamic requests, approvals, conditions, execution plans
bf391ca feat(tasks): task lifecycle, recurrence, time segments, calendar events
5196263 feat(foundation): solution structure, Docker stack, Keycloak realm, Family domain
```

## Push remaining source to GitHub

GitHub already has foundation + partial domain commits. To push the full local history:

```bash
cd /path/to/artifacts/FamilyOS
git remote add origin https://github.com/tmassey1979/FamilyOS.git
# Prefer force-with-lease only if you intend to replace remote main with local feature history:
git push -u origin main --force-with-lease
```

## GitHub issues (roadmap)

- #1 Phase 1 — Foundation
- #2 Phase 2 — Tasks
- #3 Phase 3–6 — Requests, Approvals, Execution, Procurement
- #4 Phase 9 — Family Pulse
- #5 Phase 12 — Mobile Expo & hardening
