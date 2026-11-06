"use client";

import { PageHeader } from "@/components/app/page-header";
import { DraftList } from "@/components/follow-ups/draft-list";
import { ReminderDialog } from "@/components/follow-ups/reminder-dialog";
import { ReminderList } from "@/components/follow-ups/reminder-list";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getNotifications, listDrafts, listReminders, markNotificationsRead } from "@/lib/api/follow-ups";
import { queryKeys } from "@/lib/query/query-keys";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { BellRing, Plus } from "lucide-react";
import { useState } from "react";

const when = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" });

export default function FollowUpsPage() {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const reminders = useQuery({ queryKey: queryKeys.reminderList({}), queryFn: () => listReminders() });
  const drafts = useQuery({ queryKey: queryKeys.draftList({}), queryFn: () => listDrafts() });
  const notifications = useQuery({ queryKey: queryKeys.notifications, queryFn: getNotifications });

  const read = useMutation({
    mutationFn: () => markNotificationsRead(),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.notifications }),
  });

  const unread = notifications.data?.items.filter((n) => !n.readAt) ?? [];
  const open = reminders.data ?? [];

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <PageHeader
        icon={BellRing}
        eyebrow={reminders.data ? `${open.length} open` : undefined}
        title="Follow-ups"
        description="What to chase, and the emails waiting on you. Nothing is sent until you approve it and say so."
        action={
          <Button onClick={() => setAdding(true)}>
            <Plus />
            New reminder
          </Button>
        }
      />

      {unread.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Came due</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <ul className="space-y-1.5 text-sm">
              {unread.map((n) => (
                <li key={n.id} className="flex flex-wrap justify-between gap-x-3">
                  <span className="font-medium">{n.title}</span>
                  <span className="font-mono text-xs text-muted-foreground">{when.format(new Date(n.createdAt))}</span>
                </li>
              ))}
            </ul>
            <Button variant="outline" size="sm" disabled={read.isPending} onClick={() => read.mutate()}>
              Mark all seen
            </Button>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Reminders</CardTitle>
          <CardDescription>Applying adds a follow-up a week later, and an interview a thank-you the next day. A reply closes the follow-up.</CardDescription>
        </CardHeader>
        <CardContent>
          {reminders.isError && <p className="text-sm text-destructive">Could not load your reminders. Reload to try again.</p>}
          {reminders.data && open.length === 0 && <p className="text-sm text-muted-foreground">Nothing to chase right now.</p>}
          {open.length > 0 && <ReminderList reminders={open} />}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Emails waiting on you</CardTitle>
          <CardDescription>Start one from an opportunity&apos;s page. Approve it, then send it or mark it sent.</CardDescription>
        </CardHeader>
        <CardContent>
          {drafts.isError && <p className="text-sm text-destructive">Could not load your drafts. Reload to try again.</p>}
          {drafts.data && drafts.data.length === 0 && <p className="text-sm text-muted-foreground">No drafts.</p>}
          {drafts.data && drafts.data.length > 0 && <DraftList drafts={drafts.data} />}
        </CardContent>
      </Card>

      {adding && <ReminderDialog onClose={() => setAdding(false)} />}
    </div>
  );
}
