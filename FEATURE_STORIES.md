# Family OS — Feature → Story Index

Every product feature maps to a GitHub **story** (vertical slice: backend + frontend where applicable).
Repo: https://github.com/tmassey1979/FamilyOS

## Product scenarios (definition of done)

| Scenario | Story |
|----------|-------|
| 1 Family setup | #60 |
| 2 Task lifecycle | #61 |
| 3 Decline | #62 |
| 4 Ride request | #26 |
| 5 Purchase | #59 |
| 6 Meal plan | #63 |
| 7 Family Pulse by role | #64 |

## PLAN → REQUEST → DECIDE → EXECUTE → MEASURE → LEARN

### Foundation & identity
| Feature | Story |
|---------|-------|
| Keycloak OIDC + PKCE login | #6 |
| Household onboarding / invite | #22 |
| Members & roles UI | #16 |
| Change role (Owner) | #58 |
| Deactivate member | #50 |
| Family-scoped permissions | #36 |
| Assignment ≠ acceptance | #68 |
| Demo Henderson seed | #38 |
| Docker Compose stack | #65 |
| EF Core migrations | #66 |

### Tasks
| Feature | Story |
|---------|-------|
| Create / assign / accept / complete | #7 |
| Reassign / defer / cancel | #24 |
| Decline + reasons | #30 |
| Recurring + policy | #18 |
| Skip instance | #52 |
| Priority / category / overdue | #48 |
| Planned vs actual time | #25 |
| Task history timeline | #40 |

### Requests & decisions
| Feature | Story |
|---------|-------|
| Dynamic questionnaire + queue | #8 |
| Full type catalog | #33 |
| School/Medical/Personal/Household/Other | #67 |
| Draft save/edit/submit | #53 |
| Ask / answer questions | #44 |
| Cancel / withdraw | #45 |
| Money request | #27 |
| Grocery → procurement | #39 |
| Approval policies | #23 |
| Owner policy override | #47 |
| Conditional approval | #46 |
| Conditions / blockers | #13 |
| Execution plan commit | #9 |
| Request decision trail | #41 |

### Calendar, meals, procurement
| Feature | Story |
|---------|-------|
| Calendar view + task-backed | #12 |
| Event create/edit + attendees | #54 |
| Recipes & meal plan | #17 |
| Procurement queue → purchase | #10 |
| Cart build / abandon | #69 |
| Shopping trip by store | #28 |
| Hold / defer / no-longer-needed | #49 |
| Product catalog & prices | #29 |

### Pulse, notify, learn
| Feature | Story |
|---------|-------|
| Family Pulse home | #11 |
| Waiting / blockers section | #35 |
| In-app notifications + deep link | #14 #55 |
| Push notifications | #32 |
| Notification preferences / quiet hours | #51 |
| SignalR realtime | #31 |
| Domain events (MassTransit) | #56 |
| Outcomes dashboard | #34 |
| External time source hooks | #37 |

### App shell & quality
| Feature | Story |
|---------|-------|
| Settings hub | #42 |
| Offline-tolerant mobile | #57 |
| Production hardening | #43 |

## Engineering tasks (not product stories)

| Task | Issue |
|------|-------|
| CI / release auto-version | #15 |
| Domain unit tests ≥90% | #19 |
| Expo app shell | #20 |
| Push full source to main | #21 |

## Count

- **Product stories:** #6–#14, #16–#18, #22–#69 (excl. tasks)
- **Tasks:** #15, #19, #20, #21
- **Phases #1–#5:** closed (superseded by slices)
