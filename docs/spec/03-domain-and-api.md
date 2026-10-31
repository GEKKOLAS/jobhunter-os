# 03 — Domain and API

Conventions (all modules): REST under `/api/v1`, JSON camelCase, errors as RFC 7807
ProblemDetails with an `errorCode`; lists return `{ items, page, pageSize, totalCount }`;
ids are UUID v7; every user-owned table has `user_id`, `created_at`, `updated_at` and an
`xmin` concurrency token where the row is edited in place (stale writes → 409
`concurrency.conflict`). Validation failures → 422 `validation_failed` with field errors.

## Identity

Ported from Mail Template Hub phase 1: `users`, `user_sessions`, `password_reset_tokens`,
`audit_logs`.

| Method | Path | Notes |
|--------|------|-------|
| POST | `/auth/register`, `/auth/login`, `/auth/logout` | Login returns `csrfToken` in the body as well as the cookie |
| POST | `/auth/password/forgot`, `/auth/password/reset` | Reset links are logged in development (no email in v1) |
| GET / DELETE | `/auth/sessions`, `/auth/sessions/{id}` | |
| GET / PATCH | `/me` · POST `/me/password` | |
| GET | `/audit-logs` | Own events only |

## Profile (F1)

`profiles` (1:1 with user): `headline` ≤ 120, `summary` ≤ 2,000, `location` ≤ 120,
`target_roles text[]` (≤ 10), `seniority` (`junior|mid|senior|lead|principal`),
`work_modes text[]` (`remote|hybrid|onsite`), `languages jsonb` (`[{ code, level }]`).

`profile_skills`: `name` ≤ 60, `key` (normalized: lowercased, aliases folded, e.g.
`c#`/`csharp` → `csharp`, unique per user), `category`
(`language|framework|data|cloud|tooling|practice|domain|soft`), `level` 1–5, `years`
0–40 (one decimal), `last_used_year?`.

`evidence_items`: `kind` (`project|role|certification|education`), `title` ≤ 120,
`organization?`, `start_date?`, `end_date?`, `description` ≤ 4,000, `outcome?` ≤ 1,000
(the user's own words; never generated), `links jsonb` (`[{ label, url }]`, https only),
`skill_ids uuid[]`.

| Method | Path | Notes |
|--------|------|-------|
| GET / PUT | `/profile` | Created empty on first GET |
| GET / POST | `/profile/skills` | Duplicate key → 409 `skill.duplicate` |
| PUT / DELETE | `/profile/skills/{id}` | Delete also unlinks evidence |
| GET / POST | `/profile/evidence` | |
| PUT / DELETE | `/profile/evidence/{id}` | |
| GET | `/skills/dictionary?q=` | Known skills and aliases, for autocomplete |

Events: `ProfileChanged` (skills or seniority changed). Audit: `profile.updated`.

## Opportunities (F2)

`companies`: `name` ≤ 120, `website?`, `domain?` (normalized, unique per user when set),
`notes?`.

`opportunities`:

| Column | Notes |
|--------|-------|
| company_id | Created on the fly from a name when unknown |
| title ≤ 160, location? ≤ 120, work_mode?, employment_type? | `full_time|part_time|contract|internship` |
| salary_min?, salary_max?, salary_currency?, salary_period? | Only as stated in the posting |
| source | `manual|paste|url|greenhouse|lever|ashby` |
| source_url?, source_ref? | Unique (user_id, source, source_ref) when ref is set — re-importing updates instead of duplicating |
| description | Plain text ≤ 50,000 |
| stage | See below |
| priority | `low|normal|high`; `priority_set_by` `user|suggested` |
| applied_at?, first_response_at?, closed_at?, close_reason? | Set by stage transitions and events |
| archived_at? | Soft delete |

Stages and allowed moves:

```
saved ──▶ preparing ──▶ applied ──▶ screening ──▶ interviewing ──▶ offer ──▶ accepted
  │           │            │            │              │             └──▶ declined
  └───────────┴────────────┴────────────┴──────────────┴──▶ rejected | withdrawn | no_response
```

Moving forward may skip stages (e.g. `saved → applied`); moving back is allowed one step
for corrections. `applied_at` is set on the first move to `applied` or later;
`first_response_at` on the first move to `screening` or later, or by an inbound
interaction. Closed stages set `closed_at`. Every move writes
`opportunity_stage_changes (from, to, at, note?)` and raises `OpportunityStageChanged`.

`job_sources`: `provider` (`greenhouse|lever|ashby`), `board` (the company's board token),
`label`, `last_checked_at?`. Listing a source's postings is **read-only**: the user picks
which postings to save. Postings already saved are flagged.

| Method | Path | Notes |
|--------|------|-------|
| GET | `/opportunities?stage&priority&q&archived&page&pageSize` | |
| POST | `/opportunities` | Manual or from a preview below |
| GET / PUT / DELETE | `/opportunities/{id}` | DELETE archives |
| POST | `/opportunities/{id}/stage` | `{ stage, note? }`; illegal move → 409 `opportunity.invalid_stage` |
| GET | `/opportunities/{id}/history` | Stage changes |
| GET | `/board` | Open opportunities grouped by stage, ordered by priority then age |
| POST | `/imports/text` | `{ text }` → preview (parsed fields, not saved) |
| POST | `/imports/url` | `{ url }` → preview; rate limit `import`; SSRF rules in spec 02 §5; failures → 422 `import.unreachable` / `import.unsupported` |
| GET / POST | `/sources` · DELETE `/sources/{id}` | |
| GET | `/sources/{id}/postings` | Fetched live; `{ items: [{ ref, title, location, url, alreadySaved }] }` |
| POST | `/sources/{id}/postings/{ref}/save` | Saves one posting as an opportunity |
| GET / POST / PUT | `/companies`, `/companies/{id}` | |

Audit: `opportunity.created`, `opportunity.stage_changed`, `opportunity.archived`,
`source.created`, `source.deleted`.

## Matching (F3)

`opportunity_requirements`: `text` ≤ 300, `kind` (`must|nice`), `skill_key?`,
`years_min?`, `origin` (`rules|ai|user`), `confirmed bool`. Re-extraction replaces
unconfirmed rows only.

`fit_assessments` (one current row per opportunity): `score` 0–100, `band`,
`covered jsonb`, `partial jsonb`, `gaps jsonb`, `notes jsonb`, `algorithm_version`,
`computed_at`, `stale bool`.

**Algorithm v1 (deterministic, documented in the UI):**

- Weights: `must` = 3, `nice` = 1. Requirements without a `skill_key` are listed but not
  scored (shown as "not scored").
- Coverage per requirement: **1** when the profile has the skill with level ≥ 3 and years ≥
  `years_min` (if any); **0.5** when the skill is present but under the level or years, or
  when only a related skill from the same dictionary family is present; **0** otherwise.
- `score = round(100 × Σ weight × coverage ÷ Σ weight)`; no scored requirements → no
  score ("not enough information").
- Bands: ≥ 75 strong, ≥ 55 good, ≥ 35 partial, below weak.
- Notes, not in the score: work mode, location and seniority mismatches against the
  profile's preferences.
- Suggested priority: `high` for strong fits still in `saved`/`preparing` and less than
  14 days old; `low` for weak fits; otherwise `normal`. It never overrides a priority the
  user set.

Every assessment shows "Estimate based on your profile — not a prediction of the outcome."

| Method | Path | Notes |
|--------|------|-------|
| GET | `/opportunities/{id}/requirements` | |
| PUT | `/opportunities/{id}/requirements` | Replace the list; marks all as `user`-confirmed |
| POST | `/opportunities/{id}/requirements/extract` | Rules or AI; rate limit `ai` when AI |
| GET | `/opportunities/{id}/fit` | Recomputes when stale |

Event handlers: `ProfileChanged`, `RequirementsChanged` → mark assessments stale.

## Contacts (F4)

`contacts`: `name` ≤ 120, `company_id?`, `role?`, `email?`, `linkedin_url?` (https,
linkedin.com only), `phone?`, `notes?` ≤ 2,000, `archived_at?`.
`contact_opportunities (contact_id, opportunity_id, relation)`, relation
`recruiter|hiring_manager|referral|interviewer|other`.
`interactions`: `contact_id`, `opportunity_id?`, `channel`
(`email|linkedin|phone|video|in_person|other`), `direction` (`outbound|inbound`),
`occurred_at`, `summary` ≤ 2,000, `outreach_send_id?`.

| Method | Path | Notes |
|--------|------|-------|
| GET / POST | `/contacts?q&companyId` | |
| GET / PUT / DELETE | `/contacts/{id}` | DELETE archives |
| POST / DELETE | `/contacts/{id}/opportunities/{opportunityId}` | `{ relation }` |
| GET / POST | `/contacts/{id}/interactions` | Raises `InteractionLogged` |
| DELETE | `/interactions/{id}` | |

## Follow-ups (F5)

`reminders`: `title` ≤ 160, `due_at`, `opportunity_id?`, `contact_id?`, `kind`
(`follow_up|thank_you|prep|custom`), `status` (`open|done|dismissed`), `snoozed_until?`,
`origin` (`user|rule`), `rule_key?` (unique with the opportunity, so a rule fires once).

Rules v1 (from events): moved to `applied` → follow-up in `FollowUps:AfterApplyDays`
(default 7) unless a response already exists; moved to `interviewing` → thank-you
reminder the next day; inbound interaction → close open `follow_up` reminders for the same
opportunity.

`drafts`: `kind` (`follow_up|thank_you|outreach`), `opportunity_id?`, `contact_id?`,
`to_email?`, `subject` ≤ 200, `body` ≤ 10,000 (plain text), `origin` (`ai|user`),
`status` (`draft|approved|sent|discarded`), `sent_via?` (`outreach_studio|manual`),
`outreach_send_id?`, `sent_at?`. Status moves: draft → approved → sent; approved → draft
on edit; any → discarded.

`notifications`: in-app only in v1, created by `DueRemindersJob` (every 15 min) for
reminders that became due.

| Method | Path | Notes |
|--------|------|-------|
| GET / POST | `/reminders?status&due=today|overdue|week` | |
| PUT / DELETE | `/reminders/{id}` · POST `/reminders/{id}/done` · POST `/reminders/{id}/snooze` | |
| GET / POST | `/drafts` | POST `{ kind, opportunityId?, contactId?, generate: bool, facts? }` |
| PUT / DELETE | `/drafts/{id}` | Editing an approved draft returns it to `draft` |
| POST | `/drafts/{id}/approve` | |
| POST | `/drafts/{id}/send` | Approved only (409 `draft.not_approved`); needs a recipient email and Outreach Studio configured (409 `outreach.not_configured`); logs an outbound interaction |
| POST | `/drafts/{id}/mark-sent` | Manual channel; logs an outbound interaction |
| GET | `/notifications` · POST `/notifications/read` | |

Audit: `draft.approved`, `draft.sent`, `reminder.created` (user), `reminder.done`.

## Integrations

| Method | Path | Notes |
|--------|------|-------|
| GET | `/integrations/outreach` | `{ configured, baseUrl, templateVersionId, lastTestedAt, lastTestOk }` — never the key |
| PUT | `/integrations/outreach` | `{ baseUrl, apiKey?, templateVersionId }`; key encrypted at rest |
| POST | `/integrations/outreach/test` | Calls Outreach Studio `GET /api/v1/me` |
| DELETE | `/integrations/outreach` | |
| GET | `/ai/capabilities` | `{ extraction: "ai"|"rules", drafts: bool }` |

## Analytics (F6, F7)

Read-only queries over the user's data. A rate is `null` when its denominator is below 5,
and every rate is returned with its numerator and denominator.

| Metric | Definition |
|--------|------------|
| Applications | Opportunities with `applied_at` in the range |
| Response rate | Applications with `first_response_at` ÷ applications |
| Interview rate | Applications that reached `interviewing` ÷ applications |
| Offer rate | Applications that reached `offer` ÷ applications |
| Time to first response | Median days `applied_at → first_response_at`, responded ones only |
| No response | Applied ≥ 30 days ago with no response (shown, not assumed `no_response`) |

| Method | Path | Returns |
|--------|------|---------|
| GET | `/analytics/summary?days=30|90|365` | Totals, funnel, weekly series, by source, by target role |
| GET | `/analytics/skills?days=` | Most requested skill keys in saved postings; most frequent gaps |
| GET | `/dashboard` | Due/overdue reminders, stale applications (applied, no response, ≥ 10 days, no open reminder), top-priority open opportunities, counts per stage |
