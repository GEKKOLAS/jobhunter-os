import { describe, expect, it } from "vitest";
import { addTag, formatPeriod, formatYears, groupSkills } from "@/lib/profile";
import type { Skill } from "@/lib/schemas/profile";

const skill = (name: string, category: Skill["category"], level: number, years = 1): Skill => ({
  id: name,
  name,
  key: name.toLowerCase(),
  category,
  level,
  years,
  lastUsedYear: null,
  evidenceCount: 0,
  version: 1,
});

describe("groupSkills", () => {
  it("orders categories like the catalog and skills strongest first", () => {
    const groups = groupSkills([
      skill("Docker", "cloud", 3),
      skill("TypeScript", "language", 3),
      skill("C#", "language", 5),
      skill("Azure", "cloud", 3, 4),
    ]);

    expect(groups.map((g) => g.category)).toEqual(["language", "cloud"]);
    expect(groups[0].skills.map((s) => s.name)).toEqual(["C#", "TypeScript"]);
    expect(groups[1].skills.map((s) => s.name)).toEqual(["Azure", "Docker"]);
  });
});

describe("formatYears", () => {
  it("is honest about less than a year", () => {
    expect(formatYears(0)).toBe("under a year");
    expect(formatYears(1)).toBe("1 yr");
    expect(formatYears(2.5)).toBe("2.5 yrs");
  });
});

describe("formatPeriod", () => {
  it("formats open, closed and undated periods", () => {
    expect(formatPeriod("2026-07-04", null)).toBe("Jul 2026 – present");
    expect(formatPeriod("2019-01-01", "2021-03-31")).toBe("Jan 2019 – Mar 2021");
    expect(formatPeriod(null, null)).toBeNull();
  });
});

describe("addTag", () => {
  it("trims, ignores duplicates by case, and stops at the limit", () => {
    expect(addTag(["Backend Engineer"], "  backend engineer ")).toEqual(["Backend Engineer"]);
    expect(addTag([], "  ")).toEqual([]);
    expect(addTag(["a", "b"], "c", 2)).toEqual(["a", "b"]);
    expect(addTag(["a"], " Platform Engineer ")).toEqual(["a", "Platform Engineer"]);
  });
});
