import { api } from "@/lib/api/client";
import {
  catalogSkillSchema,
  type EvidenceInput,
  evidenceSchema,
  type ProfileInput,
  profileSchema,
  type SkillInput,
  skillSchema,
} from "@/lib/schemas/profile";
import { z } from "zod";

const itemsOf = <T extends z.ZodType>(schema: T) => z.object({ items: z.array(schema) });

export const getProfile = () => api("/profile", { schema: profileSchema });

export const saveProfile = (input: ProfileInput) =>
  api("/profile", { method: "PUT", body: input, schema: profileSchema });

export const listSkills = () => api("/profile/skills", { schema: itemsOf(skillSchema) }).then((r) => r.items);

export const createSkill = (input: SkillInput) => api("/profile/skills", { body: input, schema: skillSchema });

export const updateSkill = (id: string, input: SkillInput & { version: number }) =>
  api(`/profile/skills/${id}`, { method: "PUT", body: input, schema: skillSchema });

export const deleteSkill = (id: string) => api(`/profile/skills/${id}`, { method: "DELETE" });

export const listEvidence = () => api("/profile/evidence", { schema: itemsOf(evidenceSchema) }).then((r) => r.items);

export const createEvidence = (input: EvidenceInput) => api("/profile/evidence", { body: input, schema: evidenceSchema });

export const updateEvidence = (id: string, input: EvidenceInput & { version: number }) =>
  api(`/profile/evidence/${id}`, { method: "PUT", body: input, schema: evidenceSchema });

export const deleteEvidence = (id: string) => api(`/profile/evidence/${id}`, { method: "DELETE" });

export const searchCatalog = (q: string, limit = 6) =>
  api(`/skills/catalog?q=${encodeURIComponent(q)}&limit=${limit}`, { schema: itemsOf(catalogSkillSchema) }).then(
    (r) => r.items,
  );
