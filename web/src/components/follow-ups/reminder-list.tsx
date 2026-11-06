"use client";

import { ReminderDialog } from "@/components/follow-ups/reminder-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { completeReminder, dismissReminder, snoozeReminder } from "@/lib/api/follow-ups";
import { describeApiError } from "@/lib/api/form-errors";
import { groupReminders, REMINDER_KIND_LABELS, snoozeOptions } from "@/lib/follow-ups";
import { queryKeys } from "@/lib/query/query-keys";
import type { Reminder } from "@/lib/schemas/follow-ups";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Check, Pencil, X } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { toast } from "sonner";

const when = new Intl.DateTimeFormat(undefined, { weekday: "short", month: "short", day: "numeric", hour: "numeric", minute: "2-digit" });

/** Open reminders by when they're due, each with done, snooze and dismiss. */
export function ReminderList({ reminders, showSubject = true }: { reminders: Reminder[]; showSubject?: boolean }) {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<Reminder | null>(null);
  const refresh = () => void queryClient.invalidateQueries({ queryKey: queryKeys.reminders });
  const failed = (error: unknown) => toast.error(describeApiError(error).message ?? "Could not change it.");

  const done = useMutation({ mutationFn: completeReminder, onSuccess: () => { toast.success("Done"); refresh(); }, onError: failed });
  const snooze = useMutation({
    mutationFn: ({ id, until }: { id: string; until: Date }) => snoozeReminder(id, until.toISOString()),
    onSuccess: refresh,
    onError: failed,
  });
  const dismiss = useMutation({ mutationFn: dismissReminder, onSuccess: refresh, onError: failed });
  const busy = done.isPending || snooze.isPending || dismiss.isPending;

  return (
    <div className="space-y-5">
      {groupReminders(reminders).map((group) => (
        <section key={group.key} className="space-y-2">
          <h3 className={`text-sm font-semibold ${group.key === "overdue" ? "text-destructive" : ""}`}>
            {group.title} <span className="font-normal text-muted-foreground">({group.items.length})</span>
          </h3>
          <ul className="divide-y">
            {group.items.map((r) => (
              <li key={r.id} className="flex flex-wrap items-center justify-between gap-x-3 gap-y-2 py-2">
                <div className="min-w-0 flex-1 basis-56">
                  <p className="text-sm font-medium break-words">{r.title}</p>
                  <p className="text-xs text-muted-foreground">
                    {[
                      when.format(new Date(r.effectiveDueAt)),
                      r.snoozedUntil && "snoozed",
                      REMINDER_KIND_LABELS[r.kind],
                    ]
                      .filter(Boolean)
                      .join(" · ")}
                    {showSubject && r.opportunityId && r.opportunityTitle && (
                      <>
                        {" · "}
                        <Link href={`/opportunities/${r.opportunityId}`} className="text-link hover:underline">
                          {r.opportunityTitle}
                        </Link>
                      </>
                    )}
                    {showSubject && r.contactId && r.contactName && (
                      <>
                        {" · "}
                        <Link href={`/contacts/${r.contactId}`} className="text-link hover:underline">
                          {r.contactName}
                        </Link>
                      </>
                    )}
                  </p>
                </div>
                <div className="flex min-w-0 flex-wrap items-center gap-1">
                  {r.origin === "rule" && <Badge variant="outline">Automatic</Badge>}
                  <Button variant="outline" size="sm" disabled={busy} onClick={() => done.mutate(r.id)}>
                    <Check />
                    Done
                  </Button>
                  {snoozeOptions().map((option) => (
                    <Button key={option.label} variant="ghost" size="sm" disabled={busy} onClick={() => snooze.mutate({ id: r.id, until: option.until })}>
                      {option.label}
                    </Button>
                  ))}
                  <Button variant="ghost" size="icon-sm" aria-label={`Edit ${r.title}`} onClick={() => setEditing(r)}>
                    <Pencil />
                  </Button>
                  <Button variant="ghost" size="icon-sm" aria-label={`Dismiss ${r.title}`} disabled={busy} onClick={() => dismiss.mutate(r.id)}>
                    <X />
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        </section>
      ))}
      {editing && <ReminderDialog reminder={editing} onClose={() => setEditing(null)} />}
    </div>
  );
}
