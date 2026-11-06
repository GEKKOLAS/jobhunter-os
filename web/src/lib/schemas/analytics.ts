import { BANDS } from "@/lib/schemas/matching";
import { PRIORITIES, STAGES } from "@/lib/schemas/opportunities";
import { z } from "zod";

// The shapes of src/JobHunter.Application/Analytics/AnalyticsModels.cs (spec 03, Analytics).

export const RANGES = [30, 90, 365] as const;
export type RangeDays = (typeof RANGES)[number];

/** A rate with what it was computed from; `value` is null when there are too few to say. */
export const rateSchema = z.object({
  numerator: z.number(),
  denominator: z.number(),
  value: z.number().nullable(),
});
export type Rate = z.infer<typeof rateSchema>;

export const FUNNEL_STEPS = ["saved", "applied", "responded", "interviewing", "offer"] as const;
export type FunnelStepName = (typeof FUNNEL_STEPS)[number];

const breakdownSchema = z.object({
  key: z.string().nullable(),
  applications: z.number(),
  responses: z.number(),
  interviews: z.number(),
  responseRate: rateSchema,
  interviewRate: rateSchema,
});
export type Breakdown = z.infer<typeof breakdownSchema>;

const weekSchema = z.object({
  weekStart: z.string(),
  applications: z.number(),
  responses: z.number(),
  partial: z.boolean(),
});
export type Week = z.infer<typeof weekSchema>;

export const summarySchema = z.object({
  days: z.number(),
  from: z.string(),
  to: z.string(),
  saved: z.number(),
  applications: z.number(),
  rates: z.object({ response: rateSchema, interview: rateSchema, offer: rateSchema, noResponse: rateSchema }),
  timeToFirstResponse: z.object({ days: z.number().nullable(), count: z.number() }),
  funnel: z.array(z.object({ step: z.enum(FUNNEL_STEPS), count: z.number(), rate: rateSchema.nullable() })),
  weekly: z.array(weekSchema),
  bySource: z.array(breakdownSchema),
  byRole: z.array(breakdownSchema),
});
export type Summary = z.infer<typeof summarySchema>;

const skillCount = { key: z.string(), name: z.string(), postings: z.number(), must: z.number(), nice: z.number() };

export const skillsSchema = z.object({
  days: z.number(),
  postings: z.number(),
  requested: z.array(z.object({ ...skillCount, inProfile: z.boolean() })),
  gaps: z.array(z.object(skillCount)),
});
export type SkillsReport = z.infer<typeof skillsSchema>;

const quietItemSchema = z.object({
  id: z.string(),
  title: z.string(),
  companyName: z.string(),
  appliedAt: z.string(),
  daysWaiting: z.number(),
});

const nextUpItemSchema = z.object({
  id: z.string(),
  title: z.string(),
  companyName: z.string(),
  stage: z.enum(STAGES),
  priority: z.enum(PRIORITIES),
  score: z.number().nullable(),
  band: z.enum(BANDS).nullable(),
  createdAt: z.string(),
});
export type NextUpItem = z.infer<typeof nextUpItemSchema>;

export const dashboardSchema = z.object({
  quiet: z.object({ items: z.array(quietItemSchema), total: z.number() }),
  nextUp: z.object({ items: z.array(nextUpItemSchema), total: z.number() }),
  stages: z.record(z.enum(STAGES), z.number()),
});
export type Dashboard = z.infer<typeof dashboardSchema>;
