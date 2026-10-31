import type { Opportunity, Priority, Stage } from "@/lib/schemas/opportunities";

export const STAGE_LABELS: Record<Stage, string> = {
  saved: "Saved",
  preparing: "Preparing",
  applied: "Applied",
  screening: "Screening",
  interviewing: "Interviewing",
  offer: "Offer",
  accepted: "Accepted",
  declined: "Declined",
  rejected: "Rejected",
  withdrawn: "Withdrawn",
  no_response: "No response",
};

/** How a move reads in a menu: closes sound like closes, corrections like corrections. */
export function moveLabel(from: Stage, to: Stage, previous?: Stage | null): string {
  if (previous && to === previous && isClosed(from)) return `Undo: back to ${STAGE_LABELS[to]}`;
  if (isClosed(to)) return `Close: ${STAGE_LABELS[to].toLowerCase()}`;
  return `Move to ${STAGE_LABELS[to]}`;
}

export const PRIORITY_LABELS: Record<Priority, string> = { high: "High", normal: "Normal", low: "Low" };

export const WORK_MODE_LABELS = { remote: "Remote", hybrid: "Hybrid", onsite: "On-site" } as const;

export const EMPLOYMENT_LABELS = {
  full_time: "Full-time",
  part_time: "Part-time",
  contract: "Contract",
  internship: "Internship",
} as const;

const CLOSED: Stage[] = ["accepted", "declined", "rejected", "withdrawn", "no_response"];

export function isClosed(stage: Stage): boolean {
  return CLOSED.includes(stage);
}

/** "today", "1 day", "12 days": how long a card has been sitting in its column. */
export function ageLabel(since: string, now: Date = new Date()): string {
  const days = Math.floor((now.getTime() - new Date(since).getTime()) / 86_400_000);
  if (days <= 0) return "today";
  return days === 1 ? "1 day" : `${days} days`;
}

/** "USD 4,000–5,500 / month", "EUR 60,000 / year", or null when the posting gave no salary. */
export function formatSalary(o: Pick<Opportunity, "salaryMin" | "salaryMax" | "salaryCurrency" | "salaryPeriod">): string | null {
  if (o.salaryMin === null && o.salaryMax === null) return null;
  const n = (v: number) => v.toLocaleString("en-US");
  const range =
    o.salaryMin !== null && o.salaryMax !== null && o.salaryMin !== o.salaryMax
      ? `${n(o.salaryMin)}–${n(o.salaryMax)}`
      : n((o.salaryMin ?? o.salaryMax)!);
  return [o.salaryCurrency, range].filter(Boolean).join(" ") + (o.salaryPeriod ? ` / ${o.salaryPeriod}` : "");
}
