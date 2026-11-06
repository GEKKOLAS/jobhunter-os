import type { FunnelStepName, Rate } from "@/lib/schemas/analytics";

// How analytics read (spec 03, Analytics; PRD principle 1): a number with what it came from,
// and "too few to say" instead of a percentage when the sample is small.

export const SOURCE_LABELS: Record<string, string> = {
  manual: "Typed in",
  paste: "Pasted text",
  url: "From a link",
  greenhouse: "Greenhouse",
  lever: "Lever",
  ashby: "Ashby",
};

export const FUNNEL_LABELS: Record<FunnelStepName, string> = {
  saved: "Saved",
  applied: "Applied",
  responded: "Got an answer",
  interviewing: "Interviewing",
  offer: "Offer",
};

export const RANGE_LABELS = { 30: "30 days", 90: "90 days", 365: "12 months" } as const;

/** The viewer's IANA zone, for the weekly buckets. */
export function viewerTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";
  } catch {
    return "UTC";
  }
}

export function percent(value: number): string {
  return `${Math.round(value * 100)}%`;
}

export type Reading = {
  /** What the tile shows large. */
  headline: string;
  /** What it was computed from. */
  detail: string;
  /** False when there's no number to show yet. */
  known: boolean;
};

export function describeRate(rate: Rate): Reading {
  const detail = `${rate.numerator} of ${rate.denominator}`;
  if (rate.denominator === 0) return { headline: "None yet", detail: "Nothing to count", known: false };
  if (rate.value === null) return { headline: "Too few to say", detail, known: false };
  return { headline: percent(rate.value), detail, known: true };
}

export function days(value: number): string {
  return `${value} ${value === 1 ? "day" : "days"}`;
}

export function describeMedian(median: { days: number | null; count: number }): Reading {
  const detail = `${median.count} ${median.count === 1 ? "answer" : "answers"}`;
  if (median.count === 0) return { headline: "None yet", detail: "No answers yet", known: false };
  if (median.days === null) return { headline: "Too few to say", detail, known: false };
  return { headline: days(median.days), detail, known: true };
}

/** "2026-10-05" as "Oct 5", read as a calendar date (no time zone shift). */
export function weekLabel(weekStart: string, locale?: string): string {
  const [y, m, d] = weekStart.split("-").map(Number);
  return new Intl.DateTimeFormat(locale, { month: "short", day: "numeric" }).format(new Date(y, m - 1, d));
}

/** Round axis ticks from 0 for small counts: at most five steps, steps of 1, 2 or 5 × 10ⁿ. */
export function countTicks(max: number): number[] {
  const top = Math.max(1, Math.ceil(max));
  let step = 1;
  for (let magnitude = 1; ; magnitude *= 10) {
    const found = [1, 2, 5].map((s) => s * magnitude).find((s) => Math.ceil(top / s) <= 5);
    if (found) {
      step = found;
      break;
    }
  }
  const ticks: number[] = [];
  for (let t = 0; t < top + step; t += step) ticks.push(t);
  return ticks;
}

/** The end of the viewer's day: reminders due by then are today's. */
export function endOfToday(now: Date = new Date()): Date {
  return new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59, 999);
}
