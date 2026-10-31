"use client";

import { describeApiError } from "@/lib/api/form-errors";
import { moveOpportunity } from "@/lib/api/opportunities";
import { STAGE_LABELS } from "@/lib/opportunities";
import { queryKeys } from "@/lib/query/query-keys";
import type { Stage } from "@/lib/schemas/opportunities";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

/** Moves an opportunity along the pipeline and refreshes the board and every list. */
export function useMoveOpportunity() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, stage, note }: { id: string; stage: Stage; note?: string }) => moveOpportunity(id, stage, note),
    onSuccess: (moved) => {
      queryClient.setQueryData(queryKeys.opportunity(moved.id), moved);
      toast.success(`${moved.title}: ${STAGE_LABELS[moved.stage]}`);
    },
    onError: (error) => toast.error(describeApiError(error).message ?? "Could not move it."),
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.board });
      void queryClient.invalidateQueries({ queryKey: queryKeys.opportunities });
    },
  });
}
