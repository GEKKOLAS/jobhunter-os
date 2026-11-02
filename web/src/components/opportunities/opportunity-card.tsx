"use client";

import { FitChip } from "@/components/matching/fit-chip";
import { StageSelect } from "@/components/opportunities/stage-select";
import { Badge } from "@/components/ui/badge";
import { ageLabel, PRIORITY_LABELS, WORK_MODE_LABELS } from "@/lib/opportunities";
import type { OpportunityCard as Card } from "@/lib/schemas/opportunities";
import Link from "next/link";

/** A board card. Draggable to any column its allowed moves include; the Move menu does the same by keyboard. */
export function OpportunityCard({
  card,
  onDragStart,
  onDragEnd,
}: {
  card: Card;
  onDragStart: (card: Card) => void;
  onDragEnd: () => void;
}) {
  const meta = [card.location, card.workMode && WORK_MODE_LABELS[card.workMode]].filter(Boolean).join(" · ");

  return (
    <li
      draggable
      onDragStart={(e) => {
        e.dataTransfer.setData("text/plain", card.id);
        e.dataTransfer.effectAllowed = "move";
        onDragStart(card);
      }}
      onDragEnd={onDragEnd}
      className="cursor-grab rounded-lg border-2 bg-card p-3 shadow-hard-sm active:cursor-grabbing dark:border dark:shadow-none"
    >
      <div className="flex items-start justify-between gap-2">
        <Link href={`/opportunities/${card.id}`} className="min-w-0 font-semibold leading-snug hover:underline">
          {card.title}
        </Link>
        {card.priority !== "normal" && (
          <Badge variant={card.priority === "high" ? "warning" : "outline"} className="shrink-0">
            {PRIORITY_LABELS[card.priority]}
          </Badge>
        )}
      </div>
      <p className="mt-0.5 truncate text-sm text-muted-foreground">{card.companyName}</p>
      {meta && <p className="truncate text-xs text-muted-foreground">{meta}</p>}
      <div className="mt-2.5 flex flex-wrap items-center justify-between gap-2">
        <span className="flex min-w-0 items-center gap-1.5">
          <FitChip score={card.fitScore} band={card.fitBand} />
          <span className="font-mono text-[11px] text-muted-foreground" title={card.appliedAt ? "Since you applied" : "Since you saved it"}>
            {ageLabel(card.appliedAt ?? card.createdAt)}
          </span>
        </span>
        <StageSelect id={card.id} title={card.title} stage={card.stage} allowedMoves={card.allowedMoves} className="w-32" />
      </div>
    </li>
  );
}
