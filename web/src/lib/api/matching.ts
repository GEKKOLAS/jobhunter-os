import { api } from "@/lib/api/client";
import { extractionSchema, fitSchema, fromDraft, type RequirementDraft, requirementsSchema } from "@/lib/schemas/matching";

export const getRequirements = (id: string) => api(`/opportunities/${id}/requirements`, { schema: requirementsSchema });

export const saveRequirements = (id: string, items: RequirementDraft[]) =>
  api(`/opportunities/${id}/requirements`, { method: "PUT", body: { items: items.map(fromDraft) }, schema: requirementsSchema });

export const extractRequirements = (id: string) =>
  api(`/opportunities/${id}/requirements/extract`, { method: "POST", body: {}, schema: extractionSchema });

export const getFit = (id: string) => api(`/opportunities/${id}/fit`, { schema: fitSchema });
