# JobHunter OS

The operations center for a job search: the opportunities you're considering, how well each
one fits your real profile, the recruiters you talk to, the follow-ups you owe, and honest
numbers about what is working.

It is phase 2 of a career toolkit. Phase 1, **Outreach Studio**
([Mail Template Hub](https://github.com/GEKKOLAS/smtp)), builds and sends email. JobHunter OS
calls its API to send follow-ups, and only after you approve each one.

> **Status: phases 0–8 done.** Accounts work end to end (sign-up, sign-in, sessions you can see
> and revoke, password reset, a security activity log). So does F1, your profile: skills from a
> 128-entry catalog that folds aliases (`C#` = `csharp`), and the evidence behind them. And so
> does F2: bring opportunities in by hand, from pasted text, from a link (through an SSRF guard)
> or from a company's public Greenhouse, Lever or Ashby board, check what was read, and move them
> across a Kanban board whose stage rules (skips, one-step corrections, undoable closes) are
> checked for every stage pair. F3 reads what a posting asks for (pattern rules, or Claude with a
> key), lets you check it, and scores the confirmed list with deterministic code that shows its
> reasons. F4 keeps the people in your search, what links them to each opportunity and what was
> said. F5 reminds you to follow up (applying adds one, a reply closes it), drafts the email
> (blank, or written by Claude from facts you give it and checked before you see it), and sends it
> through Outreach Studio only once you've approved it. F6 counts how your applications are
> doing (answer, interview and offer rates with their sample sizes, "too few to say" under
> five, by week, source and target role) and which skills postings ask for and you lack. F7,
> the dashboard, shows what's due today, what has gone quiet and what to work on next. Phase 9
> is the launch; see the [implementation plan](docs/spec/04-implementation-plan.md).

## What it will do

| | Feature |
|-|---------|
| F1 | **Profile & evidence**: skills with level and years, and the projects and roles that back them up |
| F2 | **Opportunities**: capture postings by hand, from pasted text, from a link, or from a company's public job board (Greenhouse, Lever, Ashby); a Kanban pipeline with stage history |
| F3 | **Smart matching**: requirements extracted from the posting, a deterministic fit score with covered requirements and gaps. It is an estimate, never a hiring prediction |
| F4 | **Recruiter CRM**: contacts linked to opportunities, with every conversation logged |
| F5 | **Follow-ups**: reminders (some created automatically, e.g. a week after applying) and AI drafts that only use facts from your profile; you approve each email before it is sent through Outreach Studio |
| F6 | **Application analytics**: response, interview and offer rates with their sample sizes, time to first response, most requested skills, most frequent gaps |
| F7 | **Dashboard**: what's due today, what has gone quiet, what to work on next |

The principles behind these features (honest numbers, no invented experience, approval for
anything outward-facing, authorized sources only) are in the [PRD](docs/spec/01-prd.md).

## Architecture

A modular monolith on ASP.NET Core (.NET 10) with Clean Architecture layers. Modules
(Identity, Profile, Opportunities, Matching, Contacts, FollowUps, Analytics, Integrations)
only use each other through their `Contracts` and domain events. Architecture tests enforce
this, and the events go through a transactional outbox. Details:
[architecture](docs/spec/02-architecture.md) · [domain and API](docs/spec/03-domain-and-api.md).

| Layer | Stack |
|-------|-------|
| API | ASP.NET Core 10, EF Core 10, PostgreSQL 16, Serilog, OpenTelemetry |
| Web | Next.js 16, React 19, TypeScript, Tailwind 4, shadcn/ui, TanStack Query, Zod |
| Tests | xUnit, Testcontainers, NetArchTest, Vitest |

The web app shares the toolkit's two themes: **Mailroom** (light) and **Nocturne** (dark).

## Run it locally

Requirements: .NET 10 SDK, Node 22+, Docker.

```bash
docker compose up -d postgres
```

```bash
dotnet run --project src/JobHunter.Api --launch-profile http
```

```bash
npm --prefix web install
```

```bash
npm --prefix web run dev
```

Open http://localhost:3100. The API listens on http://localhost:5101 and PostgreSQL on
port 5433, so JobHunter OS runs next to Mail Template Hub (3000 / 5001 / 5432).

Everything works without an API key: pattern rules read a posting's requirements. To have
Claude read them instead, add a key to the API's user secrets (never to a file in the repo):

```bash
dotnet user-secrets set "Ai:ApiKey" "your-anthropic-key" --project src/JobHunter.Api
```

Without Outreach Studio, approved drafts are copied and marked sent by hand. To send from the
app, run Outreach Studio (`smtp`) on http://localhost:5001, create an API key there, and add it
with the id of a template version whose subject is `{{subject}}` and body shows `{{body}}` in
**Settings → Outreach Studio**. Sending uses your default mailbox there.

Secrets at rest use the development key in `appsettings.Development.json`. Anywhere else the API
refuses to start with it; set `Secrets:Keys:1` (32 random bytes, base64) and
`Secrets:ActiveVersion` instead.

## Tests

```bash
dotnet test JobHunter.slnx
```

```bash
npm --prefix web test
```

Integration tests start a throwaway PostgreSQL container, so Docker must be running.
