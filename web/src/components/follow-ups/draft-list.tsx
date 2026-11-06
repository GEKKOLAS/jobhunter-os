"use client";

import { DraftDialog } from "@/components/follow-ups/draft-dialog";
import { Badge } from "@/components/ui/badge";
import { DRAFT_KIND_LABELS, DRAFT_STATUS_LABELS } from "@/lib/follow-ups";
import type { Draft } from "@/lib/schemas/follow-ups";
import { useState } from "react";

/** Drafts waiting on you. Opening one is where it's edited, approved and sent. */
export function DraftList({ drafts }: { drafts: Draft[] }) {
  const [open, setOpen] = useState<Draft | null>(null);

  return (
    <>
      <ul className="divide-y">
        {drafts.map((d) => (
          <li key={d.id}>
            <button type="button" onClick={() => setOpen(d)} className="flex w-full flex-wrap items-center justify-between gap-x-3 gap-y-1 py-2 text-left hover:bg-accent/50">
              <span className="min-w-0 flex-1 basis-56">
                <span className="block truncate text-sm font-medium">{d.subject || <span className="text-muted-foreground italic">No subject yet</span>}</span>
                <span className="block truncate text-xs text-muted-foreground">
                  {[DRAFT_KIND_LABELS[d.kind], d.contactName && `to ${d.contactName}`, d.opportunityTitle].filter(Boolean).join(" · ")}
                </span>
              </span>
              <Badge variant={d.status === "approved" ? "success" : "outline"}>{DRAFT_STATUS_LABELS[d.status]}</Badge>
            </button>
          </li>
        ))}
      </ul>
      {open && <DraftDialog draft={open} onClose={() => setOpen(null)} />}
    </>
  );
}
