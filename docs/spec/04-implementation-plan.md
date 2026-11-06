# 04 — Implementation plan

Each phase is one or more reviewable commits. A phase is done when its tests, lint,
typecheck and build pass, both themes are checked, and this file's status column is updated.

| Phase | Scope | Status |
|-------|-------|--------|
| 0 | Foundation | Done (2026-10-31) |
| 1 | Identity | Done (2026-10-31) |
| 2 | Profile & evidence (F1) | Done (2026-10-31) |
| 3 | Opportunities: capture, board, history (F2) | Done (2026-10-31) |
| 4 | Imports: text, URL, job-board sources (F2) | Done (2026-11-01) |
| 5 | Matching (F3) | Done (2026-11-02) |
| 6 | Contacts (F4) | Done (2026-11-02) |
| 7 | Outbox + follow-ups, drafts, Outreach Studio (F5) | Done (2026-11-05) |
| 8 | Analytics + dashboard (F6, F7) | Done (2026-11-05) |
| 9 | Launch: Docker images, deploy, screenshots, case study | — |

## Phase 0 — Foundation

- Repository, `.editorconfig`, `.gitattributes`, `.gitignore`, README.
- `JobHunter.slnx` with the four layers, the architecture and integration test projects
  (the unit test project arrives with the first domain logic, in phase 1); shared build
  settings in `Directory.Build.props`.
- API: Serilog, ProblemDetails with the global exception mapping, security headers,
  OpenAPI (development), OpenTelemetry, `/healthz` (database check), `GET /api/v1/ping`.
- Persistence: `AppDbContext` (snake_case), options validated on start, design-time
  factory, migrations-at-startup gate.
- Architecture tests: layer rules and the module boundary rule (spec 02 §3).
- Integration tests: `ApiFactory` on a PostgreSQL 16 Testcontainer; health, ping,
  security headers, ProblemDetails for unknown routes.
- Web: Next.js app with the Mailroom/Nocturne themes and toggle, the app shell with every
  module in the navigation (pages say what's coming and in which phase), `/api` proxy, API
  status indicator, vitest.
- `docker-compose.yml` (PostgreSQL on 5433), GitHub Actions CI (backend + web).

## Phase 1 — Identity

Port Mail Template Hub's auth: entities and migration, Argon2id hasher, session and CSRF
middleware, rate limits, audit writer, auth/me/sessions endpoints, `(auth)` pages
(register, login, forgot/reset), session guard, settings page (profile, password,
sessions). Unit tests for the hasher and token helpers; integration tests for every
endpoint, CSRF and rate limiting.

## Phase 2 — Profile & evidence

Profile, skills (with the skill catalog and alias normalization) and evidence; `/profile`
page with skill chips, level/years editors and evidence cards.

## Phase 3 — Opportunities

Companies, opportunities, stage machine with history (unit-tested transition table), board
endpoint; `/board` Kanban (drag between columns, keyboard-accessible move menu),
`/opportunities` list with filters, opportunity detail page.

## Phase 4 — Imports

Text import (heuristics + JSON-LD `JobPosting`), URL import with the SSRF guard
(unit-tested against private/loopback/metadata addresses and redirect hops), Greenhouse /
Lever / Ashby clients with fixtures shaped like their real responses, "Add from a company board" flow, dedupe by
`source_ref`.

## Phase 5 — Matching

Catalog families in scoring, rule-based extractor, Anthropic extractor behind the port,
requirements editor, algorithm v1 with exhaustive unit tests, fit panel on the
opportunity page and score chips on the board.

## Phase 6 — Contacts

Contacts, links to opportunities, interactions timeline; `/contacts` pages.

## Phase 7 — Outbox and follow-ups

Domain events, outbox interceptor, the outbox worker, inbox receipts (idempotency
integration test: the same message twice → one effect), `InteractionLogged` (an inbound one
marks the application as responded) and `outreach_send_id`, reminder rules, the due-reminders pass,
drafts with the Anthropic writer and guardrails, Outreach Studio client (stub handler in tests,
real `smtp` instance verified manually), approval flow UI.

## Phase 8 — Analytics and dashboard

Summary/skills/dashboard queries as pure functions over rows (a unit test per definition in
spec 03) behind a read-only port, integration tests for the queries, isolation and refusals;
`/analytics` (range in the URL, rates with their sample sizes, a weekly chart with a table
view) and the dashboard page, which also links every area on phones. Chart colors are checked
with a palette validator in both themes. Any change in the app marks analytics stale, so the
dashboard follows reminders and moves without a reload.

## Phase 9 — Launch

Container images for API and web, production configuration and secrets, deploy target,
OpenTelemetry exporter, README screenshots in both themes, and a case-study write-up
(problem, architecture, decisions, results — real numbers only). The API image needs time
zone data (tzdata): weekly analytics take the viewer's IANA zone.

## Testing strategy

| Level | Tools | Covers |
|-------|-------|--------|
| Unit | xUnit | Stage machine, scoring, skill normalization, SSRF guard, parsers, guardrails |
| Integration | xUnit, `WebApplicationFactory`, Testcontainers PostgreSQL 16, stubbed HTTP handlers for outside services | Every endpoint, ownership isolation, outbox delivery, jobs |
| Architecture | NetArchTest | Layer rules, module boundaries |
| Web | Vitest + Testing Library; Playwright for screenshots | Pure logic (board grouping, score display, rate formatting), key components |

Testcontainers is pinned to 3.x like Mail Template Hub (3.x works with the local Docker
engine; a 4.x bump is a separate change).
