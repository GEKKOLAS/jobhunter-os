import { PRIORITY_LABELS, WORK_MODE_LABELS } from "@/lib/opportunities";
import { LEVEL_LABELS } from "@/lib/profile";
import type { Band, Fit, FitNote, Outcome } from "@/lib/schemas/matching";
import type { Priority } from "@/lib/schemas/opportunities";

export const BAND_LABELS: Record<Band, string> = {
  strong: "Strong fit",
  good: "Good fit",
  partial: "Partial fit",
  weak: "Weak fit",
};

export const BAND_BADGES = { strong: "success", good: "info", partial: "warning", weak: "outline" } as const;

export const FIT_DISCLAIMER = "Estimate based on your profile — not a prediction of the outcome.";

export const MISSING_REASONS: Record<NonNullable<Fit["missingReason"]>, string> = {
  no_requirements: "Nothing to estimate from yet. Read the posting below, check the list and confirm it.",
  no_scored_requirements: "None of the confirmed requirements names a skill, so there's nothing to score.",
  no_skills: "Your profile has no skills yet, so there's nothing to compare. A score of 0 wouldn't be true.",
};

/** "level 2 (beginner)", in the words the profile uses for its 1–5 scale. */
function level(n: number): string {
  return `level ${n} (${LEVEL_LABELS[n]?.toLowerCase() ?? "unknown"})`;
}

function years(n: number): string {
  return n === 1 ? "1 year" : `${n} years`;
}

/** The one line under a requirement that says why it came out this way. */
export function describeOutcome(o: Outcome): string {
  const asked = o.yearsMin === null ? "" : `, and this asks for ${years(o.yearsMin)}`;
  switch (o.reason) {
    case "meets":
      return `You have ${o.skillName} at ${level(o.yourLevel!)} with ${years(o.yourYears!)}.`;
    case "under_level":
      return `You have ${o.skillName} at ${level(o.yourLevel!)}. Full coverage starts at level 3.`;
    case "under_years":
      return `You have ${years(o.yourYears!)} of ${o.skillName}${asked}.`;
    case "under_level_and_years":
      return `You have ${o.skillName} at ${level(o.yourLevel!)} with ${years(o.yourYears!)}${asked}.`;
    case "related":
      return `You don't list ${o.skillName}, but ${o.relatedSkill} is closely related.`;
    case "missing":
      return `${o.skillName} isn't on your profile.`;
    case "no_skill":
      return "This isn't about one skill, so it's listed but not scored.";
  }
}

export function describeNote(note: FitNote): string {
  switch (note.code) {
    case "work_mode":
      return `The posting is ${modeLabel(note.posting)}; you prefer ${note.profile.split(", ").map(modeLabel).join(" or ")}.`;
    case "location":
      return `The posting says ${note.posting}; your profile says ${note.profile}. Check whether that works.`;
    case "seniority":
      return `The title reads as ${note.posting}; your profile says ${note.profile}.`;
  }
}

function modeLabel(mode: string): string {
  return (WORK_MODE_LABELS as Record<string, string>)[mode]?.toLowerCase() ?? mode;
}

/** A section per coverage, in reading order: what you have, then what's short, then what wasn't scored. Empty ones are left out. */
export function groupOutcomes(outcomes: Outcome[]): { title: string; coverage: Outcome["coverage"]; items: Outcome[] }[] {
  const sections = [
    { title: "Covered", coverage: "covered" },
    { title: "Partly covered", coverage: "partial" },
    { title: "Gaps", coverage: "gap" },
    { title: "Not scored", coverage: "not_scored" },
  ] as const;
  return sections
    .map((s) => ({ ...s, items: outcomes.filter((o) => o.coverage === s.coverage) }))
    .filter((s) => s.items.length > 0);
}

/** How much the number rests on: a score from 2 of 18 requirements shouldn't read like one from all 18. */
export function describeBasis(outcomes: Outcome[]): string {
  const scored = outcomes.filter((o) => o.coverage !== "not_scored").length;
  return scored === outcomes.length
    ? `Based on all ${outcomes.length} confirmed requirements.`
    : `Based on ${scored} of ${outcomes.length} confirmed requirements. The rest don't name a skill.`;
}

/** What the suggestion means for this opportunity: applied, or left alone because the user chose. */
export function describeSuggestion(
  suggested: Priority,
  current: Priority,
  setBy: string | null,
): string {
  const label = PRIORITY_LABELS[suggested];
  if (setBy === "user" && current !== suggested) {
    return `Suggested priority: ${label}. You set ${PRIORITY_LABELS[current]}, so that stays.`;
  }
  return `Suggested priority: ${label}.`;
}
