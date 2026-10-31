import { SKILL_CATEGORIES, type Skill, type SkillCategory } from "@/lib/schemas/profile";

export const LEVEL_LABELS: Record<number, string> = {
  1: "Aware",
  2: "Beginner",
  3: "Working",
  4: "Strong",
  5: "Expert",
};

export const CATEGORY_LABELS: Record<SkillCategory, string> = {
  language: "Languages",
  framework: "Frameworks",
  data: "Data & messaging",
  cloud: "Cloud & infrastructure",
  tooling: "Tooling",
  practice: "Practices",
  domain: "Domains",
  soft: "Soft skills",
};

/** Skills in category order (the catalog's), strongest first within each; empty categories dropped. */
export function groupSkills(skills: Skill[]) {
  return SKILL_CATEGORIES.map((category) => ({
    category,
    label: CATEGORY_LABELS[category],
    skills: skills
      .filter((s) => s.category === category)
      .sort((a, b) => b.level - a.level || b.years - a.years || a.name.localeCompare(b.name)),
  })).filter((group) => group.skills.length > 0);
}

export function formatYears(years: number): string {
  if (years === 0) return "under a year";
  return `${years} ${years === 1 ? "yr" : "yrs"}`;
}

const monthYear = new Intl.DateTimeFormat("en", { month: "short", year: "numeric", timeZone: "UTC" });

/** "Jul 2026 – present", "Jan 2019 – Mar 2021", "since Jul 2026"… or null when undated. */
export function formatPeriod(start: string | null, end: string | null): string | null {
  const fmt = (date: string) => monthYear.format(new Date(`${date}T00:00:00Z`));
  if (start && end) return `${fmt(start)} – ${fmt(end)}`;
  if (start) return `${fmt(start)} – present`;
  if (end) return `until ${fmt(end)}`;
  return null;
}

/** Adds a tag unless it is blank or already there (ignoring case); keeps at most `max`. */
export function addTag(tags: string[], value: string, max = 10): string[] {
  const tag = value.trim();
  if (!tag || tags.length >= max || tags.some((t) => t.toLowerCase() === tag.toLowerCase())) return tags;
  return [...tags, tag];
}
