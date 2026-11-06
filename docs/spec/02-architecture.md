# 02 — Architecture

## 1. Context

```
 Browser ──▶ Next.js (web, :3100) ──/api proxy──▶ JobHunter.Api (:5101) ──▶ PostgreSQL (:5433)
                                                        │   └─ background worker (same process)
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
  JobHunter.Infrastructure/  EF Core, outbox and background worker, AI, HTTP clients, crypto
  JobHunter.Api/             controllers, auth handlers, middleware, composition root
tests/
  JobHunter.UnitTests/
  JobHunter.IntegrationTests/  Testcontainers PostgreSQL + stubbed HTTP handlers
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
- A background worker in the API process (every 10 s) claims one message at a time with
  `FOR UPDATE SKIP LOCKED`, so several instances never take the same one, runs its handlers
  and marks it processed, all in one transaction. Failures back off (30 s doubling, up to an
  hour) and retry; after 10 attempts a message is parked with its error, logged as an error,
  and `/healthz` reports Degraded until someone looks. Processed messages are deleted after
  30 days.
- Delivery is at-least-once. Handlers are idempotent: each records the message id it
  processed in `inbox_receipts (message_id, handler)`, and a duplicate is skipped.
- Why not Hangfire, as first planned: the outbox table already gives persistence, retries
  and parking. A job server would add a second scheduler and its own schema for two timers.
  The same worker runs the due-reminders pass every 5 minutes.

Examples: `OpportunityStageChanged → applied` creates a follow-up reminder (FollowUps);
`InteractionLogged inbound` marks the application as responded (Opportunities) and closes
open follow-up reminders for that contact (FollowUps). Fit assessments need no event: they are
recomputed on read (spec 03, Matching).

## 5. Security

Ported from Mail Template Hub, same rules:

- **Auth:** Argon2id password hashes; server-side sessions in an `HttpOnly`, `Secure`,
  `SameSite=Lax` `__Host-` cookie; idle and absolute expiry; double-submit CSRF token on
  unsafe methods; fixed-window rate limits per IP (`auth`, `import`, `ai`).
- **Ownership:** every user-owned table has `user_id`; queries filter by the current user,
  and a resource owned by someone else is a 404, never a 403.
- **Secrets at rest:** the Outreach Studio API key is encrypted with AES-256-GCM using a
  per-row data key wrapped by a versioned key-encryption key from configuration
  (`Secrets:Keys:{version}`, `Secrets:ActiveVersion`; the same envelope scheme as Mail Template
  Hub's OAuth tokens), with the row's id as associated data so a ciphertext copied to another
  row fails. It is write-only through the API. Development has a key in its settings file,
  which the API refuses to start with anywhere else.
- **URL import (SSRF):** `https` only (and `http` when `Imports:AllowHttp` is set, in
  development); ports 80/443 only; no credentials in the URL; names like `localhost`, `*.local`
  and `*.internal` refused outright. The host is resolved **while connecting**, inside the HTTP
  handler's connect callback: every address must be public (no loopback, private, link-local,
  CGNAT, documentation, multicast or metadata ranges, and IPv6 forms that embed one) and the
  connection goes to an address that was checked, so DNS rebinding can't slip past a
  check made earlier. Redirects are followed manually (max 3) and each hop is re-validated;
  10 s timeout; 2 MB body cap; `text/html` or `text/plain` only; no cookies. The fetched HTML
  is reduced to text and never rendered.
- **Untrusted text:** posting descriptions are stored as plain text (HTML stripped on
  ingest) and rendered as text, so a posting can't inject markup into the app.
- **Headers, logging, audit:** security headers middleware, Serilog with secret scrubbing,
  and an audit log for auth events and every state change listed in spec 03.

## 6. AI

Ports in Application, Anthropic implementations in Infrastructure using the official
`Anthropic` SDK, with the key (`Ai:ApiKey`, never in a file under version control) and the model
(`Ai:Model`, default `claude-opus-5-5`) from configuration. Extraction runs at low effort with a
JSON schema as its output format, and opts into server-side refusal fallbacks. Posting text is
untrusted: it goes to the model as data inside delimiters, the model has no tools, and its
answer can only be requirement rows, which still need the user's confirmation.

| Port | Used for | Without an API key |
|------|----------|--------------------|
| `IRequirementExtractor` | Posting text → requirements (must/nice, skill, years) | `RuleBasedRequirementExtractor`: section headings, skill catalog + "N+ years" patterns, labelled `rules`. Also the fallback when an AI call fails |
| `IPostingParser` | Imported page text → title, company, location, description | Heuristics from the page `<title>` and `JobPosting` JSON-LD when present (phase 4 ships these; an AI parser can replace them behind the port once the Anthropic client exists, in phase 5) |
| `IDraftWriter` | Follow-up / thank-you drafts from facts the user selected | Unavailable; the UI offers a blank draft (a fake draft is worse than none) |

Guardrails: prompts receive only the facts needed; drafts must not mention skills,
employers, numbers or dates absent from the input; `{{placeholders}}` must round-trip; an
output that fails validation is rejected, not repaired. The fit score is computed by code
(spec 03 §Matching), never by the model.

## 7. Integrations

- **Outreach Studio.** Settings stores the base URL, the API key (encrypted) and the id of
  a plain template version with `{{subject}}`/`{{body}}` variables. Sending a draft reads the
  user's default connected mailbox from `GET /api/v1/me` (`defaultAccountId`), then calls
  `POST /api/v1/sends` with `Authorization: Bearer mth_…` and an `Idempotency-Key`, and records
  the returned send id. "Test connection" calls `GET /api/v1/me`. When not configured, drafts
  are copied and marked as sent manually. The base URL is user-supplied, so it goes through the
  SSRF guard's connector: https and public addresses only, unless
  `Integrations:Outreach:AllowPrivateNetwork` is set (Development, where Outreach Studio runs
  on `http://localhost:5001`). Calls time out after 15 s.
- **Job boards.** Greenhouse (`boards-api.greenhouse.io/v1/boards/{token}/jobs?content=true`),
  Lever (`api.lever.co/v0/postings/{company}?mode=json`) and Ashby
  (`api.ashbyhq.com/posting-api/job-board/{name}`) are public APIs companies publish for
  their own careers pages. Base URLs are configurable (`Imports:*BaseUrl`). Their responses are
  capped at 32 MB (a large board with full text is several MB) and time out after 20 s.

## 8. Observability and operations

Serilog structured logs; OpenTelemetry traces for HTTP in/out and EF Core (the worker's
queries included), exported over OTLP when `OpenTelemetry:OtlpEndpoint` is set. `/healthz`
checks the database and the outbox (Degraded while a message is parked). Migrations run at
startup in development and tests only (`Database:ApplyMigrationsOnStartup`); production
applies them in the deploy pipeline.
