import { z } from "zod";

// The value sets of src/JobHunter.Domain/Opportunities/OpportunityVocabulary.cs.
export const OPEN_STAGES = ["saved", "preparing", "applied", "screening", "interviewing", "offer"] as const;
export const CLOSED_STAGES = ["accepted", "declined", "rejected", "withdrawn", "no_response"] as const;
export const STAGES = [...OPEN_STAGES, ...CLOSED_STAGES] as const;
export const PRIORITIES = ["low", "normal", "high"] as const;
export const WORK_MODES = ["remote", "hybrid", "onsite"] as const;
export const EMPLOYMENT_TYPES = ["full_time", "part_time", "contract", "internship"] as const;
export const SALARY_PERIODS = ["year", "month", "hour"] as const;

export type Stage = (typeof STAGES)[number];
export type OpenStage = (typeof OPEN_STAGES)[number];
export type Priority = (typeof PRIORITIES)[number];

const stage = z.enum(STAGES);

export const companySchema = z.object({
  id: z.string(),
  name: z.string(),
  website: z.string().nullable(),
  domain: z.string().nullable(),
  notes: z.string().nullable(),
  version: z.number(),
});
export type Company = z.infer<typeof companySchema>;

export const opportunityCardSchema = z.object({
  id: z.string(),
  title: z.string(),
  companyId: z.string(),
  companyName: z.string(),
  location: z.string().nullable(),
  workMode: z.enum(WORK_MODES).nullable(),
  stage,
  allowedMoves: z.array(stage),
  priority: z.enum(PRIORITIES),
  createdAt: z.string(),
  updatedAt: z.string(),
  appliedAt: z.string().nullable(),
  archivedAt: z.string().nullable(),
});
export type OpportunityCard = z.infer<typeof opportunityCardSchema>;

export const opportunitySchema = z.object({
  id: z.string(),
  title: z.string(),
  company: companySchema,
  location: z.string().nullable(),
  workMode: z.enum(WORK_MODES).nullable(),
  employmentType: z.enum(EMPLOYMENT_TYPES).nullable(),
  salaryMin: z.number().nullable(),
  salaryMax: z.number().nullable(),
  salaryCurrency: z.string().nullable(),
  salaryPeriod: z.enum(SALARY_PERIODS).nullable(),
  source: z.string(),
  sourceUrl: z.string().nullable(),
  description: z.string(),
  stage,
  previousStage: stage.nullable(),
  allowedMoves: z.array(stage),
  priority: z.enum(PRIORITIES),
  prioritySetBy: z.string().nullable(),
  appliedAt: z.string().nullable(),
  firstResponseAt: z.string().nullable(),
  closedAt: z.string().nullable(),
  archivedAt: z.string().nullable(),
  version: z.number(),
  createdAt: z.string(),
  updatedAt: z.string(),
});
export type Opportunity = z.infer<typeof opportunitySchema>;

export const stageChangeSchema = z.object({
  id: z.string(),
  fromStage: stage.nullable(),
  toStage: stage,
  occurredAt: z.string(),
  note: z.string().nullable(),
});
export type StageChange = z.infer<typeof stageChangeSchema>;

export const boardSchema = z.object({
  columns: z.array(z.object({ stage: z.enum(OPEN_STAGES), items: z.array(opportunityCardSchema) })),
  closedCounts: z.record(z.string(), z.number()),
});
export type Board = z.infer<typeof boardSchema>;

export const pagedOpportunitiesSchema = z.object({
  items: z.array(opportunityCardSchema),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
});

export type OpportunityInput = {
  title: string;
  companyName: string;
  location: string | null;
  workMode: Opportunity["workMode"];
  employmentType: Opportunity["employmentType"];
  salaryMin: number | null;
  salaryMax: number | null;
  salaryCurrency: string | null;
  salaryPeriod: Opportunity["salaryPeriod"];
  sourceUrl: string | null;
  description: string;
  priority: Priority;
  stage?: OpenStage;
};
