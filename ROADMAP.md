# Family OS — Prioritized execution roadmap

Stories ordered for workflow quality and one-shot Docker DX.
Commit after each completed feature. CI must be green before calling a commitment successful.

## P0 — Platform (do first)

| Priority | Issue | Feature |
|----------|-------|---------|
| 1 | #21 | Push full local source to GitHub main |
| 2 | #65 | Docker Compose one-shot stack |
| 3 | #15 | CI: build, test, coverage, version |
| 4 | #66 | EF Core migrations + schema |
| 5 | #38 | Demo seed (Henderson household) |
| 6 | #43 | Production hardening |

## P1 — Core path

| Priority | Issue | Feature |
|----------|-------|---------|
| 7 | #6 | Auth: Keycloak → family context |
| 8 | #36 | Family-scoped permissions |
| 9 | #22 | Household onboarding |
| 10 | #11 | Family Pulse |
| 11 | #7 | Tasks lifecycle |
| 12 | #68 | Assignment ≠ acceptance |
| 13 | #20 | Expo app shell |

## P2 — Requests & decisions

#8 #23 #44 #46 #13 #9 #26

## P3 — Procurement & calendar

#10 #69 #28 #12 #59

## P4 — Scenarios & polish

#60–#64, notifications, learn, remaining FEATURE_STORIES.md

## Versioning

- Directory.Build.props VersionPrefix
- CI stamps version + git SHA
- Release workflow: manual bump or v* tag
- Docker: familyos-api:<semver>
