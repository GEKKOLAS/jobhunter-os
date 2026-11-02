"use client";

import { ContactDialog } from "@/components/contacts/contact-dialog";
import { InteractionForm } from "@/components/contacts/interaction-form";
import { InteractionList } from "@/components/contacts/interaction-list";
import { Field } from "@/components/profile/field";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { NativeSelect } from "@/components/ui/native-select";
import { ApiError } from "@/lib/api/client";
import { archiveContact, getContact, linkOpportunity, listInteractions, restoreContact, unlinkOpportunity } from "@/lib/api/contacts";
import { describeApiError } from "@/lib/api/form-errors";
import { listOpportunities } from "@/lib/api/opportunities";
import { reachLinks, RELATION_LABELS, roleLine } from "@/lib/contacts";
import { useDebounced } from "@/lib/hooks/use-debounced";
import { STAGE_LABELS } from "@/lib/opportunities";
import { queryKeys } from "@/lib/query/query-keys";
import { type Contact, RELATIONS, type Relation } from "@/lib/schemas/contacts";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, ExternalLink, Plus, X } from "lucide-react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";

export default function ContactPage() {
  const { id } = useParams<{ id: string }>();
  const contact = useQuery({ queryKey: queryKeys.contact(id), queryFn: () => getContact(id) });

  if (contact.error instanceof ApiError && contact.error.status === 404) {
    return (
      <div className="mx-auto max-w-3xl space-y-3">
        <p className="font-semibold">This contact doesn&apos;t exist, or isn&apos;t yours.</p>
        <Link href="/contacts" className="text-sm text-link hover:underline">
          Back to contacts
        </Link>
      </div>
    );
  }
  if (contact.isError) return <p className="text-sm text-destructive">Could not load them. Reload to try again.</p>;
  if (!contact.data) return <p className="text-sm text-muted-foreground">Loading…</p>;

  return <Details contact={contact.data} />;
}

function Details({ contact: c }: { contact: Contact }) {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const interactions = useQuery({ queryKey: queryKeys.interactions(c.id), queryFn: () => listInteractions(c.id) });

  const refresh = () => void queryClient.invalidateQueries({ queryKey: queryKeys.contacts });

  const archive = useMutation({
    mutationFn: () => archiveContact(c.id),
    onSuccess: () => {
      toast.success("Archived. Restore them any time.");
      refresh();
    },
  });
  const restore = useMutation({
    mutationFn: () => restoreContact(c.id),
    onSuccess: () => {
      toast.success("Restored");
      refresh();
    },
  });

  const archived = c.archivedAt !== null;
  const links = reachLinks(c);

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <Link href="/contacts" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="size-4" />
        Contacts
      </Link>

      <header className="flex flex-col gap-3">
        {archived && (
          <div>
            <Badge variant="outline">Archived</Badge>
          </div>
        )}
        <h1 className="font-heading text-3xl leading-tight font-bold tracking-[-0.03em] break-words dark:text-2xl dark:font-semibold">{c.name}</h1>
        {roleLine(c) && <p className="text-muted-foreground">{roleLine(c)}</p>}
        {links.length > 0 && (
          <ul className="flex flex-wrap gap-x-4 gap-y-1 text-sm">
            {links.map((link) => (
              <li key={link.href} className="min-w-0 break-all">
                <a
                  href={link.href}
                  {...(link.external ? { target: "_blank", rel: "noopener noreferrer" } : {})}
                  className="inline-flex items-center gap-1 text-link hover:underline"
                >
                  {link.label}
                  {link.external && <ExternalLink aria-hidden className="size-3" />}
                </a>
              </li>
            ))}
          </ul>
        )}
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" size="sm" onClick={() => setEditing(true)}>
            Edit
          </Button>
          {archived ? (
            <Button variant="outline" size="sm" disabled={restore.isPending} onClick={() => restore.mutate()}>
              Restore
            </Button>
          ) : (
            <Button variant="ghost" size="sm" disabled={archive.isPending} onClick={() => archive.mutate()}>
              Archive
            </Button>
          )}
        </div>
      </header>

      {c.notes && (
        <Card>
          <CardHeader>
            <CardTitle>Notes</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm leading-relaxed break-words whitespace-pre-line">{c.notes}</p>
          </CardContent>
        </Card>
      )}

      <OpportunitiesCard contact={c} />

      <Card>
        <CardHeader>
          <CardTitle>Conversations</CardTitle>
          <CardDescription>What you&apos;ve said to each other, newest first.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-5">
          {archived ? (
            <p className="text-sm text-muted-foreground">Restore them to log a new conversation.</p>
          ) : (
            <InteractionForm
              contactId={c.id}
              opportunities={c.opportunities.map((o) => ({ id: o.opportunityId, title: o.title }))}
              onLogged={refresh}
            />
          )}
          {interactions.isError && <p className="text-sm text-destructive">Could not load the log. Reload to try again.</p>}
          {interactions.data && interactions.data.length === 0 && <p className="text-sm text-muted-foreground">Nothing logged yet.</p>}
          {interactions.data && interactions.data.length > 0 && (
            <div className="border-t-2 pt-4 dark:border-t">
              <InteractionList items={interactions.data} show="opportunity" onChanged={refresh} />
            </div>
          )}
        </CardContent>
      </Card>

      {editing && <ContactDialog contact={c} onClose={() => setEditing(false)} />}
    </div>
  );
}

function OpportunitiesCard({ contact: c }: { contact: Contact }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.contacts });
    void queryClient.invalidateQueries({ queryKey: queryKeys.opportunities });
  };

  const unlink = useMutation({
    mutationFn: (opportunityId: string) => unlinkOpportunity(c.id, opportunityId),
    onSuccess: refresh,
    onError: (error) => toast.error(describeApiError(error).message ?? "Could not remove it."),
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>Opportunities</CardTitle>
        <CardDescription>The jobs they&apos;re part of.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {c.opportunities.length === 0 && !adding && <p className="text-sm text-muted-foreground">Not linked to anything yet.</p>}

        {c.opportunities.length > 0 && (
          <ul className="divide-y">
            {c.opportunities.map((o) => (
              <li key={o.opportunityId} className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 py-2">
                <div className="min-w-0 flex-1 basis-48">
                  <Link href={`/opportunities/${o.opportunityId}`} className="block truncate text-sm font-medium hover:underline">
                    {o.title}
                  </Link>
                  <p className="truncate text-xs text-muted-foreground">
                    {[o.companyName, RELATION_LABELS[o.relation], o.archived ? "Archived" : STAGE_LABELS[o.stage]].join(" · ")}
                  </p>
                </div>
                <Button
                  variant="ghost"
                  size="icon-sm"
                  aria-label={`Remove ${o.title}`}
                  disabled={unlink.isPending}
                  onClick={() => unlink.mutate(o.opportunityId)}
                >
                  <X />
                </Button>
              </li>
            ))}
          </ul>
        )}

        {c.archivedAt === null &&
          (adding ? (
            <AddOpportunity contactId={c.id} onDone={() => setAdding(false)} onLinked={refresh} />
          ) : (
            <Button variant="outline" size="sm" onClick={() => setAdding(true)}>
              <Plus />
              Link an opportunity
            </Button>
          ))}
      </CardContent>
    </Card>
  );
}

function AddOpportunity({ contactId, onDone, onLinked }: { contactId: string; onDone: () => void; onLinked: () => void }) {
  const [search, setSearch] = useState("");
  const [relation, setRelation] = useState<Relation>("recruiter");
  const term = useDebounced(search.trim());

  const matches = useQuery({
    queryKey: queryKeys.opportunityList({ q: term, picker: true }),
    queryFn: () => listOpportunities({ q: term }),
    enabled: term.length > 0,
  });

  const link = useMutation({
    mutationFn: (opportunityId: string) => linkOpportunity(contactId, opportunityId, relation),
    onSuccess: () => {
      onLinked();
      onDone();
    },
    onError: (error) => toast.error(describeApiError(error).message ?? "Could not link it."),
  });

  return (
    <div className="space-y-3 rounded-lg border-2 p-3 dark:border">
      <div className="grid gap-3 sm:grid-cols-[1fr_11rem]">
        <Field id="opportunity-search" label="Which job">
          <Input id="opportunity-search" autoComplete="off" autoFocus placeholder="Search role or company" value={search} onChange={(e) => setSearch(e.target.value)} />
        </Field>
        <Field id="opportunity-relation" label="Their part">
          <NativeSelect id="opportunity-relation" value={relation} onChange={(e) => setRelation(e.target.value as Relation)}>
            {RELATIONS.map((r) => (
              <option key={r} value={r}>
                {RELATION_LABELS[r]}
              </option>
            ))}
          </NativeSelect>
        </Field>
      </div>

      {term.length > 0 && matches.data && (
        <ul aria-label="Matching opportunities" className="space-y-1">
          {matches.data.items.slice(0, 6).map((o) => (
            <li key={o.id}>
              <button
                type="button"
                disabled={link.isPending}
                onClick={() => link.mutate(o.id)}
                className="w-full rounded-md border px-3 py-2 text-left text-sm hover:bg-accent disabled:opacity-50"
              >
                <span className="font-medium">{o.title}</span>
                <span className="text-muted-foreground"> · {o.companyName}</span>
              </button>
            </li>
          ))}
          {matches.data.items.length === 0 && <li className="text-sm text-muted-foreground">Nothing matches that.</li>}
        </ul>
      )}

      <Button variant="ghost" size="sm" onClick={onDone}>
        Cancel
      </Button>
    </div>
  );
}
