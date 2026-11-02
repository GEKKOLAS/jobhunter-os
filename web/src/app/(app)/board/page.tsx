"use client";

import { PageHeader } from "@/components/app/page-header";
import { OpportunityCard } from "@/components/opportunities/opportunity-card";
import { OpportunityDialog } from "@/components/opportunities/opportunity-dialog";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { getBoard } from "@/lib/api/opportunities";
import { useMoveOpportunity } from "@/lib/hooks/use-move-opportunity";
import { STAGE_LABELS } from "@/lib/opportunities";
import { queryKeys } from "@/lib/query/query-keys";
import { CLOSED_STAGES, type OpenStage, type OpportunityCard as CardData } from "@/lib/schemas/opportunities";
import { useQuery } from "@tanstack/react-query";
import { Columns3, Plus } from "lucide-react";
import Link from "next/link";
import { useState } from "react";

export default function BoardPage() {
  const board = useQuery({ queryKey: queryKeys.board, queryFn: getBoard });
  const move = useMoveOpportunity();
  const [dragging, setDragging] = useState<CardData | null>(null);
  const [over, setOver] = useState<OpenStage | null>(null);
  // undefined: closed; a stage: adding a new opportunity to that column.
  const [adding, setAdding] = useState<OpenStage | undefined>(undefined);

  const open = board.data?.columns.reduce((n, c) => n + c.items.length, 0) ?? 0;
  const closed = board.data ? Object.values(board.data.closedCounts).reduce((n, c) => n + c, 0) : 0;
  const canDrop = (stage: OpenStage) => dragging !== null && dragging.allowedMoves.includes(stage);

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        icon={Columns3}
        eyebrow={board.data ? `${open} open · ${closed} closed` : undefined}
        title="Board"
        description="Drag a card to move it, or use its Move menu. Only moves that make sense are offered."
        action={
          <div className="flex flex-wrap gap-2">
            <Button asChild variant="outline">
              <Link href="/sources">Import</Link>
            </Button>
            <Button onClick={() => setAdding("saved")}>
              <Plus />
              New opportunity
            </Button>
          </div>
        }
      />

      {board.isError && <p className="text-sm text-destructive">Could not load the board. Reload to try again.</p>}

      {board.data && open === 0 && closed === 0 && (
        <Card>
          <CardContent className="flex flex-col items-start gap-3 py-6">
            <p className="font-semibold">No opportunities yet.</p>
            <p className="text-sm text-muted-foreground">
              Add a job you&apos;re considering, or import one from a link, pasted text or a company&apos;s job board.
            </p>
            <div className="flex flex-wrap gap-2">
              <Button onClick={() => setAdding("saved")}>
                <Plus />
                Add your first opportunity
              </Button>
              <Button asChild variant="outline">
                <Link href="/sources">Import one</Link>
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {board.data && (open > 0 || closed > 0) && (
        <div className="-mx-4 grid snap-x auto-cols-[minmax(15.5rem,1fr)] grid-flow-col gap-3 overflow-x-auto px-4 pb-2 lg:mx-0 lg:px-0">
          {board.data.columns.map((column) => (
            <section
              key={column.stage}
              aria-label={`${STAGE_LABELS[column.stage]}, ${column.items.length}`}
              onDragOver={(e) => {
                if (!canDrop(column.stage)) return;
                e.preventDefault();
                e.dataTransfer.dropEffect = "move";
                setOver(column.stage);
              }}
              onDragLeave={() => setOver((s) => (s === column.stage ? null : s))}
              onDrop={(e) => {
                e.preventDefault();
                setOver(null);
                if (dragging && canDrop(column.stage)) move.mutate({ id: dragging.id, stage: column.stage });
                setDragging(null);
              }}
              className={`flex snap-start flex-col gap-2 rounded-xl p-2 transition-colors ${
                over === column.stage
                  ? "bg-primary/25 outline-2 outline-[#111111] outline-dashed dark:bg-primary/10 dark:outline-primary"
                  : dragging && !canDrop(column.stage) && dragging.stage !== column.stage
                    ? "opacity-45"
                    : "bg-muted/50"
              }`}
            >
              <header className="flex items-center justify-between px-1">
                <h2 className="font-mono text-[11px] font-bold tracking-[0.06em] uppercase dark:font-sans dark:text-xs dark:font-semibold dark:normal-case dark:tracking-normal">
                  {STAGE_LABELS[column.stage]} <span className="text-muted-foreground">{column.items.length}</span>
                </h2>
                <Button
                  variant="ghost"
                  size="icon-xs"
                  aria-label={`Add an opportunity to ${STAGE_LABELS[column.stage]}`}
                  onClick={() => setAdding(column.stage)}
                >
                  <Plus />
                </Button>
              </header>
              <ul className="flex min-h-24 flex-col gap-2">
                {column.items.map((card) => (
                  <OpportunityCard key={card.id} card={card} onDragStart={setDragging} onDragEnd={() => setDragging(null)} />
                ))}
                {column.items.length === 0 && (
                  <li className="rounded-lg border-2 border-dashed px-3 py-6 text-center text-xs text-muted-foreground dark:border">
                    Nothing here
                  </li>
                )}
              </ul>
            </section>
          ))}
        </div>
      )}

      {board.data && closed > 0 && (
        <nav aria-label="Closed opportunities" className="flex flex-wrap items-center gap-2 text-sm">
          <span className="text-muted-foreground">Closed:</span>
          {CLOSED_STAGES.filter((s) => (board.data.closedCounts[s] ?? 0) > 0).map((s) => (
            <Link
              key={s}
              href={`/opportunities?stage=${s}`}
              className="rounded-full border-2 px-3 py-0.5 hover:bg-accent dark:border"
            >
              {STAGE_LABELS[s]} {board.data.closedCounts[s]}
            </Link>
          ))}
        </nav>
      )}

      {adding !== undefined && <OpportunityDialog initialStage={adding} onClose={() => setAdding(undefined)} />}
    </div>
  );
}
