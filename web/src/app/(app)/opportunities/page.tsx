"use client";

import { PageHeader } from "@/components/app/page-header";
import { OpportunityDialog } from "@/components/opportunities/opportunity-dialog";
import { ToggleChip } from "@/components/profile/field";
import { FitChip } from "@/components/matching/fit-chip";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { NativeSelect } from "@/components/ui/native-select";
import { listOpportunities, type OpportunityFilters } from "@/lib/api/opportunities";
import { useDebounced } from "@/lib/hooks/use-debounced";
import { ageLabel, isClosed, PRIORITY_LABELS, STAGE_LABELS } from "@/lib/opportunities";
import { queryKeys } from "@/lib/query/query-keys";
import { PRIORITIES, STAGES } from "@/lib/schemas/opportunities";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { Briefcase, Plus } from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";

export default function OpportunitiesPage() {
  return (
    <Suspense fallback={null}>
      <OpportunitiesList />
    </Suspense>
  );
}

/** Every opportunity, filterable. The filters live in the URL so links (and back) keep them. */
function OpportunitiesList() {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [adding, setAdding] = useState(false);
  const [search, setSearch] = useState(params.get("q") ?? "");
  const q = useDebounced(search, 300);

  const filters: OpportunityFilters = {
    stage: params.get("stage") ?? "",
    priority: params.get("priority") ?? "",
    q,
    archived: params.get("archived") === "true",
    page: Number(params.get("page") ?? 1),
  };
  const list = useQuery({
    queryKey: queryKeys.opportunityList(filters),
    queryFn: () => listOpportunities(filters),
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
        icon={Briefcase}
        eyebrow={list.data ? `${total} ${filters.archived ? "archived" : "opportunities"}` : undefined}
        title="Opportunities"
        description="Everything you've captured, open or closed."
        action={
          <Button onClick={() => setAdding(true)}>
            <Plus />
            New opportunity
          </Button>
        }
      />

      <div className="flex flex-wrap items-center gap-2">
        <Input
          aria-label="Search by role or company"
          placeholder="Search role or company"
          className="w-full sm:w-64"
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            if (params.get("page")) setParam("page", null);
          }}
        />
        <NativeSelect aria-label="Stage" className="w-40" value={filters.stage} onChange={(e) => setParam("stage", e.target.value)}>
          <option value="">Any stage</option>
          <option value="open">Open</option>
          <option value="closed">Closed</option>
          {STAGES.map((s) => (
            <option key={s} value={s}>
              {STAGE_LABELS[s]}
            </option>
          ))}
        </NativeSelect>
        <NativeSelect aria-label="Priority" className="w-36" value={filters.priority} onChange={(e) => setParam("priority", e.target.value)}>
          <option value="">Any priority</option>
          {PRIORITIES.map((p) => (
            <option key={p} value={p}>
              {PRIORITY_LABELS[p]}
            </option>
          ))}
        </NativeSelect>
        <ToggleChip pressed={filters.archived ?? false} onToggle={() => setParam("archived", filters.archived ? null : "true")}>
          Archived
        </ToggleChip>
      </div>

      {list.isError && <p className="text-sm text-destructive">Could not load opportunities. Reload to try again.</p>}
      {list.data && list.data.items.length === 0 && (
        <p className="text-sm text-muted-foreground">
          {filters.stage || filters.priority || filters.q || filters.archived ? "Nothing matches these filters." : "No opportunities yet."}
        </p>
      )}

      {list.data && list.data.items.length > 0 && (
        <Card className="gap-0 py-0">
          <ul>
            {list.data.items.map((o) => (
              <li key={o.id} className="border-b-2 last:border-b-0 dark:border-b dark:border-border/60">
                <Link
                  href={`/opportunities/${o.id}`}
                  className="flex flex-wrap items-center gap-x-4 gap-y-1 px-4 py-3 transition-colors hover:bg-accent"
                >
                  <span className="min-w-0 flex-1 basis-56">
                    <span className="block truncate font-semibold">{o.title}</span>
                    <span className="block truncate text-sm text-muted-foreground">
                      {[o.companyName, o.location].filter(Boolean).join(" · ")}
                    </span>
                  </span>
                  <FitChip score={o.fitScore} band={o.fitBand} />
                  {o.priority !== "normal" && (
                    <Badge variant={o.priority === "high" ? "warning" : "outline"}>{PRIORITY_LABELS[o.priority]}</Badge>
                  )}
                  <Badge variant={isClosed(o.stage) ? "secondary" : "info"}>{STAGE_LABELS[o.stage]}</Badge>
                  <span className="hidden w-20 text-right font-mono text-xs text-muted-foreground sm:inline">
                    {ageLabel(o.updatedAt)}
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

      {adding && <OpportunityDialog onClose={() => setAdding(false)} onSaved={(o) => router.push(`/opportunities/${o.id}`)} />}
    </div>
  );
}
