"use client";

import { DraftList } from "@/components/follow-ups/draft-list";
import { NewDraftDialog } from "@/components/follow-ups/new-draft-dialog";
import { ReminderDialog } from "@/components/follow-ups/reminder-dialog";
import { ReminderList } from "@/components/follow-ups/reminder-list";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getPeople } from "@/lib/api/contacts";
import { listDrafts, listReminders } from "@/lib/api/follow-ups";
import { queryKeys } from "@/lib/query/query-keys";
import type { Opportunity } from "@/lib/schemas/opportunities";
import { useQuery } from "@tanstack/react-query";
import { BellPlus, Mail } from "lucide-react";
import { useState } from "react";

/** What's next on this opportunity: its reminders and the emails waiting on you. */
export function OpportunityFollowUpsCard({ opportunity }: { opportunity: Opportunity }) {
  const reminders = useQuery({
    queryKey: queryKeys.reminderList({ opportunityId: opportunity.id }),
    queryFn: () => listReminders({ opportunityId: opportunity.id }),
  });
  const drafts = useQuery({
    queryKey: queryKeys.draftList({ opportunityId: opportunity.id }),
    queryFn: () => listDrafts({ opportunityId: opportunity.id }),
  });
  const people = useQuery({ queryKey: queryKeys.people(opportunity.id), queryFn: () => getPeople(opportunity.id) });
  const [reminding, setReminding] = useState(false);
  const [drafting, setDrafting] = useState(false);

  const nothing = reminders.data?.length === 0 && drafts.data?.length === 0;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Follow-ups</CardTitle>
        <CardDescription>Reminders and emails for this one. Nothing is sent until you approve it and say so.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {nothing && <p className="text-sm text-muted-foreground">Nothing pending. Applying adds a follow-up reminder a week later.</p>}
        {reminders.data && reminders.data.length > 0 && <ReminderList reminders={reminders.data} showSubject={false} />}
        {drafts.data && drafts.data.length > 0 && (
          <section className="space-y-2">
            <h3 className="text-sm font-semibold">Emails</h3>
            <DraftList drafts={drafts.data} />
          </section>
        )}
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" size="sm" onClick={() => setReminding(true)}>
            <BellPlus />
            Remind me
          </Button>
          <Button variant="outline" size="sm" onClick={() => setDrafting(true)}>
            <Mail />
            Write an email
          </Button>
        </div>
      </CardContent>
      {reminding && <ReminderDialog opportunityId={opportunity.id} onClose={() => setReminding(false)} />}
      {drafting && (
        <NewDraftDialog
          opportunityId={opportunity.id}
          people={(people.data?.contacts ?? []).map((p) => ({ contactId: p.contactId, name: p.name }))}
          initialKind={opportunity.stage === "interviewing" ? "thank_you" : "follow_up"}
          onClose={() => setDrafting(false)}
        />
      )}
    </Card>
  );
}
