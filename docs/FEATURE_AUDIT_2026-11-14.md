# FamilyOS verified feature audit — 2026-11-14

**Audit baseline:** `main` at `913c43a00b68e628c190aeeebc04b778ea68868c` (PR #73 merge).
**Method:** inspected `FEATURE_STORIES.md`, `ROADMAP.md`, the mobile Expo Router screens/API clients, ASP.NET controllers/application handlers, domain models, infrastructure services, tests, and CI workflow. This is a source-code audit, not a live production/device acceptance test.

## Status definitions

- **Implemented (code):** a usable path is visible in both relevant client and server code. Still requires normal release acceptance testing.
- **Partial:** one or more layers/capabilities named by the story are absent or visibly incomplete.
- **Needs verification:** implementation exists, but runtime behavior, production configuration, or device behavior has not been demonstrated.
- **Not evidenced:** no corresponding implementation was found in the inspected source.

## Summary

| Area | Audit result | Main remaining work |
|---|---|---|
| Identity and household | **Needs verification** | Re-test Android registration/sign-in and household create/join after PR #73 APK; user previously saw create-household 401 when token was absent. |
| Members and roles | **Implemented (code)** | Verify owner/adult/teen/child permissions against real Keycloak users. |
| Task lifecycle and history | **Implemented (code)** | Device acceptance test; improve recurrence, scheduling and time tracking UI. |
| Requests and approvals | **Partial** | Backend supports rich workflow; mobile still uses type-specific hard-coded composition and does not expose execution-plan commit. |
| Calendar | **Partial** | Upcoming/create/cancel UI exists; edit, attendees, and task-backed event workflows are not surfaced in the mobile screen. |
| Meals and recipes | **Implemented (code)** | Validate family usage and meal-to-shopping workflow. |
| Procurement | **Implemented (code)** | Product catalog is purchase-history-derived; no external product/price catalog is evidenced. |
| Family Pulse | **Partial** | Home, next action, attention, blockers and coming-up sections exist; outcomes API exists but no outcomes dashboard screen was found. |
| Notifications and realtime | **Partial** | In-app inbox, device registration, quiet hours, Expo push sender, and SignalR chat exist; broad domain realtime and end-to-end push/deep-link acceptance need work. |
| Offline | **Needs verification** | Cache and mutation queue/replay code exist; test reconnect, replay ordering, auth expiry, retries and duplicate prevention on device. |
| Quality/CI | **Partial** | CI runs domain tests, but 90% coverage is not enforced; current workflow is report-only with a 70% threshold. No API/mobile integration-test project was found in the inspected tree. |

## Story-by-story status

### Foundation and identity

| Story | Status | Evidence / remaining acceptance |
|---|---|---|
| #6 Keycloak OIDC + PKCE | **Needs verification** | `mobile/app/login.tsx` uses Expo AuthSession PKCE and exchanges/persists tokens. PR #73 changed registration to `openAuthSessionAsync`; verify with a new APK on Android, including cold-start callback, cancellation, token persistence/refresh and logout. |
| #22 Household onboarding / invite | **Needs verification** | `mobile/app/onboarding.tsx`, `FamilyController`, `InvitesController`; create, preview, accept, invite list/revoke paths exist. Re-test with real authenticated accounts because the reported create call returned 401 when no access token was present. |
| #16 Members & roles UI | **Implemented (code)** | `mobile/app/(tabs)/family.tsx` displays members and supports add member. |
| #58 Change role (Owner) | **Implemented (code)** | Family screen calls role-change API; controller requires OwnerOnly. Verify authorization with non-owner accounts. |
| #50 Deactivate member | **Implemented (code)** | Family screen and `POST /api/family/members/{id}/deactivate`; verify downstream access revocation and historical-data behavior. |
| #36 Family-scoped permissions | **Implemented (code)** | Family authorization policies and family-scoped controller/application queries exist. Add negative cross-family integration tests. |
| #68 Assignment ≠ acceptance | **Implemented (code)** | Task screen explicitly treats assignment as a request; separate accept/decline actions and API endpoints exist. |
| #38 Demo Henderson seed | **Implemented (code)** | Seed data exists; verify clean-database idempotence and ensure production never receives demo identities/data. |
| #65 Docker Compose stack | **Implemented (code)** | Compose/deploy files exist; keep validating clean one-shot deployment and persistent-volume recovery. |
| #66 EF Core migrations + schema | **Needs verification** | EF Core context plus schema bootstrap/ensure scripts exist; audit migration coverage against a clean database and an upgraded production database. |

### Tasks

| Story | Status | Evidence / remaining acceptance |
|---|---|---|
| #7 Create/assign/accept/complete | **Implemented (code)** | Tasks screen and TasksController cover lifecycle. |
| #24 Reassign/defer/cancel | **Implemented (code)** | Task screen exposes these actions and API endpoints exist. |
| #30 Decline + reasons | **Implemented (code)** | Decline action and reason support exist; verify reason selection UX and audit trail. |
| #18 Recurring + policy | **Partial** | Domain/application/API recurrence endpoints exist; Tasks screen does not expose recurrence creation/list/deactivation. |
| #52 Skip instance | **Implemented (code)** | Task screen exposes skip and API endpoint exists. |
| #48 Priority/category/overdue | **Partial** | Priority and due date appear in task UI; API/domain support category, but task form does not expose category selection. Verify overdue behavior/time zones. |
| #25 Planned vs actual time | **Partial** | API/domain carry planned and actual duration; mobile screen does not expose planned-time editing or a clear time-tracking workflow. |
| #40 Task history timeline | **Implemented (code)** | Task screen fetches and displays task history. |

### Requests and decisions

| Story | Status | Evidence / remaining acceptance |
|---|---|---|
| #8 Dynamic questionnaire + queue | **Partial** | Request types are loaded from API and queues render, but mobile creation branches on hard-coded type codes instead of rendering the backend question schema generically. |
| #33 Full type catalog | **Needs verification** | RequestTypeDefinition and seeded type definitions exist; compare live seeded catalog with product requirements. |
| #67 School/Medical/Personal/Household/Other | **Partial** | Mobile has special handling for School/Medical and API definitions; verify complete category catalog and add missing Personal/Household/Other flows. |
| #53 Draft save/edit/submit | **Implemented (code)** | Request screen has create-draft, edit, submit and withdraw flows. |
| #44 Ask/answer questions | **Implemented (code)** | Request screen and API expose ask/answer. |
| #45 Cancel/withdraw | **Implemented (code)** | Request screen exposes withdraw; API supports cancel. |
| #27 Money request | **Implemented (code)** | Dedicated amount/purpose composer exists. |
| #39 Grocery → procurement | **Implemented (code)** | Meal/request and procurement API paths connect requests to queue items; verify end-to-end linkage and permissions. |
| #23 Approval policies | **Implemented (code)** | Policy evaluation/approval paths and request UI controls exist; add role/threshold scenario tests. |
| #47 Owner policy override | **Implemented (code)** | API and request UI expose override. |
| #46 Conditional approval | **Implemented (code)** | Conditional approval and conditions UI/API exist. |
| #13 Conditions/blockers | **Implemented (code)** | Conditions API and request screen expose condition creation/satisfaction. |
| #9 Execution plan commit | **Partial** | API has generate-plan and commit-plan endpoints; mobile exposes Generate plan but no corresponding plan review/commit flow was found. |
| #41 Request decision trail | **Implemented (code)** | Request screen fetches and displays history. |

### Calendar, meals and procurement

| Story | Status | Evidence / remaining acceptance |
|---|---|---|
| #12 Calendar view + task-backed events | **Partial** | Calendar screen shows events and creates/cancels events; task-linked event creation is not exposed in the screen. |
| #54 Event create/edit + attendees | **Partial** | API supports create/update/cancel, but mobile only creates and cancels. No attendee model/API/UI was found in the inspected calendar path. |
| #17 Recipes & meal plan | **Implemented (code)** | Weekly plan, recipes, servings, apply-to-plan and ingredients-to-shopping UI/API exist. |
| #10 Procurement queue → purchase | **Implemented (code)** | Family screen exposes queue, carts and purchase recording. |
| #69 Cart build / abandon | **Implemented (code)** | Family screen creates carts, adds/removes items and abandons carts. |
| #28 Shopping trip by store | **Implemented (code)** | Family screen groups queue items by preferred store and starts store carts. |
| #49 Hold/defer/no-longer-needed | **Implemented (code)** | UI and API actions exist. |
| #29 Product catalog & prices | **Partial** | Product history is derived from purchased items and actual prices; no live vendor catalog, search, or price comparison is evidenced. |

### Pulse, notifications and learning

| Story | Status | Evidence / remaining acceptance |
|---|---|---|
| #11 Family Pulse home | **Implemented (code)** | Home screen renders greeting, role, next action, attention, blockers, coming up and household counts. |
| #35 Waiting/blockers | **Implemented (code)** | Pulse API and home screen expose waiting/blockers. |
| #14 In-app notifications + deep link | **Partial** | Settings screen has notification inbox/read actions and notification routing exists; verify every supported entity route and cold-start behavior. |
| #55 Notification deep-link companion | **Needs verification** | Routing helper exists; verify payload-to-route mapping for task, request, event and procurement entities. |
| #32 Push notifications | **Partial** | Device registration UI and Expo push sender exist; verify permission prompts, token refresh, receipt/error handling, delivery and tap routing on real Android devices. |
| #51 Notification preferences / quiet hours | **Implemented (code)** | Settings screen reads/writes quiet-hours settings; verify server-side delivery suppression and boundary/time-zone cases. |
| #31 SignalR realtime | **Partial** | SignalR chat hub/client exist and chat screen has live/polling states; task/request/Pulse live updates are not evidenced. |
| #56 Domain events (MassTransit) | **Partial** | Event bus and RabbitMQ wiring exist, but publish failures are swallowed and the inspected TaskCompleted consumer only logs. Add durable/outbox/retry behavior and meaningful consumers before relying on events. |
| #34 Outcomes dashboard | **Partial** | Outcomes API/query and mobile API client exist; no outcomes dashboard screen was found in the mobile routes. |
| #37 External time source hooks | **Needs verification** | Google Calendar OAuth, sync worker and settings controls exist; verify production credentials, OAuth callback/state validation, pull/push behavior and duplicate/update handling. |

### App shell and engineering

| Story | Status | Evidence / remaining acceptance |
|---|---|---|
| #42 Settings hub | **Implemented (code)** | Settings screen includes account, invites, security, Google Calendar, device, notifications, quiet hours and sign-out. |
| #57 Offline-tolerant mobile | **Needs verification** | Cache, queued mutations, offline banner and queue flush code exist; run airplane-mode/reconnect acceptance tests, including conflict and replay cases. |
| #43 Production hardening | **Needs verification** | Exception/correlation/health and observability code exist; validate production logs, alerting, secret handling and recovery. |
| #15 CI build/test/coverage/version | **Partial** | CI builds/tests and reports coverage. Coverage threshold is 70% and report-only; it does not enforce the 90% target. |
| #19 Domain unit tests ≥90% | **Partial** | Domain test suite exists. Workflow currently sets `DOMAIN_COVERAGE_MIN=70` and `DOMAIN_COVERAGE_ENFORCE=false`; measure current coverage and raise/enforce target after gaps are closed. |
| #20 Expo app shell | **Implemented (code)** | Expo Router app, tab screens and root auth/offline/analytics setup exist. |
| #21 Push full local source to main | **Implemented (code)** | Current source tree and release workflows are present on main. |

## Actionable backlog (ordered)

### P0 — prove the release works for a real family
1. **Android auth + household onboarding acceptance** — build APK from merged PR #73; test new registration, sign-in, callback capture, PKCE exchange, token persistence/refresh, logout, household create, invite preview/accept. Capture sanitized request/response and server logs for failures. Do not close #6/#22 until passed on a physical Android device.
2. **Authorization integration tests** — test Owner/Adult/Teen/Child permissions and cross-family isolation for household, invites, tasks, requests and procurement. Current controller policies alone do not prove runtime isolation.

### P1 — complete the core workflows
3. **Generic request questionnaire + type catalog** — render request questions from RequestTypeDefinition schema; remove hard-coded code-only form branching; cover School, Medical, Personal, Household, Other, Ride, Grocery/Purchase and Money; validate answers and preserve draft edits.
4. **Execution plan review/commit on mobile** — display generated plan and selected items, allow deselection, commit, show resulting tasks/calendar/procurement work and decision history.
5. **Task recurrence and time planning UI** — add recurrence create/list/deactivate, category editing, planned start/end/estimate, actual-time capture and variance display; cover DST/time-zone and recurrence skip behavior.
6. **Offline sync acceptance and resilience** — test queued writes replay on reconnect, expired-token refresh, ordering/dependencies, duplicate prevention/idempotency, conflict handling, retry/backoff and user-visible failed queue recovery.
7. **API integration test suite** — add database-backed tests for auth/family scoping, household create/join, request decisions, task lifecycle, procurement and calendar. Domain unit tests do not cover HTTP/auth/EF integration.

### P2 — finish collaboration and visibility
8. **Calendar mobile completion** — add edit event, attendees, task/request links, date-range navigation and clear Google sync status; verify Google OAuth state/redirect and two-way update/delete behavior.
9. **Notifications/realtime completion** — complete push token lifecycle and delivery receipts, quiet-hours enforcement, deep-link routing for all entity types, and realtime updates for tasks/requests/Pulse beyond chat.
10. **Outcomes dashboard** — add mobile route and role-aware charts/metrics using the existing outcomes API; define metrics and date ranges before UI work.
11. **Product catalog scope decision** — decide whether #29 means purchase-history catalog only or vendor-backed product/price search. If vendor-backed, define data provider, refresh cadence and price provenance before implementation.

### P3 — quality and operational maturity
12. **Raise and enforce domain coverage** — measure actual coverage; raise minimum from 70% report-only to 90% enforced in stages with explicit exclusions and branch protection.
13. **Domain event reliability** — use an outbox/retry/dead-letter strategy; make consumers perform durable side effects; expose broker health and failed-message observability.
14. **Production acceptance checklist** — verify secrets/configuration, backups/restore, migrations, health checks, logs/alerts, push credentials, Google OAuth credentials and rollback procedure.

## Tracker hygiene

The existing issue tracker contains many closed issues whose labels/body still say **status:open** or whose story text says the feature is not complete. GitHub closed/open state is therefore not a trustworthy completion signal today. Reconcile issue state with this audit; close a story only when its acceptance criteria pass in the actual mobile/API workflow. The audit classifications above are based on source presence and exposed UI, not claims that production acceptance has passed.

## Primary source files inspected

- `FEATURE_STORIES.md`, `ROADMAP.md`, `README.md`, `.github/workflows/ci.yml`
- Mobile: `mobile/app/login.tsx`, `mobile/app/onboarding.tsx`, `mobile/app/(tabs)/index.tsx`, `tasks.tsx`, `requests.tsx`, `calendar.tsx`, `meals.tsx`, `family.tsx`, `settings.tsx`, `chat.tsx`, `mobile/src/api/client.ts`, `mobile/src/offline/*`, `mobile/src/push/*`
- API: `FamilyController`, `InvitesController`, `TasksController`, `RequestsController`, `CalendarController`, `MealsController`, `RecipesController`, `ProcurementController`, `PulseController`, `NotificationsController`, `OutcomesController`
- Application/domain/infrastructure: task/request/procurement/calendar/Google Calendar handlers, MassTransit event bus/consumer, Expo push sender, domain models and tests.
