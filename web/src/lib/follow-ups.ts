import type { DraftKind, Reminder, ReminderKind } from "@/lib/schemas/follow-ups";

export const REMINDER_KIND_LABELS: Record<ReminderKind, string> = {
  follow_up: "Follow up",
  thank_you: "Thank-you",
  prep: "Prepare",
  custom: "Other",
};

export const DRAFT_KIND_LABELS: Record<DraftKind, string> = {
  follow_up: "Follow-up",
  thank_you: "Thank-you",
  outreach: "First message",
};

export const DRAFT_STATUS_LABELS = { draft: "Draft", approved: "Approved", sent: "Sent", discarded: "Discarded" } as const;

export type ReminderGroup = { key: "overdue" | "today" | "week" | "later"; title: string; items: Reminder[] };

function endOfDay(date: Date, plusDays = 0): Date {
  const end = new Date(date);
  end.setDate(end.getDate() + plusDays);
  end.setHours(23, 59, 59, 999);
  return end;
}

/** Overdue, today, this week, later: by the viewer's own day, which the server can't know. Empty groups are left out. */
export function groupReminders(reminders: Reminder[], now: Date = new Date()): ReminderGroup[] {
  const today = endOfDay(now);
  const week = endOfDay(now, 7);
  const groups: ReminderGroup[] = [
    { key: "overdue", title: "Overdue", items: [] },
    { key: "today", title: "Today", items: [] },
    { key: "week", title: "Next 7 days", items: [] },
    { key: "later", title: "Later", items: [] },
  ];
  for (const reminder of reminders) {
    const due = new Date(reminder.effectiveDueAt);
    const group = due < now ? groups[0] : due <= today ? groups[1] : due <= week ? groups[2] : groups[3];
    group.items.push(reminder);
  }
  return groups.filter((g) => g.items.length > 0);
}

/** The two snoozes worth a button: tomorrow morning, and a week from now at the same time. */
export function snoozeOptions(now: Date = new Date()): { label: string; until: Date }[] {
  const tomorrow = new Date(now);
  tomorrow.setDate(tomorrow.getDate() + 1);
  tomorrow.setHours(9, 0, 0, 0);
  const nextWeek = new Date(now);
  nextWeek.setDate(nextWeek.getDate() + 7);
  return [
    { label: "Tomorrow", until: tomorrow },
    { label: "Next week", until: nextWeek },
  ];
}

/** Each line a fact, trimmed, blanks dropped, at most eight of at most 300 characters: what the writer may say. */
export function parseFacts(text: string): string[] {
  return text
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean)
    .slice(0, 8)
    .map((line) => line.slice(0, 300));
}

/** The draft as one plain text a mail app takes when pasted: subject on top, then the body. */
export function draftAsText(d: { subject: string; body: string }): string {
  return d.subject ? `${d.subject}\n\n${d.body}` : d.body;
}
