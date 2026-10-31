"use client";

import { NativeSelect } from "@/components/ui/native-select";
import { useMoveOpportunity } from "@/lib/hooks/use-move-opportunity";
import { moveLabel } from "@/lib/opportunities";
import type { Stage } from "@/lib/schemas/opportunities";
import { cn } from "@/lib/utils";

/**
 * "Move…": the allowed moves (from the API) as a plain select. It is the keyboard and
 * screen-reader way to do what dragging does on the board.
 */
export function StageSelect({
  id,
  title,
  stage,
  previousStage,
  allowedMoves,
  className,
}: {
  id: string;
  title: string;
  stage: Stage;
  previousStage?: Stage | null;
  allowedMoves: Stage[];
  className?: string;
}) {
  const move = useMoveOpportunity();
  if (allowedMoves.length === 0) return null;

  return (
    <NativeSelect
      aria-label={`Move ${title}`}
      className={cn("h-8 text-xs", className)}
      value=""
      disabled={move.isPending}
      onChange={(e) => {
        if (e.target.value) move.mutate({ id, stage: e.target.value as Stage });
      }}
    >
      <option value="">Move…</option>
      {allowedMoves.map((to) => (
        <option key={to} value={to}>
          {moveLabel(stage, to, previousStage)}
        </option>
      ))}
    </NativeSelect>
  );
}
