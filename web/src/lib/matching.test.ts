import { describe, expect, it } from "vitest";
import { describeBasis, describeNote, describeOutcome, describeSuggestion, groupOutcomes } from "@/lib/matching";
import { fromDraft, toDraft, type Outcome, type Requirement } from "@/lib/schemas/matching";

const outcome = (over: Partial<Outcome>): Outcome => ({
  text: "Needs C#",
  kind: "must",
  skillKey: "csharp",
  skillName: "C#",
  yearsMin: null,
  coverage: "covered",
  reason: "meets",
  yourLevel: null,
  yourYears: null,
  relatedSkill: null,
  ...over,
});

describe("describeOutcome", () => {
  it("says what you have when it meets the requirement", () => {
    expect(describeOutcome(outcome({ yourLevel: 4, yourYears: 6 }))).toBe("You have C# at level 4 (strong) with 6 years.");
    expect(describeOutcome(outcome({ yourLevel: 3, yourYears: 1 }))).toBe("You have C# at level 3 (working) with 1 year.");
  });

  it("says what falls short and what it asks for", () => {
    expect(describeOutcome(outcome({ reason: "under_level", yourLevel: 2, yourYears: 6 }))).toBe(
      "You have C# at level 2 (beginner). Full coverage starts at level 3.",
    );
    expect(describeOutcome(outcome({ reason: "under_years", yourLevel: 4, yourYears: 2.5, yearsMin: 5 }))).toBe(
      "You have 2.5 years of C#, and this asks for 5 years.",
    );
    expect(describeOutcome(outcome({ reason: "under_level_and_years", yourLevel: 1, yourYears: 1, yearsMin: 5 }))).toBe(
      "You have C# at level 1 (aware) with 1 year, and this asks for 5 years.",
    );
  });

  it("names the relative, the gap and the unscored", () => {
    expect(describeOutcome(outcome({ reason: "related", relatedSkill: "F#" }))).toBe("You don't list C#, but F# is closely related.");
    expect(describeOutcome(outcome({ reason: "missing" }))).toBe("C# isn't on your profile.");
    expect(describeOutcome(outcome({ reason: "no_skill", skillName: null }))).toContain("not scored");
  });
});

describe("describeNote", () => {
  it("reads as a sentence with the labels people use", () => {
    expect(describeNote({ code: "work_mode", posting: "onsite", profile: "remote, hybrid" })).toBe(
      "The posting is on-site; you prefer remote or hybrid.",
    );
    expect(describeNote({ code: "location", posting: "Berlin", profile: "Bogotá" })).toContain("Berlin");
    expect(describeNote({ code: "seniority", posting: "senior", profile: "mid" })).toBe("The title reads as senior; your profile says mid.");
  });
});

describe("groupOutcomes", () => {
  it("orders sections by what you have first and drops empty ones", () => {
    const groups = groupOutcomes([
      outcome({ text: "gap", coverage: "gap" }),
      outcome({ text: "covered", coverage: "covered" }),
      outcome({ text: "unscored", coverage: "not_scored" }),
    ]);

    expect(groups.map((g) => g.title)).toEqual(["Covered", "Gaps", "Not scored"]);
    expect(groups[1].items.map((o) => o.text)).toEqual(["gap"]);
  });
});

describe("describeBasis", () => {
  it("says how much of the list the number rests on", () => {
    const scored = outcome({});
    const unscored = outcome({ coverage: "not_scored", reason: "no_skill", skillName: null });

    expect(describeBasis([scored, scored])).toBe("Based on all 2 confirmed requirements.");
    expect(describeBasis([scored, unscored, unscored])).toBe("Based on 1 of 3 confirmed requirements. The rest don't name a skill.");
  });
});

describe("describeSuggestion", () => {
  it("is plain when it applied or agrees", () => {
    expect(describeSuggestion("high", "high", "suggested")).toBe("Suggested priority: High.");
    expect(describeSuggestion("high", "high", "user")).toBe("Suggested priority: High.");
  });

  it("says your own choice stays", () => {
    expect(describeSuggestion("high", "low", "user")).toBe("Suggested priority: High. You set Low, so that stays.");
  });
});

describe("requirement drafts", () => {
  const requirement: Requirement = {
    id: "r1",
    text: "5+ years of C#",
    kind: "must",
    skillKey: "csharp",
    skillName: "C#",
    yearsMin: 5,
    origin: "rules",
    confirmed: false,
  };

  it("round-trips the skill by name", () => {
    expect(fromDraft(toDraft(requirement))).toEqual({ text: "5+ years of C#", kind: "must", skill: "C#", yearsMin: 5 });
  });

  it("sends a blank skill as none and trims", () => {
    expect(fromDraft({ text: "  A degree ", kind: "nice", skill: "  ", yearsMin: null })).toEqual({
      text: "A degree",
      kind: "nice",
      skill: null,
      yearsMin: null,
    });
  });
});
