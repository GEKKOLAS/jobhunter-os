import { api } from "@/lib/api/client";
import {
  type ContactInput,
  contactSchema,
  type InteractionInput,
  interactionSchema,
  linkedOpportunitySchema,
  pagedContactsSchema,
  peopleSchema,
  type Relation,
} from "@/lib/schemas/contacts";
import { z } from "zod";

export type ContactFilters = {
  q?: string;
  archived?: boolean;
  page?: number;
  pageSize?: number;
};

export function listContacts(filters: ContactFilters) {
  const params = new URLSearchParams();
  if (filters.q?.trim()) params.set("q", filters.q.trim());
  if (filters.archived) params.set("archived", "true");
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", String(filters.pageSize ?? 25));
  return api(`/contacts?${params}`, { schema: pagedContactsSchema });
}

export const getContact = (id: string) => api(`/contacts/${id}`, { schema: contactSchema });

export const createContact = (input: ContactInput) => api("/contacts", { body: input, schema: contactSchema });

export const updateContact = (id: string, input: ContactInput & { version: number }) =>
  api(`/contacts/${id}`, { method: "PUT", body: input, schema: contactSchema });

export const archiveContact = (id: string) => api(`/contacts/${id}`, { method: "DELETE" });

export const restoreContact = (id: string) => api(`/contacts/${id}/restore`, { method: "POST", schema: contactSchema });

export const linkOpportunity = (contactId: string, opportunityId: string, relation: Relation) =>
  api(`/contacts/${contactId}/opportunities/${opportunityId}`, { body: { relation }, schema: linkedOpportunitySchema });

export const unlinkOpportunity = (contactId: string, opportunityId: string) =>
  api(`/contacts/${contactId}/opportunities/${opportunityId}`, { method: "DELETE" });

export const listInteractions = (contactId: string) =>
  api(`/contacts/${contactId}/interactions`, { schema: z.object({ items: z.array(interactionSchema) }) }).then((r) => r.items);

export const logInteraction = (contactId: string, input: InteractionInput) =>
  api(`/contacts/${contactId}/interactions`, { body: input, schema: interactionSchema });

export const deleteInteraction = (id: string) => api(`/interactions/${id}`, { method: "DELETE" });

export const getPeople = (opportunityId: string) => api(`/opportunities/${opportunityId}/people`, { schema: peopleSchema });
