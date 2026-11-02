"use client";

import { PageHeader } from "@/components/app/page-header";
import { ContactDialog } from "@/components/contacts/contact-dialog";
import { ToggleChip } from "@/components/profile/field";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { type ContactFilters, listContacts } from "@/lib/api/contacts";
import { lastTalked, roleLine } from "@/lib/contacts";
import { useDebounced } from "@/lib/hooks/use-debounced";
import { queryKeys } from "@/lib/query/query-keys";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { Plus, Users } from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";

export default function ContactsPage() {
  return (
    <Suspense fallback={null}>
      <ContactsList />
    </Suspense>
  );
}

/** Everyone in your search, alphabetically. The filters live in the URL so links (and back) keep them. */
function ContactsList() {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [adding, setAdding] = useState(false);
  const [search, setSearch] = useState(params.get("q") ?? "");
  const q = useDebounced(search, 300);

  const filters: ContactFilters = {
    q,
    archived: params.get("archived") === "true",
    page: Number(params.get("page") ?? 1),
  };
  const list = useQuery({
    queryKey: queryKeys.contactList(filters),
    queryFn: () => listContacts(filters),
    placeholderData: keepPreviousData,
  });

  const setParam = (key: string, value: string | null) => {
    const next = new URLSearchParams(params);
    if (value) next.set(key, value);
    else next.delete(key);
    if (key !== "page") next.delete("page");
    router.replace(`${pathname}${next.size ? `?${next}` : ""}`, { scroll: false });
  };

  const total = list.data?.totalCount ?? 0;
  const pages = Math.max(1, Math.ceil(total / (list.data?.pageSize ?? 25)));
  const page = list.data?.page ?? 1;

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6">
      <PageHeader
        icon={Users}
        eyebrow={list.data ? `${total} ${filters.archived ? "archived" : total === 1 ? "contact" : "contacts"}` : undefined}
        title="Contacts"
        description="The people in your search, and what you've said to each other."
        action={
          <Button onClick={() => setAdding(true)}>
            <Plus />
            New contact
          </Button>
        }
      />

      <div className="flex flex-wrap items-center gap-2">
        <Input
          aria-label="Search by name, role, email or company"
          placeholder="Search name, role, email or company"
          className="w-full sm:w-80"
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            if (params.get("page")) setParam("page", null);
          }}
        />
        <ToggleChip pressed={filters.archived ?? false} onToggle={() => setParam("archived", filters.archived ? null : "true")}>
          Archived
        </ToggleChip>
      </div>

      {list.isError && <p className="text-sm text-destructive">Could not load your contacts. Reload to try again.</p>}
      {list.data && list.data.items.length === 0 && (
        <p className="text-sm text-muted-foreground">
          {filters.q || filters.archived ? "Nobody matches this." : "No contacts yet. Add the recruiter you're talking to."}
        </p>
      )}

      {list.data && list.data.items.length > 0 && (
        <Card className="gap-0 py-0">
          <ul>
            {list.data.items.map((c) => (
              <li key={c.id} className="border-b-2 last:border-b-0 dark:border-b dark:border-border/60">
                <Link href={`/contacts/${c.id}`} className="flex flex-wrap items-center gap-x-4 gap-y-1 px-4 py-3 transition-colors hover:bg-accent">
                  <span className="min-w-0 flex-1 basis-56">
                    <span className="block truncate font-semibold">{c.name}</span>
                    {roleLine(c) && <span className="block truncate text-sm text-muted-foreground">{roleLine(c)}</span>}
                  </span>
                  <span className="font-mono text-xs text-muted-foreground">
                    {c.lastInteractionAt ? `talked ${lastTalked(c.lastInteractionAt)}` : "not talked yet"}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {pages > 1 && (
        <nav aria-label="Pages" className="flex items-center justify-between text-sm">
          <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setParam("page", String(page - 1))}>
            Previous
          </Button>
          <span className="text-muted-foreground">
            Page {page} of {pages}
          </span>
          <Button variant="outline" size="sm" disabled={page >= pages} onClick={() => setParam("page", String(page + 1))}>
            Next
          </Button>
        </nav>
      )}

      {adding && <ContactDialog onClose={() => setAdding(false)} onSaved={(saved) => router.push(`/contacts/${saved.id}`)} />}
    </div>
  );
}
