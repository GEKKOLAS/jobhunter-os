import { z } from "zod";

export const REQUIREMENT_KINDS = ["must", "nice"] as const;
export type RequirementKind = (typeof REQUIREMENT_KINDS)[number];

const kind = z.enum(REQUIREMENT_KINDS);
const extractor = z.enum(["rules", "ai"]);

export const requirementSchema = z.object({
  id: z.string(),
  text: z.string(),
  kind,
  skillKey: z.string().nullable(),
  skillName: z.string().nullable(),
  yearsMin: z.number().nullable(),
  origin: z.enum(["rules", "ai", "user"]),
  confirmed: z.boolean(),
});
export type Requirement = z.infer<typeof requirementSchema>;

export const requirementsSchema = z.object({
  items: z.array(requirementSchema),
  extractor,
});
export type Requirements = z.infer<typeof requirementsSchema>;

export const extractionSchema = requirementsSchema.extend({ fellBackToRules: z.boolean() });
export type Extraction = z.infer<typeof extractionSchema>;

export const BANDS = ["strong", "good", "partial", "weak"] as const;
export type Band = (typeof BANDS)[number];

export const outcomeSchema = z.object({
  text: z.string(),
  kind,
  skillKey: z.string().nullable(),
  skillName: z.string().nullable(),
  yearsMin: z.number().nullable(),
  coverage: z.enum(["covered", "partial", "gap", "not_scored"]),
  reason: z.enum(["meets", "under_level", "under_years", "under_level_and_years", "related", "missing", "no_skill"]),
  yourLevel: z.number().nullable(),
  yourYears: z.number().nullable(),
  relatedSkill: z.string().nullable(),
});
export type Outcome = z.infer<typeof outcomeSchema>;

export const fitNoteSchema = z.object({
  code: z.enum(["work_mode", "location", "seniority"]),
  posting: z.string(),
  profile: z.string(),
});
export type FitNote = z.infer<typeof fitNoteSchema>;

export const fitSchema = z.object({
  score: z.number().nullable(),
  band: z.enum(BANDS).nullable(),
  missingReason: z.enum(["no_requirements", "no_scored_requirements", "no_skills"]).nullable(),
  outcomes: z.array(outcomeSchema),
  notes: z.array(fitNoteSchema),
  suggestedPriority: z.enum(["high", "normal", "low"]).nullable(),
  algorithmVersion: z.number(),
  computedAt: z.string().nullable(),
});
export type Fit = z.infer<typeof fitSchema>;

/** What the editor holds and what `PUT` takes: the skill by name, which the server folds through the catalog. */
export type RequirementDraft = {
  text: string;
  kind: RequirementKind;
  skill: string;
  yearsMin: number | null;
};

export function toDraft(r: Requirement): RequirementDraft {
  return { text: r.text, kind: r.kind, skill: r.skillName ?? "", yearsMin: r.yearsMin };
}

export function fromDraft(d: RequirementDraft) {
  return { text: d.text.trim(), kind: d.kind, skill: d.skill.trim() || null, yearsMin: d.yearsMin };
}
