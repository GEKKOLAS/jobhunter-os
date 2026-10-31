# 02 — Architecture

## 1. Context

```
 Browser ──▶ Next.js (web, :3100) ──/api proxy──▶ JobHunter.Api (:5101) ──▶ PostgreSQL (:5433)
                                                        │   └─ Hangfire worker (same process)
                                                        ├──▶ Anthropic API         (extraction, drafts)
                                                        ├──▶ Greenhouse / Lever / Ashby public job-board APIs
                                                        ├──▶ Posting URLs           (user-initiated import)
                                                        └──▶ Outreach Studio API    (send approved emails)
```

The browser only talks to the Next.js origin; `/api/*` is rewritten to the API, so the
session cookie is first-party and there is no CORS surface.

## 2. Solution layout

```
JobHunter.slnx
src/
  JobHunter.Domain/          entities, value objects, domain events — no dependencies
  JobHunter.Application/     use cases per module, ports (interfaces), contracts
  JobHunter.Infrastructure/  EF Core, Hangfire, outbox, AI, HTTP clients, crypto
  JobHunter.Api/             controllers, auth handlers, middleware, composition root
tests/
  JobHunter.UnitTests/
  JobHunter.IntegrationTests/  Testcontainers PostgreSQL + WireMock doubles
  JobHunter.ArchitectureTests/ layer and module rules
web/                         Next.js app
docs/spec/
```

Layer rules (architecture tests): Domain depends on nothing; Application on Domain only;
Infrastructure on Application and Domain; Api composes everything.

## 3. Modules

Each module owns a namespace in every layer (`JobHunter.Domain.Opportunities`,
`JobHunter.Application.Opportunities`, …), its tables, and its endpoints.

| Module | Owns | Feature |
|--------|------|---------|
| `Identity` | users, sessions, password reset, audit log | — |
| `Profile` | profile, skills, evidence items, skill catalog | F1 |
| `Opportunities` | companies, opportunities, stage history, job sources | F2 |
| `Matching` | requirements, fit assessments | F3 |
| `Contacts` | contacts, interactions | F4 |
| `FollowUps` | reminders, drafts, notifications | F5 |
| `Analytics` | read models only (no tables of its own in v1) | F6, F7 |
| `Integrations` | Outreach Studio connection, AI and job-board clients | — |

**Module boundary rule.** Code in module A may use module B only through
`JobHunter.Application.B.Contracts` (interfaces and DTOs B chooses to expose) and B's domain
events (`JobHunter.Domain.B.Events`). It never touches B's entities, use cases or
`DbSet`s. The one exception is `Analytics`, which only reads: it may query other modules'
entities, but not call their use cases. Architecture tests enforce this in Domain and
Application, where the business rules live. Infrastructure is where every module's
adapters meet (EF configurations, jobs), so it is exempt. A second test fails when a
namespace appears that is neither a declared module nor the shared kernel, so a new module
can't slip past the rule. The shared kernel (`JobHunter.Domain.Common`,
`JobHunter.Application.Common`) holds only cross-cutting types: ids, clock, current user,
errors, paging.

**One database port per module.** There is one `AppDbContext`, but handlers never see it:
each module declares its own narrow port (`IIdentityDbContext` with the Identity tables
only, and so on), and the context implements them all. A handler in one module therefore
can't even name another module's tables.

**Why namespaces and not one project per module:** the boundary is what matters and the
test enforces it; a project per module per layer would mean ~30 projects for one developer.
If a module is ever extracted into a service, its `Contracts` become the API.

## 4. Module events and the outbox

Modules react to each other through domain events, not direct calls:

- An aggregate raises an event (e.g. `OpportunityStageChanged`, `InteractionLogged`).
- On `SaveChanges`, an interceptor serializes pending events into `outbox_messages` in the
  **same transaction** as the state change.
- `DispatchOutboxJob` (Hangfire, every 10 s) claims batches with `FOR UPDATE SKIP LOCKED`,
  invokes the handlers, and marks rows processed. Failures back off and retry; after 10
  attempts a row is parked with its error and shown on an admin endpoint.
- Delivery is at-least-once. Handlers are idempotent: each records the message id it
  processed in `inbox_receipts (message_id, handler)`, and a duplicate is skipped.

Examples: `OpportunityStageChanged → applied` creates a follow-up reminder (FollowUps);
`InteractionLogged inbound` marks the application as responded (Opportunities) and closes
open follow-up reminders for that contact (FollowUps); `ProfileChanged` and
`RequirementsChanged` mark fit assessments stale (Matching).

## 5. Security

Ported from Mail Template Hub, same rules:

- **Auth:** Argon2id password hashes; server-side sessions in an `HttpOnly`, `Secure`,
  `SameSite=Lax` `__Host-` cookie; idle and absolute expiry; double-submit CSRF token on
  unsafe methods; fixed-window rate limits per IP (`auth`, `import`, `ai`).
- **Ownership:** every user-owned table has `user_id`; queries filter by the current user,
  and a resource owned by someone else is a 404, never a 403.
- **Secrets at rest:** the Outreach Studio API key is encrypted with AES-256-GCM using a
  per-row data key wrapped by a versioned key-encryption key from configuration (the same
  envelope scheme as Mail Template Hub's OAuth tokens). It is write-only through the API.
- **URL import (SSRF):** `https` only (and `http` in development); the host is resolved
  and every address must be public (no loopback, private, link-local, CGNAT or metadata
  ranges); redirects are followed manually (max 3) and each hop is re-validated; 10 s
  timeout; 2 MB body cap; `text/html` or `text/plain` only. The fetched HTML is reduced to
  text and never rendered.
- **Untrusted text:** posting descriptions are stored as plain text (HTML stripped on
  ingest) and rendered as text, so a posting can't inject markup into the app.
- **Headers, logging, audit:** security headers middleware, Serilog with secret scrubbing,
  and an audit log for auth events and every state change listed in spec 03.

## 6. AI

Ports in Application, Anthropic implementations in Infrastructure, models from
configuration (`Ai:Model`, `Ai:FastModel`).

| Port | Used for | Without an API key |
|------|----------|--------------------|
| `IRequirementExtractor` | Posting text → requirements (must/nice, skill, years) | `RuleBasedRequirementExtractor`: skill catalog + "N+ years" patterns, labelled `rules` |
| `IPostingParser` | Imported page text → title, company, location, description | Heuristics from the page `<title>` and `JobPosting` JSON-LD when present |
| `IDraftWriter` | Follow-up / thank-you drafts from facts the user selected | Unavailable; the UI offers a blank draft (a fake draft is worse than none) |

Guardrails: prompts receive only the facts needed; drafts must not mention skills,
employers, numbers or dates absent from the input; `{{placeholders}}` must round-trip; an
output that fails validation is rejected, not repaired. The fit score is computed by code
(spec 03 §Matching), never by the model.

## 7. Integrations

- **Outreach Studio.** Settings stores the base URL, the API key (encrypted) and the id of
  a plain template version with `{{subject}}`/`{{body}}` variables. Sending a draft calls
  `POST /api/v1/sends` on Outreach Studio with the user's default connected mailbox and
  records the returned send id. "Test connection" calls `GET /api/v1/me`. When not
  configured, drafts are copied and marked as sent manually.
- **Job boards.** Greenhouse (`boards-api.greenhouse.io/v1/boards/{token}/jobs?content=true`),
  Lever (`api.lever.co/v0/postings/{company}?mode=json`) and Ashby
  (`api.ashbyhq.com/posting-api/job-board/{name}`) are public APIs companies publish for
  their own careers pages. Base URLs are configurable so tests point at WireMock.

## 8. Observability and operations

Serilog structured logs; OpenTelemetry traces for HTTP in/out, EF Core and Hangfire jobs,
exported over OTLP when `OpenTelemetry:OtlpEndpoint` is set. `/healthz` checks the
database. Migrations run at startup in development and tests only
(`Database:ApplyMigrationsOnStartup`); production applies them in the deploy pipeline.
