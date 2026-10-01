# Family OS

**Make the next meaningful action obvious.**

Family OS is an execution-first operating system for households. It is not just a calendar, to-do list, or chat app — it coordinates requests, decisions, approvals, tasks, calendars, procurement, execution, and outcomes.

```
PLAN → REQUEST → DECIDE → EXECUTE → MEASURE → LEARN
```

## Stack

| Layer | Technology |
|-------|------------|
| Mobile | React Native, TypeScript, Expo, Expo Router |
| API | ASP.NET Core 8, C# |
| Data | PostgreSQL, Entity Framework Core |
| Messaging | MassTransit + RabbitMQ |
| Identity | Keycloak (OIDC / OAuth 2.0 + PKCE) |
| Real-time | SignalR (where it improves the experience) |

Architecture: **modular monolith** with clear domain boundaries (Family, Tasks, Calendar, Requests, Approvals, Conditions, Execution, Procurement, Notifications, Time Intelligence, Pulse).

## Solution structure

```
FamilyOS/
├── docker/                 # PostgreSQL, RabbitMQ, Keycloak
├── mobile/                 # Expo React Native app
├── src/
│   ├── FamilyOS.Contracts  # Commands & events
│   ├── FamilyOS.Domain     # Domain models & invariants
│   ├── FamilyOS.Application# CQRS handlers, DTOs
│   ├── FamilyOS.Infrastructure # EF Core, MassTransit, Keycloak wiring
│   └── FamilyOS.Api        # REST API, auth, Swagger
└── tests/
```

## Quick start

### 1. Infrastructure

```bash
cd docker
docker compose up -d
```

Services:

- PostgreSQL → `localhost:5432` (user/pass/db: `familyos`)
- RabbitMQ → `localhost:5672` (management UI: `15672`)
- Keycloak → `localhost:8080` (admin/admin), realm `familyos`

### 2. API

```bash
cd src/FamilyOS.Api
dotnet restore
dotnet ef database update --project ../FamilyOS.Infrastructure
dotnet run
```

### 3. Mobile (Expo)

```bash
cd mobile
npm install
npx expo start
```

Open in **Expo Go**. Authenticate via Keycloak (Authorization Code + PKCE).

### Dev users (Keycloak realm)

| Username | Password | Household role |
|----------|----------|----------------|
| terry.owner | password | Owner |
| michelle.adult | password | Adult |
| mia.teen | password | Teen |
| eli.child | password | Child |

## Feature commits

This repository is organized with **feature-oriented commits** aligned to product phases:

1. Foundation (solution, docker, Keycloak, domain core)
2. Tasks domain
3. Requests, approvals, conditions, execution
4. Procurement
5. Application CQRS layer
6. Infrastructure (EF, messaging, identity, seed)
7. API (REST, JWT, Swagger)

## License

Proprietary — all rights reserved unless otherwise stated.
