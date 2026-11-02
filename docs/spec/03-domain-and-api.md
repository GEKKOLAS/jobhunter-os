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

- **Signed in by default.** Every endpoint requires a session unless it is marked anonymous
  (register, login, password reset, ping, health). The rule also covers requests that
  match no route: they answer 401 to anonymous callers and 404 only once signed in.
- **Cookies.** `jh_session` (`__Host-jh_session` in production, `Secure`) and `jh_csrf`.
  Browsers share cookies across ports of one host, so these names must differ from Mail
  Template Hub's `mth_*` for both apps to run on `localhost`.
- **Registering a taken email** returns the same body as a real sign-up, but no session.
  Without email verification the missing session cookie still tells the caller the
  address exists; the `auth` rate limit (10 per 15 min per IP) bounds that probing. Email
  verification arrives with transactional email.
- **Sign-in redirect.** The web app follows `?next=` only for paths on its own origin
  (`//host` and `/\host` are rejected), so a crafted sign-in link can't send the user
  elsewhere.

## Profile (F1)

`profiles` (1:1 with user): `headline` ≤ 120, `summary` ≤ 2,000, `location` ≤ 120,
`target_roles text[]` (≤ 10, each ≤ 80), `seniority?` (`junior|mid|senior|lead|principal`),
`work_modes text[]` (`remote|hybrid|onsite`), `languages jsonb`
(`[{ name ≤ 40, level }]`, ≤ 10, level `basic|conversational|professional|native`).

`profile_skills` (≤ 150 per user): `name` ≤ 60, `key` (unique per user, see below),
`category` (`language|framework|data|cloud|tooling|practice|domain|soft`), `level` 1–5,
`years` 0–40 (one decimal), `last_used_year?` (1970–this year).

`evidence_items` (≤ 100 per user): `kind` (`project|role|certification|education`),
`title` ≤ 120, `organization?` ≤ 120, `start_date?`, `end_date?` (not before the start;
empty means ongoing), `description` ≤ 4,000, `outcome?` ≤ 1,000 (the user's own words;
never generated), `links jsonb` (≤ 5 × `{ label ≤ 60, url }`, https only),
`skill_ids uuid[]` (the user's own skills only, else 422 `evidence.unknown_skill`).

**Skill keys and the catalog.** A skill's key is its name in a compact form: lowercase,
`#` → `sharp`, `+` → `plus`, everything else that isn't a letter or digit dropped
(`ASP.NET Core` → `aspnetcore`). The compact form is then looked up in the **skill
catalog**, a curated list of known skills with their aliases, category and family, so
`C#`, `csharp` and `C Sharp` are the same skill and `.NET Core` folds into `dotnet`.
Unknown skills keep their compact form as key. The catalog belongs to Profile; Matching
(phase 5) uses it through `JobHunter.Application.Profile.Contracts.ISkillCatalog`, and
the families are what "related skill" means there.

**Evidence count.** Every skill reports how many evidence items cite it. A skill with
none shows "no evidence yet". That isn't an error: it's a reminder that claims
should be backed by evidence (PRD principle 2).

| Method | Path | Notes |
|--------|------|-------|
| GET / PUT | `/profile` | Created empty on first GET; PUT carries `version` |
| GET / POST | `/profile/skills` | Duplicate key → 409 `skill.duplicate`; `category` may be omitted for catalog skills; limit → 409 `profile.limit` |
| PUT / DELETE | `/profile/skills/{id}` | PUT carries `version`; delete also unlinks evidence |
| GET / POST | `/profile/evidence` | Newest first by start date; limit → 409 `profile.limit` |
| PUT / DELETE | `/profile/evidence/{id}` | PUT carries `version` |
| GET | `/skills/catalog?q=&limit=` | Catalog matches for autocomplete: exact, then prefix, then contains |

Rows edited in place carry `version` (PostgreSQL `xmin`); a PUT with a stale version →
409 `concurrency.conflict`.

Audit: `profile.updated`, `skill.created|updated|deleted`, `evidence.created|updated|deleted`.
Other modules read skills and preferences through
`JobHunter.Application.Profile.Contracts.ICandidateFacts`.

## Opportunities (F2)

`companies`: `name` ≤ 120 (citext, unique per user, so "Contoso" and "contoso" are one
company), `website?` (https/http URL), `domain?` (the website's host, lowercased, `www.`
dropped; unique per user when set), `notes?` ≤ 2,000.

`opportunities`:

| Column | Notes |
|--------|-------|
| company_id | `companyId`, or `companyName` matched case-insensitively and created when unknown |
| title ≤ 160, location? ≤ 120, work_mode?, employment_type? | `remote|hybrid|onsite`; `full_time|part_time|contract|internship` |
| salary_min?, salary_max?, salary_currency?, salary_period? | Whole units, min ≤ max; ISO 4217 code; `year|month|hour`. Only as stated in the posting |
| source | `manual|paste|url|greenhouse|lever|ashby` |
| source_url?, source_ref? | Unique (user_id, source, source_ref) when ref is set — re-importing updates instead of duplicating |
| description | Plain text ≤ 50,000, shown as text, never as HTML |
| stage, previous_stage? | See below; `previous_stage` is where the last move came from |
| priority | `low|normal|high` (default `normal`); `priority_set_by` `user|suggested`, null until set |
| applied_at?, first_response_at?, closed_at? | Set by stage moves (below) and, from phase 6, by inbound interactions |
| archived_at? | Soft delete; archived opportunities leave the board and default lists |

**Stages.** Open: `saved → preparing → applied → screening → interviewing → offer`.
Closed: `accepted`, `declined`, `rejected`, `withdrawn`, `no_response`.

```
saved ──▶ preparing ──▶ applied ──▶ screening ──▶ interviewing ──▶ offer ──▶ accepted
  │           │            │            │              │             ├──▶ declined
  └───────────┴────────────┴────────────┴──────────────┴─────────────┴──▶ rejected | withdrawn
                           └────────────┴──────────────┴──▶ no_response
```

| From | Allowed moves |
|------|---------------|
| An open stage | Any later open stage (skipping is fine: `saved → applied`); the open stage just before it (a correction); `rejected` or `withdrawn` |
| `applied`, `screening`, `interviewing` | Also `no_response` |
| `offer` | Also `accepted` or `declined` |
| A closed stage | Only back to `previous_stage` (undoing the close) |

Anything else → 409 `opportunity.invalid_stage`, naming the allowed moves.

**Timestamps.** `applied_at`: the first move to `applied` or any later open stage, or to
`accepted`/`declined`. `first_response_at`: the first move to `screening` or later, or to
`rejected` from `applied` onwards (a rejection is an answer); never for `no_response`.
`closed_at`: set on every close, cleared when undone. Neither of the first two is cleared
by a correction, since the event happened. Every move writes
`opportunity_stage_changes (from, to, at, note? ≤ 500)`. `OpportunityStageChanged` is
raised from phase 7, when the outbox exists.

**Imports never save.** They return a preview that the user checks in the capture dialog and
then saves with `POST /opportunities`: the same fields as `OpportunityInput` (with
`companyName`), plus `source`, `sourceRef`, `parsedFrom`
(`job_board|structured_data|page_text|pasted_text`), `warnings[]` and
`existingOpportunityId?` (already saved under this ref). A URL goes through, in order:

1. **Sites whose terms forbid automated access** (LinkedIn, Indeed, Glassdoor, ZipRecruiter,
   Monster) → 422 `import.not_allowed`, suggesting a paste. PRD principle 4.
2. **Job-board links** (`boards.greenhouse.io`/`job-boards.greenhouse.io/{board}/jobs/{id}`,
   `jobs.lever.co/{board}/{id}`, `jobs.ashbyhq.com/{board}/{id}`) use the provider's public API,
   so they get the full posting and the same ref as saving from a source. Real Greenhouse and
   Ashby pages carry little or nothing a plain fetch can read.
3. **Any other page** is fetched through the SSRF guard (spec 02 §5). `JobPosting` JSON-LD is
   preferred; otherwise heuristics read the title and main text. A page with under 200
   characters of text gets a warning to paste the posting instead (it likely needs JavaScript).

Pasted text is read by heuristics only: labelled lines (`Company:`, `Location:`), the first
line as title, keywords for work mode and type, and a salary only when a currency and an
amount are both stated and the text says it is pay: the line names pay, sits right under a short
pay heading ("Base Salary"), or states its period. A currency code after the amount settles what
a bare `$` meant. Nothing is guessed beyond that, and every field stays editable.

**Dedupe.** `source_ref` is `{board}:{id}` for job boards and the SHA-256 of the URL (no
fragment, no `utm_*`) for other pages; pastes have none. `POST /opportunities` with a ref
already saved updates the posting's fields (title, company, location, type, salary, link,
description) and keeps the stage, priority and history: 200 instead of 201, audit
`opportunity.reimported`.

`job_sources` (≤ 50 per user): `provider` (`greenhouse|lever|ashby`), `board` (the company's
board token, `^[A-Za-z0-9_-]{1,100}$`), `label` ≤ 80 (default: the token, tidied; it names the
company unless the provider states one), `last_checked_at?`. Unique (user, provider, board) →
409 `source.duplicate`; limit → 409 `source.limit`. Adding one checks the board exists (404 →
422 `source.not_found`). A provider failure → 502 `source.unavailable`. Listing a source's
postings is **read-only**: the user picks which postings to save. Postings already saved are
flagged. Unlisted postings are left out.

| Method | Path | Notes |
|--------|------|-------|
| GET | `/opportunities?stage&priority&q&archived&page&pageSize` | |
| POST | `/opportunities` | Manual or from a preview below |
| GET / PUT / DELETE | `/opportunities/{id}` | PUT carries `version` and edits everything but the stage; DELETE archives |
| POST | `/opportunities/{id}/restore` | Undoes an archive |
| POST | `/opportunities/{id}/stage` | `{ stage, note? }`; illegal move → 409 `opportunity.invalid_stage` |
| GET | `/opportunities/{id}/history` | Stage changes, oldest first |
| GET | `/board` | One column per open stage: opportunities by priority (high first) then oldest first; plus counts of closed ones by stage |
| POST | `/imports/text` | `{ text }` (≤ 50,000 chars) → preview |
| POST | `/imports/url` | `{ url }` → preview; rate limit `import`; SSRF rules in spec 02 §5; failures → 422 `import.not_allowed` / `import.unreachable` / `import.unsupported` |
| GET / POST | `/sources` · DELETE `/sources/{id}` | POST rate limit `import` (it calls the provider) |
| GET | `/sources/{id}/postings` | Fetched live, rate limit `import`; `{ items: [{ ref, title, location, url, alreadySaved }] }` |
| POST | `/sources/{id}/postings/{ref}/save` | Saves one posting as an opportunity: 201, or 200 if it was already saved and got updated; gone from the board → 404 `source.posting_gone` |
| GET / POST / PUT | `/companies`, `/companies/{id}` | |

Audit: `opportunity.created|updated|reimported|stage_changed|archived|restored`,
`company.created|updated`, `source.created|deleted`.

## Matching (F3)

`opportunity_requirements` (≤ 50 per opportunity): `text` ≤ 300, `kind` (`must|nice`),
`skill_key?` (from the skill catalog, like a profile skill's key), `years_min?` (0–40),
`origin` (`rules|ai|user`), `confirmed bool`, `position`. Extraction writes unconfirmed rows
and replaces only unconfirmed rows: confirmed ones stay, and a new row for a skill or text
already confirmed is left out. **Only confirmed requirements count toward the score**, so
nothing a parser guessed moves a number until the user has checked it.

`fit_assessments` (one current row per opportunity with confirmed requirements): `score?`
0–100, `band?`, `outcomes jsonb` (each requirement: `covered|partial|gap|not_scored`, why, and
what the profile has), `notes jsonb`, `algorithm_version`, `computed_at` (when the result last
changed).

**Algorithm v1 (deterministic, documented in the UI):**

- Weights: `must` = 3, `nice` = 1. Requirements without a `skill_key` are listed but not
  scored (shown as "not scored").
- Coverage per requirement: **1** when the profile has the skill with level ≥ 3 and years ≥
  `years_min` (if any); **0.5** when the skill is present but under the level or years, or
  when only a related skill from the same catalog family is present; **0** otherwise.
- `score = round(100 × Σ weight × coverage ÷ Σ weight)`, halves away from zero. No score
  ("not enough information") when there are no confirmed requirements, none has a skill, or
  the profile has no skills yet: a 0 would say something the data doesn't.
- Bands: ≥ 75 strong, ≥ 55 good, ≥ 35 partial, below weak.
- Notes, not in the score: work mode, location and seniority mismatches against the
  profile's preferences, only when both sides are stated.
- Suggested priority: `high` for strong fits still in `saved`/`preparing` and less than
  14 days old; `low` for weak fits; otherwise `normal`; none without a score. It applies to
  open, unarchived opportunities (`priority_set_by` = `suggested`, audit
  `opportunity.priority_suggested`) and never overrides a priority the user set. Choosing a
  priority counts as setting it; leaving the default `normal` on a new opportunity doesn't.

Every assessment shows "Estimate based on your profile — not a prediction of the outcome."

**Always current, no stale flag.** Computing a fit is cheap and deterministic, so every read
recomputes it from the current requirements, profile and posting, and writes only if the
result changed. Nothing can go stale: not after a deleted skill (which a timestamp comparison
would miss), a stage move, or a strong fit passing 14 days. The board and list get scores the
same way, through `Matching.Contracts.IFitScores`. Two reads racing to write the same result
are harmless: the loser keeps its computed answer and the next read settles it.

**Extraction.** `IRequirementExtractor` (spec 02 §6). The rule-based extractor reads section
headings (English and Spanish), inline signals ("nice to have", "required", "5+ years") and
catalog skills; alternatives ("Go or Python") become `nice`, because needing either isn't
needing both. Without an API key it is what runs. With one, Claude does, and if that fails or
is declined the rules run instead and the response says so (`fellBackToRules`). A model's row
naming a skill the posting never mentions is dropped; output that fails validation is
rejected whole.

| Method | Path | Notes |
|--------|------|-------|
| GET | `/opportunities/{id}/requirements` | `{ items, extractor }`, where `extractor` is `rules` or `ai` |
| PUT | `/opportunities/{id}/requirements` | `{ items: [{ text, kind, skill?, yearsMin? }] }` replaces the list; all become `user`-confirmed. Same skill twice → 422 |
| POST | `/opportunities/{id}/requirements/extract` | Rules or AI; rate limit `ai` when AI. No posting text → 422 `requirements.no_posting` |
| GET | `/opportunities/{id}/fit` | Computed on read |

Audit: `requirements.extracted`, `requirements.updated`.

## Contacts (F4)

`contacts`: `name` ≤ 120, `company_id?` (`companyId`, or `companyName` matched or created like
an opportunity's), `role?` ≤ 120, `email?` (citext, unique per user when set, archived contacts
included → 409 `contact.duplicate`), `linkedin_url?` (https, linkedin.com and its subdomains
only; a link pasted without a scheme gets `https://`), `phone?` ≤ 40, `notes?` ≤ 2,000,
`version`, `archived_at?`.

`contact_opportunities (contact_id, opportunity_id, relation)`, relation
`recruiter|hiring_manager|referral|interviewer|other`. Linking again changes the relation.

`interactions`: `contact_id`, `opportunity_id?` (kept if the link is later removed: it's what
happened), `channel` (`email|linkedin|phone|video|in_person|other`), `direction`
(`outbound|inbound`), `occurred_at` (not in the future: a planned call is a reminder, not an
interaction), `summary` 1–2,000. `outreach_send_id?` arrives with phase 7, which is what sets it.

Archiving keeps a contact's links and history, and restoring brings them back. An archived
contact can't get new links or interactions (409 `contact.archived`) until restored.

| Method | Path | Notes |
|--------|------|-------|
| GET / POST | `/contacts?q&companyId&archived&page&pageSize` | `q` searches name, role, email and company. Rows report `interactionCount`, `lastInteractionAt` and `opportunityCount` |
| GET / PUT / DELETE | `/contacts/{id}` | GET adds the linked opportunities. PUT carries `version`. DELETE archives |
| POST | `/contacts/{id}/restore` | Undoes an archive |
| POST / DELETE | `/contacts/{id}/opportunities/{opportunityId}` | `{ relation }` |
| GET / POST | `/contacts/{id}/interactions` | Newest first. `opportunityId` not the user's → 422 `interaction.unknown_opportunity`. Raises `InteractionLogged` from phase 7, when the outbox exists |
| DELETE | `/interactions/{id}` | Removes it for good: it's the user's own log |
| GET | `/opportunities/{id}/people` | The contacts linked to it and the interactions about it, newest first |

Audit: `contact.created|updated|archived|restored|linked|unlinked`, `interaction.logged|deleted`.
Summaries and contact details stay out of audit metadata.

Until phase 7 delivers `InteractionLogged`, an inbound interaction doesn't yet mark an application
as responded; moving it to a responding stage does.

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
