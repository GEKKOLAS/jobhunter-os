import { describe, expect, it } from "vitest";
import { findModule, isActivePath, MODULE_GROUPS, MODULES, PHASES, phaseOf } from "@/lib/modules";

describe("module catalogue", () => {
  it("has unique, URL-safe slugs", () => {
    const slugs = MODULES.map((m) => m.slug);
    expect(new Set(slugs).size).toBe(slugs.length);
    for (const slug of slugs) expect(slug).toMatch(/^[a-z][a-z-]*$/);
  });

  it("builds every module in a phase of the plan and lists it in a nav group", () => {
    for (const m of MODULES) {
      expect(phaseOf(m.phase), m.slug).toBeDefined();
      expect(MODULE_GROUPS).toContain(m.group);
    }
  });

  it("numbers the phases 0..n with exactly one next phase", () => {
    expect(PHASES.map((p) => p.phase)).toEqual(PHASES.map((_, i) => i));
    expect(PHASES.filter((p) => p.status === "next")).toHaveLength(1);
  });
});

describe("isActivePath", () => {
  it("matches the module and its sub-pages only", () => {
    expect(isActivePath("/board", "board")).toBe(true);
    expect(isActivePath("/opportunities/123", "opportunities")).toBe(true);
    expect(isActivePath("/boards", "board")).toBe(false);
  });
});

describe("findModule", () => {
  it("returns undefined for unknown slugs", () => {
    expect(findModule("follow-ups")?.label).toBe("Follow-ups");
    expect(findModule("nope")).toBeUndefined();
  });
});
