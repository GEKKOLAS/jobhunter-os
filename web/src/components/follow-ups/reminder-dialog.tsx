"use client";

import { Field } from "@/components/profile/field";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { NativeSelect } from "@/components/ui/native-select";
import { toLocalInput } from "@/lib/contacts";
import { createReminder, updateReminder } from "@/lib/api/follow-ups";
import { describeApiError, fieldError, isConflict, NO_ERRORS, type FormErrors } from "@/lib/api/form-errors";
import { REMINDER_KIND_LABELS } from "@/lib/follow-ups";
import { queryKeys } from "@/lib/query/query-keys";
import { REMINDER_KINDS, type Reminder, type ReminderKind } from "@/lib/schemas/follow-ups";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";

/** A reminder of your own, or an edit of one. What it's about is set when it's made. Mount it fresh for each use. */
export function ReminderDialog({
  reminder,
  opportunityId,
  contactId,
  onClose,
}: {
  reminder?: Reminder;
  opportunityId?: string;
  contactId?: string;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const tomorrow = new Date();
  tomorrow.setDate(tomorrow.getDate() + 1);
  tomorrow.setHours(9, 0, 0, 0);

  const [title, setTitle] = useState(reminder?.title ?? "");
  const [kind, setKind] = useState<ReminderKind>(reminder?.kind ?? "custom");
  const [dueAt, setDueAt] = useState(toLocalInput(reminder ? new Date(reminder.dueAt) : tomorrow));
  const [errors, setErrors] = useState<FormErrors>(NO_ERRORS);

  const save = useMutation({
    mutationFn: () => {
      const input = { title, kind, dueAt: new Date(dueAt).toISOString() };
      return reminder
        ? updateReminder(reminder.id, { ...input, version: reminder.version })
        : createReminder({ ...input, opportunityId: opportunityId ?? null, contactId: contactId ?? null });
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.reminders });
      toast.success(reminder ? "Saved" : "Reminder set");
      onClose();
    },
    onError: (error) => {
      if (isConflict(error)) {
        toast.error(describeApiError(error).message);
        void queryClient.invalidateQueries({ queryKey: queryKeys.reminders });
        onClose();
      } else {
        setErrors(describeApiError(error));
      }
    },
  });

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{reminder ? "Edit reminder" : "New reminder"}</DialogTitle>
          <DialogDescription>It shows up here, and as a notification, when it&apos;s due.</DialogDescription>
        </DialogHeader>
        <form
          id="reminder-form"
          className="space-y-4"
          noValidate
          onSubmit={(e) => {
            e.preventDefault();
            setErrors(NO_ERRORS);
            save.mutate();
          }}
        >
          {errors.message && (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {errors.message}
            </p>
          )}
          <Field id="reminder-title" label="What to do" error={fieldError(errors, "title")}>
            <Input id="reminder-title" maxLength={160} value={title} onChange={(e) => setTitle(e.target.value)} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field id="reminder-due" label="When" error={fieldError(errors, "dueAt")}>
              <Input id="reminder-due" type="datetime-local" value={dueAt} onChange={(e) => setDueAt(e.target.value)} />
            </Field>
            <Field id="reminder-kind" label="Kind" error={fieldError(errors, "kind")}>
              <NativeSelect id="reminder-kind" value={kind} onChange={(e) => setKind(e.target.value as ReminderKind)}>
                {REMINDER_KINDS.map((k) => (
                  <option key={k} value={k}>
                    {REMINDER_KIND_LABELS[k]}
                  </option>
                ))}
              </NativeSelect>
            </Field>
          </div>
        </form>
        <DialogFooter>
          <Button type="submit" form="reminder-form" disabled={save.isPending || !title.trim() || !dueAt}>
            {save.isPending ? "Saving…" : reminder ? "Save" : "Set reminder"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
