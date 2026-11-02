"use client";

import { FitPanel } from "@/components/matching/fit-panel";
import { RequirementsCard } from "@/components/matching/requirements-card";
import { OpportunityDialog } from "@/components/opportunities/opportunity-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { NativeSelect } from "@/components/ui/native-select";
import { ApiError } from "@/lib/api/client";
import { archiveOpportunity, getHistory, getOpportunity, restoreOpportunity } from "@/lib/api/opportunities";
import { useMoveOpportunity } from "@/lib/hooks/use-move-opportunity";
import {
  EMPLOYMENT_LABELS,
  formatSalary,
  isClosed,
  moveLabel,
  PRIORITY_LABELS,
  STAGE_LABELS,
  WORK_MODE_LABELS,
} from "@/lib/opportunities";
import { queryKeys } from "@/lib/query/query-keys";
import type { Opportunity, Stage } from "@/lib/schemas/opportunities";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, ExternalLink } from "lucide-react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";

const day = new Intl.DateTimeFormat(undefined, { dateStyle: "medium" });
const when = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" });

export default function OpportunityPage() {
  const { id } = useParams<{ id: string }>();
  const opportunity = useQuery({ queryKey: queryKeys.opportunity(id), queryFn: () => getOpportunity(id) });

  if (opportunity.error instanceof ApiError && opportunity.error.status === 404) {
    return (
      <div className="mx-auto max-w-3xl space-y-3">
        <p className="font-semibold">This opportunity doesn&apos;t exist, or it isn&apos;t yours.</p>
        <Link href="/opportunities" className="text-sm text-link hover:underline">
          Back to opportunities
        </Link>
      </div>
    );
  }
  if (opportunity.isError) return <p className="text-sm text-destructive">Could not load it. Reload to try again.</p>;
  if (!opportunity.data) return <p className="text-sm text-muted-foreground">Loading…</p>;

  return <Details opportunity={opportunity.data} />;
}

function Details({ opportunity: o }: { opportunity: Opportunity }) {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const history = useQuery({ queryKey: queryKeys.history(o.id), queryFn: () => getHistory(o.id) });

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.opportunities });
    void queryClient.invalidateQueries({ queryKey: queryKeys.board });
  };
  const archive = useMutation({
    mutationFn: () => archiveOpportunity(o.id),
    onSuccess: () => {
      toast.success("Archived. It's off the board; restore it any time.");
      refresh();
    },
  });
  const restore = useMutation({
    mutationFn: () => restoreOpportunity(o.id),
    onSuccess: () => {
      toast.success("Restored");
      refresh();
    },
  });

  const facts: [string, string | null][] = [
    ["Location", o.location],
    ["Work mode", o.workMode && WORK_MODE_LABELS[o.workMode]],
    ["Type", o.employmentType && EMPLOYMENT_LABELS[o.employmentType]],
    ["Salary", formatSalary(o)],
    ["Added", day.format(new Date(o.createdAt))],
    ["Applied", o.appliedAt && day.format(new Date(o.appliedAt))],
    ["First response", o.firstResponseAt && day.format(new Date(o.firstResponseAt))],
    ["Closed", o.closedAt && day.format(new Date(o.closedAt))],
  ];

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <Link href="/board" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="size-4" />
        Board
      </Link>

      <header className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant={isClosed(o.stage) ? "secondary" : "info"}>{STAGE_LABELS[o.stage]}</Badge>
          {o.priority !== "normal" && (
            <Badge variant={o.priority === "high" ? "warning" : "outline"}>{PRIORITY_LABELS[o.priority]} priority</Badge>
          )}
          {o.archivedAt && <Badge variant="outline">Archived</Badge>}
        </div>
        <h1 className="font-heading text-3xl leading-tight font-bold tracking-[-0.03em] dark:text-2xl dark:font-semibold">{o.title}</h1>
        <p className="text-muted-foreground">
          {o.company.name}
          {o.sourceUrl && (
            <>
              {" · "}
              <a href={o.sourceUrl} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 text-link hover:underline">
                Posting
                <ExternalLink aria-hidden className="size-3" />
              </a>
            </>
          )}
        </p>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" size="sm" onClick={() => setEditing(true)}>
            Edit
          </Button>
          {o.archivedAt ? (
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

      {!o.archivedAt && <MoveForm opportunity={o} />}

      <Card>
        <CardContent>
          <dl className="grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-4">
            {facts
              .filter(([, value]) => value)
              .map(([label, value]) => (
                <div key={label}>
                  <dt className="font-mono text-[11px] font-bold tracking-[0.06em] text-muted-foreground uppercase dark:font-sans dark:font-medium dark:normal-case dark:tracking-normal">
                    {label}
                  </dt>
                  <dd className="text-sm">{value}</dd>
                </div>
              ))}
          </dl>
        </CardContent>
      </Card>

      <FitPanel opportunity={o} />
      <RequirementsCard opportunity={o} />

      {o.description && (
        <Card>
          <CardHeader>
            <CardTitle>Posting</CardTitle>
          </CardHeader>
          <CardContent>
            {/* Plain text by design (spec 02 §5): a posting can't inject markup. */}
            <p className="max-h-[28rem] overflow-y-auto text-sm leading-relaxed whitespace-pre-line">{o.description}</p>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle>History</CardTitle>
        </CardHeader>
        <CardContent>
          <ol className="space-y-3">
            {history.data?.map((step) => (
              <li key={step.id} className="flex gap-3">
                <span aria-hidden className={`mt-1.5 size-2.5 shrink-0 rounded-full ${isClosed(step.toStage) ? "bg-muted-foreground" : "bg-primary"}`} />
                <div className="min-w-0">
                  <p className="text-sm font-medium">
                    {step.fromStage ? `${STAGE_LABELS[step.fromStage]} → ${STAGE_LABELS[step.toStage]}` : `Added at ${STAGE_LABELS[step.toStage]}`}
                  </p>
                  {step.note && <p className="text-sm text-muted-foreground">{step.note}</p>}
                  <p className="font-mono text-xs text-muted-foreground">{when.format(new Date(step.occurredAt))}</p>
                </div>
              </li>
            ))}
          </ol>
        </CardContent>
      </Card>

      {editing && <OpportunityDialog opportunity={o} onClose={() => setEditing(false)} />}
    </div>
  );
}

/** Move with an optional note ("Recruiter called", "Rejected by email"), kept in the history. */
function MoveForm({ opportunity: o }: { opportunity: Opportunity }) {
  const move = useMoveOpportunity();
  const [to, setTo] = useState<Stage | "">("");
  const [note, setNote] = useState("");

  if (o.allowedMoves.length === 0) return null;

  return (
    <form
      className="flex flex-wrap items-center gap-2 rounded-xl border-2 p-3 dark:border"
      onSubmit={(e) => {
        e.preventDefault();
        if (!to) return;
        move.mutate(
          { id: o.id, stage: to, note },
          {
            onSuccess: () => {
              setTo("");
              setNote("");
            },
          },
        );
      }}
    >
      <NativeSelect aria-label="Move to" className="w-56" value={to} onChange={(e) => setTo(e.target.value as Stage | "")}>
        <option value="">Move…</option>
        {o.allowedMoves.map((stage) => (
          <option key={stage} value={stage}>
            {moveLabel(o.stage, stage, o.previousStage)}
          </option>
        ))}
      </NativeSelect>
      <Input
        aria-label="Note (optional)"
        placeholder="Note (optional)"
        maxLength={500}
        className="min-w-40 flex-1"
        value={note}
        onChange={(e) => setNote(e.target.value)}
      />
      <Button type="submit" size="sm" disabled={!to || move.isPending}>
        Move
      </Button>
    </form>
  );
}
