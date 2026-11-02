import { STAGES } from "@/lib/schemas/opportunities";
import { z } from "zod";

// The value sets of src/JobHunter.Domain/Contacts/ContactRules.cs.
export const RELATIONS = ["recruiter", "hiring_manager", "referral", "interviewer", "other"] as const;
export const CHANNELS = ["email", "linkedin", "phone", "video", "in_person", "other"] as const;
export const DIRECTIONS = ["outbound", "inbound"] as const;

export type Relation = (typeof RELATIONS)[number];
export type Channel = (typeof CHANNELS)[number];
export type Direction = (typeof DIRECTIONS)[number];

export const linkedOpportunitySchema = z.object({
  opportunityId: z.string(),
  title: z.string(),
  companyName: z.string(),
  stage: z.enum(STAGES),
  archived: z.boolean(),
  relation: z.enum(RELATIONS),
});
export type LinkedOpportunity = z.infer<typeof linkedOpportunitySchema>;

export const contactSchema = z.object({
  id: z.string(),
  name: z.string(),
  company: z.object({ id: z.string(), name: z.string() }).nullable(),
  role: z.string().nullable(),
  email: z.string().nullable(),
  linkedInUrl: z.string().nullable(),
  phone: z.string().nullable(),
  notes: z.string().nullable(),
  archivedAt: z.string().nullable(),
  version: z.number(),
  createdAt: z.string(),
  updatedAt: z.string(),
  interactionCount: z.number(),
  lastInteractionAt: z.string().nullable(),
  opportunityCount: z.number(),
  opportunities: z.array(linkedOpportunitySchema),
});
export type Contact = z.infer<typeof contactSchema>;

export const pagedContactsSchema = z.object({
  items: z.array(contactSchema),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
});

export const interactionSchema = z.object({
  id: z.string(),
  contactId: z.string(),
  contactName: z.string(),
  opportunityId: z.string().nullable(),
  opportunityTitle: z.string().nullable(),
  channel: z.enum(CHANNELS),
  direction: z.enum(DIRECTIONS),
  occurredAt: z.string(),
  summary: z.string(),
});
export type Interaction = z.infer<typeof interactionSchema>;

export const peopleSchema = z.object({
  contacts: z.array(
    z.object({
      contactId: z.string(),
      name: z.string(),
      role: z.string().nullable(),
      relation: z.enum(RELATIONS),
      lastInteractionAt: z.string().nullable(),
    }),
  ),
  interactions: z.array(interactionSchema),
});
export type People = z.infer<typeof peopleSchema>;

export type ContactInput = {
  name: string;
  companyName: string | null;
  role: string | null;
  email: string | null;
  linkedInUrl: string | null;
  phone: string | null;
  notes: string | null;
};

export type InteractionInput = {
  channel: Channel;
  direction: Direction;
  occurredAt: string;
  summary: string;
  opportunityId: string | null;
};
