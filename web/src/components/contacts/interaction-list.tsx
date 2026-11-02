"use client";

import { Button } from "@/components/ui/button";
import { deleteInteraction } from "@/lib/api/contacts";
import { describeApiError } from "@/lib/api/form-errors";
import { describeInteraction } from "@/lib/contacts";
import type { Interaction } from "@/lib/schemas/contacts";
import { useMutation } from "@tanstack/react-query";
import { ArrowDownLeft, ArrowUpRight, Trash2 } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { toast } from "sonner";

const when = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" });

/** The log, newest first. Summaries are plain text: what someone said can't inject markup. */
export function InteractionList({
  items,
  show,
  onChanged,
}: {
  items: Interaction[];
  /** What the surrounding page doesn't already say: which person, or which opportunity. */
  show: "contact" | "opportunity";
  onChanged: () => void;
}) {
  const [confirming, setConfirming] = useState<string | null>(null);

  const remove = useMutation({
    mutationFn: deleteInteraction,
    onSuccess: () => {
      setConfirming(null);
      toast.success("Removed from the log");
      onChanged();
    },
    onError: (error) => toast.error(describeApiError(error).message ?? "Could not remove it."),
  });

  return (
    <ol className="space-y-4">
      {items.map((item) => (
        <li key={item.id} className="flex gap-3">
          {item.direction === "inbound" ? (
            <ArrowDownLeft aria-hidden className="mt-0.5 size-4 shrink-0 text-info" />
          ) : (
            <ArrowUpRight aria-hidden className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
          )}
          <div className="min-w-0 flex-1">
            <p className="text-sm font-medium">
              {describeInteraction(item)}
              {show === "contact" && (
                <>
                  {" · "}
                  <Link href={`/contacts/${item.contactId}`} className="text-link hover:underline">
                    {item.contactName}
                  </Link>
                </>
              )}
              {show === "opportunity" && item.opportunityId && item.opportunityTitle && (
                <>
                  {" · "}
                  <Link href={`/opportunities/${item.opportunityId}`} className="text-link hover:underline">
                    {item.opportunityTitle}
                  </Link>
                </>
              )}
            </p>
            <p className="mt-0.5 text-sm break-words whitespace-pre-line">{item.summary}</p>
            <p className="mt-0.5 font-mono text-xs text-muted-foreground">{when.format(new Date(item.occurredAt))}</p>
            {confirming === item.id && (
              <div className="mt-2 flex flex-wrap items-center gap-2">
                <span className="text-sm">Remove this entry for good?</span>
                <Button variant="destructive" size="sm" disabled={remove.isPending} onClick={() => remove.mutate(item.id)}>
                  Remove
                </Button>
                <Button variant="ghost" size="sm" onClick={() => setConfirming(null)}>
                  Keep it
                </Button>
              </div>
            )}
          </div>
          {confirming !== item.id && (
            <Button variant="ghost" size="icon-sm" aria-label={`Remove the entry from ${when.format(new Date(item.occurredAt))}`} onClick={() => setConfirming(item.id)}>
              <Trash2 />
            </Button>
          )}
        </li>
      ))}
    </ol>
  );
}
