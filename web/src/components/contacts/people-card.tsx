"use client";

import { ContactDialog } from "@/components/contacts/contact-dialog";
import { InteractionForm } from "@/components/contacts/interaction-form";
import { InteractionList } from "@/components/contacts/interaction-list";
import { Field } from "@/components/profile/field";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { NativeSelect } from "@/components/ui/native-select";
import { getPeople, linkOpportunity, listContacts, unlinkOpportunity } from "@/lib/api/contacts";
import { describeApiError } from "@/lib/api/form-errors";
import { lastTalked, RELATION_LABELS } from "@/lib/contacts";
import { useDebounced } from "@/lib/hooks/use-debounced";
import { queryKeys } from "@/lib/query/query-keys";
import { RELATIONS, type Relation } from "@/lib/schemas/contacts";
import type { Opportunity } from "@/lib/schemas/opportunities";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { MessageSquarePlus, Plus, X } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { toast } from "sonner";

/** Who's involved in an opportunity, and what's been said about it. */
export function PeopleCard({ opportunity }: { opportunity: Opportunity }) {
  const queryClient = useQueryClient();
  const people = useQuery({ queryKey: queryKeys.people(opportunity.id), queryFn: () => getPeople(opportunity.id) });
  const [adding, setAdding] = useState(false);
  const [logging, setLogging] = useState<string | null>(null);

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.people(opportunity.id) });
    void queryClient.invalidateQueries({ queryKey: queryKeys.contacts });
  };

  const unlink = useMutation({
    mutationFn: (contactId: string) => unlinkOpportunity(contactId, opportunity.id),
    onSuccess: refresh,
    onError: (error) => toast.error(describeApiError(error).message ?? "Could not remove them."),
  });

  const contacts = people.data?.contacts ?? [];
  const interactions = people.data?.interactions ?? [];

  return (
    <Card>
      <CardHeader>
        <CardTitle>People</CardTitle>
        <CardDescription>Who you&apos;re talking to about this one, and what was said.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {people.isError && <p className="text-sm text-destructive">Could not load the people on this. Reload to try again.</p>}
        {people.data && contacts.length === 0 && !adding && <p className="text-sm text-muted-foreground">Nobody linked yet.</p>}

        {contacts.length > 0 && (
          <ul className="divide-y">
            {contacts.map((person) => (
              <li key={person.contactId} className="py-2">
                <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1">
                  <div className="min-w-0 flex-1 basis-48">
                    <Link href={`/contacts/${person.contactId}`} className="block truncate text-sm font-medium hover:underline">
                      {person.name}
                    </Link>
                    <p className="truncate text-xs text-muted-foreground">
                      {[RELATION_LABELS[person.relation], person.role, person.lastInteractionAt && `talked ${lastTalked(person.lastInteractionAt)}`]
                        .filter(Boolean)
                        .join(" · ")}
                    </p>
                  </div>
                  <div className="flex shrink-0 items-center gap-1">
                    <Button variant="outline" size="sm" onClick={() => setLogging(logging === person.contactId ? null : person.contactId)}>
                      <MessageSquarePlus />
                      Log
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon-sm"
                      aria-label={`Remove ${person.name} from this opportunity`}
                      disabled={unlink.isPending}
                      onClick={() => unlink.mutate(person.contactId)}
                    >
                      <X />
                    </Button>
                  </div>
                </div>
                {logging === person.contactId && (
                  <div className="mt-3 rounded-lg border-2 p-3 dark:border">
                    <InteractionForm
                      contactId={person.contactId}
                      fixedOpportunityId={opportunity.id}
                      onLogged={() => {
                        setLogging(null);
                        refresh();
                      }}
                      onCancel={() => setLogging(null)}
                    />
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}

        {adding ? (
          <AddPerson opportunityId={opportunity.id} onDone={() => setAdding(false)} onLinked={refresh} />
        ) : (
          <Button variant="outline" size="sm" onClick={() => setAdding(true)}>
            <Plus />
            Add someone
          </Button>
        )}

        {interactions.length > 0 && (
          <section className="space-y-3 border-t-2 pt-4 dark:border-t">
            <h3 className="text-sm font-semibold">What was said</h3>
            <InteractionList items={interactions} show="contact" onChanged={refresh} />
          </section>
        )}
      </CardContent>
    </Card>
  );
}

/** Search your contacts and link one, or add someone new if they aren't there. */
function AddPerson({ opportunityId, onDone, onLinked }: { opportunityId: string; onDone: () => void; onLinked: () => void }) {
  const [search, setSearch] = useState("");
  const [relation, setRelation] = useState<Relation>("recruiter");
  const [creating, setCreating] = useState(false);
  const term = useDebounced(search.trim());

  const matches = useQuery({
    queryKey: queryKeys.contactList({ q: term, pageSize: 6 }),
    queryFn: () => listContacts({ q: term, pageSize: 6 }),
    enabled: term.length > 0,
  });

  const link = useMutation({
    mutationFn: (contactId: string) => linkOpportunity(contactId, opportunityId, relation),
    onSuccess: () => {
      onLinked();
      onDone();
    },
    onError: (error) => toast.error(describeApiError(error).message ?? "Could not link them."),
  });

  return (
    <div className="space-y-3 rounded-lg border-2 p-3 dark:border">
      <div className="grid gap-3 sm:grid-cols-[1fr_11rem]">
        <Field id="person-search" label="Who">
          <Input id="person-search" autoComplete="off" autoFocus placeholder="Search your contacts" value={search} onChange={(e) => setSearch(e.target.value)} />
        </Field>
        <Field id="person-relation" label="Their part">
          <NativeSelect id="person-relation" value={relation} onChange={(e) => setRelation(e.target.value as Relation)}>
            {RELATIONS.map((r) => (
              <option key={r} value={r}>
                {RELATION_LABELS[r]}
              </option>
            ))}
          </NativeSelect>
        </Field>
      </div>

      {term.length > 0 && matches.data && (
        <ul aria-label="Matching contacts" className="space-y-1">
          {matches.data.items.map((contact) => (
            <li key={contact.id}>
              <button
                type="button"
                disabled={link.isPending}
                onClick={() => link.mutate(contact.id)}
                className="w-full rounded-md border px-3 py-2 text-left text-sm hover:bg-accent disabled:opacity-50"
              >
                <span className="font-medium">{contact.name}</span>
                {(contact.role || contact.company) && (
                  <span className="text-muted-foreground"> · {[contact.role, contact.company?.name].filter(Boolean).join(", ")}</span>
                )}
              </button>
            </li>
          ))}
          {matches.data.items.length === 0 && <li className="text-sm text-muted-foreground">Nobody by that name yet.</li>}
        </ul>
      )}

      <div className="flex flex-wrap gap-2">
        <Button variant="outline" size="sm" onClick={() => setCreating(true)}>
          <Plus />
          New contact
        </Button>
        <Button variant="ghost" size="sm" onClick={onDone}>
          Cancel
        </Button>
      </div>

      {creating && (
        <ContactDialog initialName={search.trim()} onClose={() => setCreating(false)} onSaved={(saved) => link.mutate(saved.id)} />
      )}
    </div>
  );
}
