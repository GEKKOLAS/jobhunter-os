import { api } from "@/lib/api/client";
import { type Provider, previewSchema, sourcePostingSchema, sourceSchema } from "@/lib/schemas/imports";
import { opportunitySchema } from "@/lib/schemas/opportunities";
import { z } from "zod";

export const importFromUrl = (url: string) => api("/imports/url", { body: { url }, schema: previewSchema });

export const importFromText = (text: string) => api("/imports/text", { body: { text }, schema: previewSchema });

export const listSources = () =>
  api("/sources", { schema: z.object({ items: z.array(sourceSchema) }) }).then((r) => r.items);

export const createSource = (input: { provider: Provider; board: string; label: string | null }) =>
  api("/sources", { body: input, schema: sourceSchema });

export const deleteSource = (id: string) => api(`/sources/${id}`, { method: "DELETE" });

export const listSourcePostings = (id: string) =>
  api(`/sources/${id}/postings`, { schema: z.object({ items: z.array(sourcePostingSchema) }) }).then((r) => r.items);

export const saveSourcePosting = (id: string, postingRef: string) =>
  api(`/sources/${id}/postings/${encodeURIComponent(postingRef)}/save`, { method: "POST", schema: opportunitySchema });
