# JobHunter OS — Technical Specification

**Version:** 0.1 · **Date:** 2026-10-31 · **Status:** Draft for review

JobHunter OS is the operations center for a job search: opportunities, the professional
profile they are measured against, recruiters, follow-ups and the numbers that show what is
working. It is phase 2 of the *Developer & Creative Toolkit* ecosystem. Phase 1, Outreach
Studio (the `smtp` repository, Mail Template Hub), is a separate application that JobHunter OS
uses to send email, through its public API.

## Spec-driven workflow

This spec set is the source of truth. Nothing is implemented that isn't specified here; when
reality forces a change, the spec is amended first (or in the same commit), then the code.

```
PRD ──▶ Architecture ──▶ Domain + API contracts ──▶ Implementation plan
```

## Documents

| # | Document | Contents |
|---|----------|----------|
| 01 | [01-prd.md](01-prd.md) | Problem, principles, v1 scope (F1–F7), non-goals |
| 02 | [02-architecture.md](02-architecture.md) | Modular monolith, modules and contracts, events, security, AI, integrations |
| 03 | [03-domain-and-api.md](03-domain-and-api.md) | Tables, rules and endpoints per module |
| 04 | [04-implementation-plan.md](04-implementation-plan.md) | Phases, definition of done, testing |

## Locked decisions

| Concern | Choice | Why |
|---------|--------|-----|
| Repository | Own repo `jobhunter-os`, next to `smtp` | Separate product and deployable; shares conventions, not code |
| Backend | ASP.NET Core on .NET 10, Clean Architecture (`JobHunter.Domain/Application/Infrastructure/Api`) | Same stack as Mail Template Hub |
| Shape | Modular monolith: modules as namespaces, cross-module access only through `Contracts`, enforced by architecture tests | The ecosystem plan: extract services only when there is a real need |
| Database | PostgreSQL 16, EF Core 10, snake_case | `jsonb`, `text[]`, `xmin` concurrency, `SKIP LOCKED` |
| Async work | Hangfire on PostgreSQL + a transactional outbox for module events | No extra broker until one is needed; events survive crashes |
| Identity | Own accounts: cookie sessions, Argon2id, double-submit CSRF (ported from Mail Template Hub) | No JWT in the browser; proven in phase 1 |
| Frontend | Next.js 16 App Router, TypeScript strict, Tailwind 4, shadcn/ui, TanStack Query, Zod | Same as phase 1; `/api` proxied for first-party cookies |
| Design | The ecosystem's two themes: **Mailroom** (light) and **Nocturne** (dark) | One visual language across the toolkit |
| AI | Anthropic Claude behind ports; deterministic code decides scores | AI extracts and drafts; it never invents experience or predicts hiring |
| Email | Sent through Outreach Studio's API with a user API key, after approval | One sending stack; JobHunter OS never holds mailbox tokens |
| Observability | Serilog + OpenTelemetry | Same as phase 1 |

## Local ports

| Service | Port | (Mail Template Hub uses) |
|---------|------|--------------------------|
| API | 5101 | 5001 |
| Web | 3100 | 3000 |
| PostgreSQL | 5433 | 5432 |

Both applications run side by side on one machine.
