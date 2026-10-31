import { api } from "@/lib/api/client";
import {
  boardSchema,
  companySchema,
  type OpportunityInput,
  opportunitySchema,
  pagedOpportunitiesSchema,
  type Stage,
  stageChangeSchema,
} from "@/lib/schemas/opportunities";
import { z } from "zod";

export type OpportunityFilters = {
  stage?: string;
  priority?: string;
  q?: string;
  archived?: boolean;
  page?: number;
};

export const getBoard = () => api("/board", { schema: boardSchema });

export function listOpportunities(filters: OpportunityFilters) {
  const params = new URLSearchParams();
  if (filters.stage) params.set("stage", filters.stage);
  if (filters.priority) params.set("priority", filters.priority);
  if (filters.q?.trim()) params.set("q", filters.q.trim());
  if (filters.archived) params.set("archived", "true");
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", "25");
  return api(`/opportunities?${params}`, { schema: pagedOpportunitiesSchema });
}

export const getOpportunity = (id: string) => api(`/opportunities/${id}`, { schema: opportunitySchema });

export const createOpportunity = (input: OpportunityInput) =>
  api("/opportunities", { body: input, schema: opportunitySchema });

export const updateOpportunity = (id: string, input: OpportunityInput & { version: number }) =>
  api(`/opportunities/${id}`, { method: "PUT", body: input, schema: opportunitySchema });

export const moveOpportunity = (id: string, stage: Stage, note?: string) =>
  api(`/opportunities/${id}/stage`, { body: { stage, note: note?.trim() || null }, schema: opportunitySchema });

export const archiveOpportunity = (id: string) => api(`/opportunities/${id}`, { method: "DELETE" });

export const restoreOpportunity = (id: string) =>
  api(`/opportunities/${id}/restore`, { method: "POST", schema: opportunitySchema });

export const getHistory = (id: string) =>
  api(`/opportunities/${id}/history`, { schema: z.object({ items: z.array(stageChangeSchema) }) }).then((r) => r.items);

export const listCompanies = () =>
  api("/companies", { schema: z.object({ items: z.array(companySchema) }) }).then((r) => r.items);
