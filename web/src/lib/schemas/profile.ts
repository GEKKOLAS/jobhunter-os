import { z } from "zod";

// The value sets of src/JobHunter.Domain/Profile/ProfileVocabulary.cs.
export const SENIORITIES = ["junior", "mid", "senior", "lead", "principal"] as const;
export const WORK_MODES = ["remote", "hybrid", "onsite"] as const;
export const LANGUAGE_LEVELS = ["basic", "conversational", "professional", "native"] as const;
export const SKILL_CATEGORIES = ["language", "framework", "data", "cloud", "tooling", "practice", "domain", "soft"] as const;
export const EVIDENCE_KINDS = ["project", "role", "certification", "education"] as const;

export type SkillCategory = (typeof SKILL_CATEGORIES)[number];
export type EvidenceKind = (typeof EVIDENCE_KINDS)[number];

export const languageSchema = z.object({ name: z.string(), level: z.enum(LANGUAGE_LEVELS) });
export type Language = z.infer<typeof languageSchema>;

export const profileSchema = z.object({
  headline: z.string(),
  summary: z.string(),
  location: z.string(),
  targetRoles: z.array(z.string()),
  seniority: z.enum(SENIORITIES).nullable(),
  workModes: z.array(z.enum(WORK_MODES)),
  languages: z.array(languageSchema),
  version: z.number(),
  updatedAt: z.string().nullable(),
});
export type Profile = z.infer<typeof profileSchema>;

export const skillSchema = z.object({
  id: z.string(),
  name: z.string(),
  key: z.string(),
  category: z.enum(SKILL_CATEGORIES),
  level: z.number(),
  years: z.number(),
  lastUsedYear: z.number().nullable(),
  evidenceCount: z.number(),
  version: z.number(),
});
export type Skill = z.infer<typeof skillSchema>;

export const linkSchema = z.object({ label: z.string(), url: z.string() });
export type EvidenceLink = z.infer<typeof linkSchema>;

export const evidenceSchema = z.object({
  id: z.string(),
  kind: z.enum(EVIDENCE_KINDS),
  title: z.string(),
  organization: z.string().nullable(),
  startDate: z.string().nullable(),
  endDate: z.string().nullable(),
  description: z.string(),
  outcome: z.string().nullable(),
  links: z.array(linkSchema),
  skillIds: z.array(z.string()),
  version: z.number(),
  createdAt: z.string(),
});
export type Evidence = z.infer<typeof evidenceSchema>;

export const catalogSkillSchema = z.object({
  key: z.string(),
  name: z.string(),
  category: z.enum(SKILL_CATEGORIES),
  family: z.string().nullable(),
});
export type CatalogSkill = z.infer<typeof catalogSkillSchema>;

export type ProfileInput = Omit<Profile, "updatedAt">;

export type SkillInput = {
  name: string;
  category: SkillCategory | null;
  level: number;
  years: number;
  lastUsedYear: number | null;
};

export type EvidenceInput = {
  kind: EvidenceKind;
  title: string;
  organization: string | null;
  startDate: string | null;
  endDate: string | null;
  description: string;
  outcome: string | null;
  links: EvidenceLink[];
  skillIds: string[];
};
