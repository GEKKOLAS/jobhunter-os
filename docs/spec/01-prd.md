# 01 — Product requirements

## 1. Problem

A serious job search runs across job boards, careers pages, a CV, recruiter conversations,
reminders and a spreadsheet nobody updates. Three things go wrong:

- **Opportunities get lost.** Postings live in tabs and bookmarks; nobody remembers which
  ones were applied to, when, or what happened next.
- **Effort goes to the wrong places.** Without comparing a posting to one's real profile,
  time goes to poor fits while good fits wait.
- **Follow-ups don't happen.** A polite follow-up a week after applying, or a thank-you after
  an interview, is the cheapest thing that improves response rates, and the first thing
  forgotten.

## 2. User

One job seeker managing their own search: a developer applying to several roles a week,
talking to recruiters, wanting to know what is working. The system is multi-user and
isolates every user's data, but each workspace belongs to one person (no teams in v1).

## 3. Principles

1. **Honest numbers.** Fit scores are *estimates* with their reasons shown; they are never
   called a probability of being hired. Rates show their sample size, and small samples
   say so instead of showing a percentage.
2. **Evidence, not invention.** AI may extract, summarize and draft. It may not add skills,
   experience, metrics or achievements that the profile doesn't contain. Every draft is
   editable and attributable to the facts it used.
3. **Human approval for outward actions.** Nothing is sent, submitted or shared without
   an explicit click on that specific item. Automation prepares; the user decides.
4. **Authorized sources only.** Postings come from the user (manual, paste, a URL they
   open) or from public job-board APIs that companies publish for this purpose. No scraping
   of sites whose terms forbid it, no logged-in automation of job boards.
5. **Quality over volume.** There is no "apply to 50 jobs" feature. The product helps send
   fewer, better applications.

## 4. Scope v1

| # | Feature | What it does |
|---|---------|--------------|
| F1 | Profile & evidence | Headline, target roles, work preferences; skills with level and years; an inventory of evidence (projects, roles, certifications) linked to skills |
| F2 | Opportunities | Capture a posting (manual, paste text, import from URL, or pick from a saved company job board); dedupe; a Kanban pipeline with stage history |
| F3 | Smart matching | Requirements extracted from the posting (rules, or AI when configured), confirmed by the user; a deterministic fit score with covered requirements, gaps and a suggested priority |
| F4 | Recruiter CRM | Contacts linked to companies and opportunities; a log of interactions with channel and direction |
| F5 | Follow-ups | Reminders (manual and rule-based, e.g. 7 days after applying); AI drafts for follow-up and thank-you emails; send through Outreach Studio after approval, or copy and mark as sent |
| F6 | Application analytics | Funnel (saved → applied → response → interview → offer), response and interview rates, time to first response, by source and role; most requested skills and most frequent gaps |
| F7 | Dashboard | Today's reminders, stale applications, top-priority opportunities, pipeline counts |

## 5. Later (not v1)

- CV tailoring and cover letters from the evidence inventory (phase 3, Application Copilot).
- Semantic search over postings and evidence (embeddings, pgvector).
- The "Job Search Autopilot" workflow (discover → evaluate → prepare → review → send →
  follow up) as stateful runs with approval steps (phase 8, Personal AI Workbench).
- Mobile app; reading replies from the mailbox to detect responses automatically.

## 6. Non-goals

- Applying on the user's behalf on job sites, or any automation of third-party sites.
- Predicting hiring outcomes or ranking people.
- Bulk outreach to recruiters. Outreach Studio's own sending limits still apply.
- Enriching contacts from external data brokers. Contacts are what the user enters.

## 7. Done means

Each feature ships with: migrations, audit events for state changes, unit and integration
tests, both themes, and an honest empty state. The README shows real screenshots and the
spec status is updated in the same change.
