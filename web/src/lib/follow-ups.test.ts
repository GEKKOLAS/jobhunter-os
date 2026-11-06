import { describe, expect, it } from "vitest";
import { draftAsText, groupReminders, parseFacts, snoozeOptions } from "@/lib/follow-ups";
import type { Reminder } from "@/lib/schemas/follow-ups";

const reminder = (title: string, effectiveDueAt: Date): Reminder => ({
  id: title,
  title,
  dueAt: effectiveDueAt.toISOString(),
  snoozedUntil: null,
  effectiveDueAt: effectiveDueAt.toISOString(),
  kind: "custom",
  status: "open",
  origin: "user",
  opportunityId: null,
  opportunityTitle: null,
  contactId: null,
  contactName: null,
  version: 1,
});

describe("groupReminders", () => {
  const now = new Date(2026, 10, 5, 10, 0);

  it("sorts by the viewer's own day and leaves out empty groups", () => {
    const groups = groupReminders(
      [
        reminder("late", new Date(2026, 10, 5, 9, 0)),
        reminder("tonight", new Date(2026, 10, 5, 23, 0)),
        reminder("friday", new Date(2026, 10, 7, 12, 0)),
        reminder("edge of week", new Date(2026, 10, 12, 23, 30)),
        reminder("next month", new Date(2026, 11, 20, 12, 0)),
      ],
      now,
    );

    expect(groups.map((g) => [g.key, g.items.map((r) => r.title)])).toEqual([
      ["overdue", ["late"]],
      ["today", ["tonight"]],
      ["week", ["friday", "edge of week"]],
      ["later", ["next month"]],
    ]);
    expect(groupReminders([], now)).toEqual([]);
  });
});

describe("snoozeOptions", () => {
  it("offers tomorrow at nine and a week from now", () => {
    const [tomorrow, week] = snoozeOptions(new Date(2026, 10, 5, 22, 30));

    expect(tomorrow.until).toEqual(new Date(2026, 10, 6, 9, 0));
    expect(week.until).toEqual(new Date(2026, 10, 12, 22, 30));
  });
});

describe("parseFacts", () => {
  it("keeps what the user wrote, one fact per line, within the limits", () => {
    expect(parseFacts("  I built the API  \n\n We met on Tuesday \n")).toEqual(["I built the API", "We met on Tuesday"]);
    expect(parseFacts(Array.from({ length: 10 }, (_, i) => `fact ${i}`).join("\n"))).toHaveLength(8);
    expect(parseFacts("x".repeat(400))[0]).toHaveLength(300);
  });
});

describe("draftAsText", () => {
  it("puts the subject first, when there is one", () => {
    expect(draftAsText({ subject: "Following up", body: "Hi Ada" })).toBe("Following up\n\nHi Ada");
    expect(draftAsText({ subject: "", body: "Hi Ada" })).toBe("Hi Ada");
  });
});
