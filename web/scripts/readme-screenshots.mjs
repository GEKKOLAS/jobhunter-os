// Builds a demo workspace with fictional data and captures the README screenshots in both themes.
//
// Needs the local stack: `docker compose up -d postgres`, the API on :5101, and the web app served
// from a production build (`npm run build`, then `npx next start -p 3101`), so no dev overlay shows.
// Every company and person here is made up. Dates are moved into the past with SQL, because the
// API stamps everything with "now" and analytics are about time.
//
//   node scripts/readme-screenshots.mjs            (JH_BASE_URL defaults to http://localhost:3101)

import { execFileSync } from "node:child_process";
import { randomBytes } from "node:crypto";
import { mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright";

const BASE = process.env.JH_BASE_URL ?? "http://localhost:3101";
const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const OUT = resolve(ROOT, "docs", "screenshots");
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const ZONE = "Europe/Madrid";
/** Today (or another day) at a wall-clock time in ZONE, as SQL. */
const at = (time, dayOffset = 0) =>
  `((date_trunc('day', now() at time zone '${ZONE}') + interval '${dayOffset} days' + time '${time}') at time zone '${ZONE}')`;

function sql(statements) {
  return execFileSync("docker", ["compose", "exec", "-T", "postgres", "psql", "-U", "jh", "-d", "jobhunter", "-v", "ON_ERROR_STOP=1", "-tA"], {
    cwd: ROOT,
    input: statements,
    encoding: "utf8",
  }).trim();
}

async function launch() {
  // The installed Edge first, so nothing has to be downloaded; Playwright's own Chromium otherwise.
  try {
    return await chromium.launch({ channel: "msedge" });
  } catch {
    return await chromium.launch();
  }
}

// --- The demo workspace -------------------------------------------------------------------------

const SKILLS = [
  ["C#", 5, 8],
  ["ASP.NET Core", 5, 7],
  ["PostgreSQL", 4, 6],
  ["Docker", 4, 5],
  ["TypeScript", 3, 4],
  ["React", 3, 3],
  ["Azure", 3, 3],
  ["Kubernetes", 2, 1],
];

// days: when it was saved, sent and first answered, counted back from today.
const OPPORTUNITIES = [
  { key: "contoso", title: "Senior Backend Engineer", company: "Contoso", source: "greenhouse", path: ["applied", "screening", "interviewing"], days: [27, 25, 21], location: "Remote (EU)", workMode: "remote", salary: [85000, 105000, "EUR"],
    reqs: [["must", "C#", 5], ["must", "ASP.NET Core"], ["must", "PostgreSQL"], ["nice", "Kubernetes"], ["nice", "Apache Kafka"]] },
  { key: "litware", title: "Backend Engineer II", company: "Litware", source: "paste", path: ["applied", "screening", "interviewing", "offer"], days: [52, 50, 47], location: "Madrid", workMode: "hybrid",
    reqs: [["must", "C#"], ["must", "PostgreSQL"], ["nice", "Docker"]] },
  { key: "margies", title: "Platform Engineer", company: "Margie's Travel", source: "lever", path: ["applied", "screening", "interviewing", "rejected"], days: [46, 45, 41], workMode: "remote",
    reqs: [["must", "Kubernetes"], ["must", "Terraform"], ["nice", "Go"]] },
  { key: "fabrikam", title: "Backend Engineer, Payments", company: "Fabrikam", source: "lever", path: ["applied", "screening", "rejected"], days: [19, 18, 16],
    reqs: [["must", "C#"], ["must", "Apache Kafka"], ["nice", "Azure"]] },
  { key: "adventure", title: "Data Platform Engineer", company: "Adventure Works", source: "url", path: ["applied", "screening"], days: [34, 33, 24],
    reqs: [["must", "Python"], ["must", "PostgreSQL"], ["nice", "Apache Kafka"]] },
  { key: "coho", title: "Backend Engineer", company: "Coho Winery", source: "greenhouse", path: ["applied", "rejected"], days: [62, 60, 52],
    reqs: [["must", "C#"], ["nice", "React"]] },
  { key: "northwind", title: "Platform Engineer", company: "Northwind Traders", source: "manual", path: ["applied"], days: [15, 14],
    reqs: [["must", "Kubernetes"], ["must", "Terraform"], ["nice", "Go"]] },
  { key: "tailwind", title: "Backend Developer (.NET)", company: "Tailwind Traders", source: "ashby", path: ["applied"], days: [41, 40],
    reqs: [["must", "C#"], ["must", "Azure"]] },
  { key: "woodgrove", title: "Software Engineer, Core Banking", company: "Woodgrove Bank", source: "greenhouse", path: ["applied"], days: [8, 7],
    reqs: [["must", "C#"], ["must", "PostgreSQL"], ["nice", "Apache Kafka"]] },
  { key: "lucerne", title: "Senior .NET Developer", company: "Lucerne Publishing", source: "url", path: ["applied", "no_response"], days: [76, 75],
    reqs: [["must", "C#"], ["must", "ASP.NET Core"]] },
  { key: "proseware", title: "Staff Backend Engineer", company: "Proseware", source: "manual", path: [], days: [3], priority: "high", location: "Remote", workMode: "remote",
    reqs: [["must", "C#"], ["must", "ASP.NET Core"], ["must", "PostgreSQL"], ["nice", "Docker"]] },
  { key: "wideworld", title: "Site Reliability Engineer", company: "Wide World Importers", source: "lever", path: ["preparing"], days: [6],
    reqs: [["must", "Terraform"], ["must", "Go"], ["nice", "Kubernetes"]] },
  { key: "alpine", title: "Full-stack Engineer (React + .NET)", company: "Alpine Ski House", source: "manual", path: [], days: [20],
    reqs: [["must", "React"], ["must", "TypeScript"], ["must", "C#"]] },
];

const DESCRIPTION = `About the role
We're looking for a backend engineer to own services that move money for thousands of businesses.

What you'll do
- Design and run APIs in C# and ASP.NET Core
- Model data in PostgreSQL and keep queries fast
- Ship small, reviewable changes every day

Requirements
- 5+ years of C#
- Production experience with PostgreSQL

Nice to have
- Kubernetes
- Apache Kafka`;

async function seed(context) {
  const email = `demo-${Date.now()}@example.test`;
  const password = randomBytes(18).toString("base64url");
  const register = await context.request.post(`${BASE}/api/v1/auth/register`, { data: { email, password, displayName: "Alex Rivera" } });
  if (!register.ok()) throw new Error(`register: ${register.status()} ${await register.text()}`);

  const csrf = async () => decodeURIComponent((await context.cookies(BASE)).find((c) => c.name === "jh_csrf")?.value ?? "");
  const call = async (method, path, data) => {
    const response = await context.request.fetch(`${BASE}/api/v1${path}`, { method, data, headers: { "X-CSRF-Token": await csrf() } });
    if (!response.ok()) throw new Error(`${method} ${path}: ${response.status()} ${await response.text()}`);
    const text = await response.text();
    return text ? JSON.parse(text) : null;
  };

  const me = await call("GET", "/me");
  const userId = me.id ?? me.user?.id;

  // Profile, skills and the evidence behind them.
  const profile = await call("GET", "/profile");
  await call("PUT", "/profile", {
    ...profile,
    headline: "Backend engineer: .NET, PostgreSQL and the systems around them",
    summary: "Eight years building APIs and data-heavy services, most recently payments and logistics.",
    location: "Madrid, Spain",
    seniority: "senior",
    targetRoles: ["Backend Engineer", "Platform Engineer"],
    workModes: ["remote", "hybrid"],
    languages: [{ name: "Spanish", level: "native" }, { name: "English", level: "professional" }],
  });
  const skillIds = {};
  for (const [name, level, years] of SKILLS) skillIds[name] = (await call("POST", "/profile/skills", { name, level, years, lastUsedYear: 2026 })).id;
  await call("POST", "/profile/evidence", {
    kind: "role", title: "Senior Backend Engineer", organization: "Northwind Logistics", startDate: "2022-03-01",
    description: "Owned the order-routing API and its PostgreSQL schema; led the move from a monolith to three services.",
    outcome: "Cut p95 latency of route quotes from 900 ms to 180 ms.", links: [], skillIds: [skillIds["C#"], skillIds["ASP.NET Core"], skillIds["PostgreSQL"]],
  });
  await call("POST", "/profile/evidence", {
    kind: "project", title: "Ledger reconciliation service", organization: "Fabrikam Pay", startDate: "2020-01-01", endDate: "2022-02-28",
    description: "Built a nightly reconciliation job and its dashboard in React and TypeScript.",
    outcome: "Found and fixed a rounding issue that affected one in 4,000 settlements.", links: [], skillIds: [skillIds["C#"], skillIds["React"], skillIds["TypeScript"]],
  });

  // Opportunities: captured, read, confirmed and moved, as a user would.
  const ids = {};
  for (const o of OPPORTUNITIES) {
    const created = await call("POST", "/opportunities", {
      title: o.title, companyName: o.company, priority: o.priority ?? null, description: DESCRIPTION,
      location: o.location ?? null, workMode: o.workMode ?? null, employmentType: "full_time",
      salaryMin: o.salary?.[0] ?? null, salaryMax: o.salary?.[1] ?? null, salaryCurrency: o.salary?.[2] ?? null, salaryPeriod: o.salary ? "year" : null,
    });
    ids[o.key] = created.id;
    await call("PUT", `/opportunities/${created.id}/requirements`, {
      items: o.reqs.map(([kind, skill, yearsMin]) => ({
        // Worded the way postings word them; the app labels must and nice itself.
        text: yearsMin ? `${yearsMin}+ years of ${skill}` : kind === "must" ? `Solid ${skill} in production` : `Experience with ${skill}`,
        kind,
        skill,
        yearsMin: yearsMin ?? null,
      })),
    });
    for (const stage of o.path) await call("POST", `/opportunities/${created.id}/stage`, { stage });
  }

  // The people, and what was said.
  const priya = await call("POST", "/contacts", { name: "Priya Raman", companyName: "Contoso", role: "Technical Recruiter", email: "priya.raman@contoso.example" });
  await call("POST", `/contacts/${priya.id}/opportunities/${ids.contoso}`, { relation: "recruiter" });
  const daniel = await call("POST", "/contacts", { name: "Daniel Ortiz", companyName: "Litware", role: "Engineering Manager", email: "daniel.ortiz@litware.example" });
  await call("POST", `/contacts/${daniel.id}/opportunities/${ids.litware}`, { relation: "hiring_manager" });
  const now = Date.now();
  const ago = (d) => new Date(now - d * 86_400_000).toISOString();
  await call("POST", `/contacts/${priya.id}/interactions`, { channel: "linkedin", direction: "outbound", occurredAt: ago(26), summary: "Asked about the backend team before applying.", opportunityId: ids.contoso });
  await call("POST", `/contacts/${priya.id}/interactions`, { channel: "email", direction: "inbound", occurredAt: ago(21), summary: "Invited me to a technical interview with the payments team.", opportunityId: ids.contoso });
  await call("POST", `/contacts/${priya.id}/interactions`, { channel: "video", direction: "outbound", occurredAt: ago(17), summary: "Technical interview: system design for an idempotent payments API.", opportunityId: ids.contoso });
  await call("POST", `/contacts/${daniel.id}/interactions`, { channel: "phone", direction: "inbound", occurredAt: ago(33), summary: "Offer call: details by email this week.", opportunityId: ids.litware });

  // A reminder of their own, and a draft waiting for approval.
  await call("POST", "/reminders", { title: "Prepare system design examples for Contoso", dueAt: new Date(now + 3 * 3_600_000).toISOString(), kind: "prep", opportunityId: ids.contoso });
  const { draft } = await call("POST", "/drafts", { kind: "thank_you", opportunityId: ids.contoso, contactId: priya.id, generate: false, facts: [] });
  const written = await call("PUT", `/drafts/${draft.id}`, {
    toEmail: "priya.raman@contoso.example",
    subject: "Thank you for Tuesday's interview",
    body: [
      "Hi Priya,",
      "",
      "Thank you for setting up the interview with the payments team. I enjoyed the system design discussion, especially the part about idempotent retries.",
      "",
      "Best,",
      "Alex",
    ].join("\n"),
    version: draft.version,
  });
  await call("POST", `/drafts/${written.id}/approve`, {});

  // Let the outbox worker run the reminder rules for every move, then settle what a real
  // search would have done by now: chased the quiet ones, dropped the closed one's follow-up.
  for (let i = 0; i < 40; i++) {
    const pending = Number(sql(`select count(*) from outbox_messages where processed_at is null and payload->>'UserId' = '${userId}';`));
    if (pending === 0) break;
    await sleep(1000);
  }
  const reminders = (await call("GET", "/reminders?status=open")).items;
  for (const r of reminders) {
    if (r.opportunityId === ids.northwind || r.opportunityId === ids.tailwind) await call("POST", `/reminders/${r.id}/done`, {});
    if (r.opportunityId === ids.lucerne) await call("DELETE", `/reminders/${r.id}`);
    // Thank-yous for interviews weeks ago were sent back then.
    if (r.kind === "thank_you" && (r.opportunityId === ids.litware || r.opportunityId === ids.margies)) await call("POST", `/reminders/${r.id}/done`, {});
  }

  // Into the past: when each was saved, sent and answered, and the history to match.
  const updates = OPPORTUNITIES.map((o) => {
    const [saved, sent, answered] = o.days;
    const when = (d) => (d === undefined ? "null" : `now() - interval '${d} days'`);
    return `update opportunities set source = '${o.source}', created_at = ${when(saved)}, applied_at = ${when(sent)}, first_response_at = ${when(answered)} where id = '${ids[o.key]}';`;
  }).join("\n");
  sql(`${updates}
update opportunity_stage_changes c set occurred_at = case
    when c.from_stage is null then o.created_at
    when c.to_stage = 'preparing' then o.created_at + interval '1 day'
    when c.to_stage = 'applied' then o.applied_at
    when c.to_stage = 'screening' then o.first_response_at
    when c.to_stage = 'interviewing' then o.first_response_at + interval '4 days'
    when c.to_stage = 'offer' then o.first_response_at + interval '14 days'
    when c.to_stage = 'rejected' and c.from_stage = 'applied' then o.first_response_at
    when c.to_stage = 'rejected' then o.first_response_at + interval '9 days'
    when c.to_stage = 'no_response' then o.applied_at + interval '45 days'
    else c.occurred_at end
  from opportunities o where o.id = c.opportunity_id and o.user_id = '${userId}';
update opportunities o set closed_at = (select max(occurred_at) from opportunity_stage_changes c where c.opportunity_id = o.id)
  where o.user_id = '${userId}' and o.closed_at is not null;
update reminders set due_at = date_trunc('hour', now()) + interval '2 hours', notified_at = null
  where user_id = '${userId}' and opportunity_id = '${ids.woodgrove}' and status = 'open';
update reminders set due_at = date_trunc('hour', now()) + interval '4 hours', notified_at = null
  where user_id = '${userId}' and opportunity_id = '${ids.contoso}' and kind = 'prep' and status = 'open';
update reminders set due_at = ${at("17:00", -1)}, notified_at = null
  where user_id = '${userId}' and opportunity_id = '${ids.contoso}' and kind = 'thank_you' and status = 'open';`);

  return { ids, contactId: priya.id };
}

// --- The screenshots ----------------------------------------------------------------------------

const scrollTo = (text) => async (page) => {
  await page.getByText(text, { exact: true }).first().evaluate((el) => el.scrollIntoView({ block: "start" }));
  await page.evaluate(() => window.scrollBy(0, -88));
  await sleep(300);
};

async function settle(page) {
  await page.waitForLoadState("networkidle");
  await page.waitForFunction(() => !document.body.innerText.includes("Loading…"));
  await page.evaluate(() => document.fonts.ready);
  await sleep(800);
}

async function shoot(context, theme, name, path, { prepare, fullPage = false, viewport } = {}) {
  const page = await context.newPage();
  if (viewport) await page.setViewportSize(viewport);
  await page.addInitScript((t) => localStorage.setItem("theme", t), theme);
  await page.goto(`${BASE}${path}`);
  await settle(page);
  if (prepare) await prepare(page);
  await page.screenshot({ path: resolve(OUT, `${name}-${theme}.png`), fullPage });
  await page.close();
  console.log(`  ${name}-${theme}.png`);
}

const browser = await launch();
mkdirSync(OUT, { recursive: true });
try {
  const desktop = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: "en-US", timezoneId: ZONE, colorScheme: "light" });
  console.log("Seeding a demo workspace…");
  const { ids, contactId } = await seed(desktop);

  console.log("Capturing:");
  for (const theme of ["light", "dark"]) {
    await shoot(desktop, theme, "dashboard", "/dashboard");
    await shoot(desktop, theme, "board", "/board", { viewport: { width: 1920, height: 960 } });
    // Tall enough for the whole fit panel: covered, partial and the gaps.
    await shoot(desktop, theme, "opportunity", `/opportunities/${ids.contoso}`, { viewport: { width: 1440, height: 1340 } });
    await shoot(desktop, theme, "analytics", "/analytics", { viewport: { width: 1440, height: 1240 } });
    await shoot(desktop, theme, "analytics-skills", "/analytics", { prepare: scrollTo("By source") });
    await shoot(desktop, theme, "follow-ups", "/follow-ups");
    await shoot(desktop, theme, "contact", `/contacts/${contactId}`, { viewport: { width: 1440, height: 1320 } });
    await shoot(desktop, theme, "profile", "/profile", { prepare: scrollTo("Skills") });
  }

  const phone = await browser.newContext({
    viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true, locale: "en-US", timezoneId: ZONE,
    storageState: await desktop.storageState(),
  });
  for (const theme of ["light", "dark"]) await shoot(phone, theme, "phone-dashboard", "/dashboard");
} finally {
  await browser.close();
}
console.log(`Done: ${OUT}`);
