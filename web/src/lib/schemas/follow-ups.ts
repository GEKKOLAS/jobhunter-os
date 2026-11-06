import { z } from "zod";

// The value sets of src/JobHunter.Domain/FollowUps/FollowUps.cs.
export const REMINDER_KINDS = ["follow_up", "thank_you", "prep", "custom"] as const;
export const DRAFT_KINDS = ["follow_up", "thank_you", "outreach"] as const;

export type ReminderKind = (typeof REMINDER_KINDS)[number];
export type DraftKind = (typeof DRAFT_KINDS)[number];

export const reminderSchema = z.object({
  id: z.string(),
  title: z.string(),
  dueAt: z.string(),
  snoozedUntil: z.string().nullable(),
  effectiveDueAt: z.string(),
  kind: z.enum(REMINDER_KINDS),
  status: z.enum(["open", "done", "dismissed"]),
  origin: z.enum(["user", "rule"]),
  opportunityId: z.string().nullable(),
  opportunityTitle: z.string().nullable(),
  contactId: z.string().nullable(),
  contactName: z.string().nullable(),
  version: z.number(),
});
export type Reminder = z.infer<typeof reminderSchema>;

export const draftSchema = z.object({
  id: z.string(),
  kind: z.enum(DRAFT_KINDS),
  opportunityId: z.string().nullable(),
  opportunityTitle: z.string().nullable(),
  contactId: z.string().nullable(),
  contactName: z.string().nullable(),
  toEmail: z.string().nullable(),
  subject: z.string(),
  body: z.string(),
  origin: z.enum(["ai", "user"]),
  status: z.enum(["draft", "approved", "sent", "discarded"]),
  approvedAt: z.string().nullable(),
  sentVia: z.enum(["outreach_studio", "manual"]).nullable(),
  outreachSendId: z.string().nullable(),
  sentAt: z.string().nullable(),
  version: z.number(),
  createdAt: z.string(),
  updatedAt: z.string(),
});
export type Draft = z.infer<typeof draftSchema>;

export const createdDraftSchema = z.object({
  draft: draftSchema,
  generated: z.boolean(),
  generationNote: z.string().nullable(),
});
export type CreatedDraft = z.infer<typeof createdDraftSchema>;

export const notificationsSchema = z.object({
  items: z.array(
    z.object({
      id: z.string(),
      title: z.string(),
      reminderId: z.string().nullable(),
      createdAt: z.string(),
      readAt: z.string().nullable(),
    }),
  ),
  unreadCount: z.number(),
});
export type Notifications = z.infer<typeof notificationsSchema>;

export const outreachSettingsSchema = z.object({
  configured: z.boolean(),
  baseUrl: z.string().nullable(),
  templateVersionId: z.string().nullable(),
  keyHint: z.string().nullable(),
  lastTestedAt: z.string().nullable(),
  lastTestOk: z.boolean().nullable(),
});
export type OutreachSettings = z.infer<typeof outreachSettingsSchema>;

export const outreachTestSchema = z.object({
  ok: z.boolean(),
  displayName: z.string().nullable(),
  hasDefaultMailbox: z.boolean(),
  message: z.string().nullable(),
});
export type OutreachTest = z.infer<typeof outreachTestSchema>;

export const aiCapabilitiesSchema = z.object({ extraction: z.enum(["ai", "rules"]), drafts: z.boolean() });

export type ReminderInput = {
  title: string;
  dueAt: string;
  kind: ReminderKind;
  opportunityId?: string | null;
  contactId?: string | null;
};

export type DraftRequest = {
  kind: DraftKind;
  opportunityId: string | null;
  contactId: string | null;
  generate: boolean;
  facts: string[];
};
