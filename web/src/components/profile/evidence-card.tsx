"use client";

import { EvidenceDialog, KIND_LABELS } from "@/components/profile/evidence-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { formatPeriod } from "@/lib/profile";
import type { Evidence, Skill } from "@/lib/schemas/profile";
import { ExternalLink, Plus } from "lucide-react";
import { useState } from "react";

/** Projects, roles, certifications and degrees, each with the skills it demonstrates. */
export function EvidenceCard({ items, skills }: { items: Evidence[]; skills: Skill[] }) {
  // undefined: closed; null: adding; an item: editing it.
  const [editing, setEditing] = useState<Evidence | null | undefined>(undefined);
  const skillName = new Map(skills.map((s) => [s.id, s.name]));

  return (
    <Card>
      <CardHeader>
        <CardTitle>Evidence</CardTitle>
        <CardDescription>What backs up your skills. Drafts may cite it; they never invent beyond it.</CardDescription>
        <CardAction>
          <Button size="sm" onClick={() => setEditing(null)}>
            <Plus />
            Add evidence
          </Button>
        </CardAction>
      </CardHeader>
      <CardContent>
        {items.length === 0 && (
          <p className="text-sm text-muted-foreground">
            Add a project or role you&apos;re proud of and link the skills it shows.
          </p>
        )}
        <ul className="space-y-3">
          {items.map((item) => {
            const period = formatPeriod(item.startDate, item.endDate);
            return (
              <li key={item.id} className="rounded-xl border-2 p-4 dark:border">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="flex flex-wrap items-center gap-2 font-semibold">
                      {item.title}
                      <Badge variant="outline">{KIND_LABELS[item.kind]}</Badge>
                    </p>
                    <p className="text-sm text-muted-foreground">
                      {[item.organization, period].filter(Boolean).join(" · ")}
                    </p>
                  </div>
                  <Button variant="outline" size="sm" onClick={() => setEditing(item)}>
                    Edit
                  </Button>
                </div>
                {item.description && <p className="mt-2 line-clamp-3 text-sm whitespace-pre-line">{item.description}</p>}
                {item.outcome && (
                  <p className="mt-2 text-sm">
                    <span className="font-semibold">Outcome: </span>
                    {item.outcome}
                  </p>
                )}
                {(item.skillIds.length > 0 || item.links.length > 0) && (
                  <div className="mt-3 flex flex-wrap items-center gap-1.5">
                    {item.skillIds.map((id) => (
                      <Badge key={id} variant="secondary">
                        {skillName.get(id) ?? "Removed skill"}
                      </Badge>
                    ))}
                    {item.links.map((link) => (
                      <a
                        key={link.url}
                        href={link.url}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="inline-flex items-center gap-1 text-sm text-link underline-offset-4 hover:underline"
                      >
                        {link.label}
                        <ExternalLink aria-hidden className="size-3" />
                      </a>
                    ))}
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      </CardContent>
      {editing !== undefined && <EvidenceDialog item={editing} skills={skills} onClose={() => setEditing(undefined)} />}
    </Card>
  );
}
