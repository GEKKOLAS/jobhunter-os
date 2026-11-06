import { api } from "@/lib/api/client";
import {
  aiCapabilitiesSchema,
  createdDraftSchema,
  type DraftRequest,
  draftSchema,
  notificationsSchema,
  outreachSettingsSchema,
  outreachTestSchema,
  type ReminderInput,
  reminderSchema,
} from "@/lib/schemas/follow-ups";
import { z } from "zod";

export type ReminderFilters = { status?: string; until?: string; opportunityId?: string; contactId?: string };

function query(filters: Record<string, string | undefined>) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(filters)) if (value) params.set(key, value);
  return params.size ? `?${params}` : "";
}

export const listReminders = (filters: ReminderFilters = {}) =>
  api(`/reminders${query(filters)}`, { schema: z.object({ items: z.array(reminderSchema) }) }).then((r) => r.items);

export const createReminder = (input: ReminderInput) => api("/reminders", { body: input, schema: reminderSchema });

export const updateReminder = (id: string, input: ReminderInput & { version: number }) =>
  api(`/reminders/${id}`, { method: "PUT", body: input, schema: reminderSchema });

export const completeReminder = (id: string) => api(`/reminders/${id}/done`, { method: "POST", body: {}, schema: reminderSchema });

export const snoozeReminder = (id: string, until: string) =>
  api(`/reminders/${id}/snooze`, { method: "POST", body: { until }, schema: reminderSchema });

export const dismissReminder = (id: string) => api(`/reminders/${id}`, { method: "DELETE" });

export type DraftFilters = { status?: string; opportunityId?: string; contactId?: string };

export const listDrafts = (filters: DraftFilters = {}) =>
  api(`/drafts${query(filters)}`, { schema: z.object({ items: z.array(draftSchema) }) }).then((r) => r.items);

export const createDraft = (request: DraftRequest) => api("/drafts", { body: request, schema: createdDraftSchema });

export const updateDraft = (id: string, edit: { toEmail: string | null; subject: string; body: string; version: number }) =>
  api(`/drafts/${id}`, { method: "PUT", body: edit, schema: draftSchema });

export const approveDraft = (id: string) => api(`/drafts/${id}/approve`, { method: "POST", body: {}, schema: draftSchema });

export const sendDraft = (id: string) => api(`/drafts/${id}/send`, { method: "POST", body: {}, schema: draftSchema });

export const markDraftSent = (id: string) => api(`/drafts/${id}/mark-sent`, { method: "POST", body: {}, schema: draftSchema });

export const discardDraft = (id: string) => api(`/drafts/${id}`, { method: "DELETE" });

export const getNotifications = () => api("/notifications", { schema: notificationsSchema });

export const markNotificationsRead = (ids?: string[]) => api("/notifications/read", { method: "POST", body: { ids: ids ?? null } });

export const getOutreach = () => api("/integrations/outreach", { schema: outreachSettingsSchema });

export const saveOutreach = (input: { baseUrl: string; apiKey: string | null; templateVersionId: string }) =>
  api("/integrations/outreach", { method: "PUT", body: input, schema: outreachSettingsSchema });

export const testOutreach = () => api("/integrations/outreach/test", { method: "POST", body: {}, schema: outreachTestSchema });

export const removeOutreach = () => api("/integrations/outreach", { method: "DELETE" });

export const getAiCapabilities = () => api("/ai/capabilities", { schema: aiCapabilitiesSchema });
