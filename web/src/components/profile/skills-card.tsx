"use client";

import { SkillDialog } from "@/components/profile/skill-dialog";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { formatYears, groupSkills, LEVEL_LABELS } from "@/lib/profile";
import type { Skill } from "@/lib/schemas/profile";
import { Plus } from "lucide-react";
import { useState } from "react";

function LevelDots({ level }: { level: number }) {
  return (
    <span aria-hidden className="flex gap-0.5">
      {[1, 2, 3, 4, 5].map((n) => (
        <span key={n} aria-hidden className={`size-1.5 rounded-full ${n <= level ? "bg-foreground" : "bg-muted-foreground/30"}`} />
      ))}
    </span>
  );
}

/** Skills by category. Each says how much evidence backs it; none is a nudge, not an error. */
export function SkillsCard({ skills }: { skills: Skill[] }) {
  // undefined: closed; null: adding; a skill: editing it.
  const [editing, setEditing] = useState<Skill | null | undefined>(undefined);

  return (
    <Card>
      <CardHeader>
        <CardTitle>Skills</CardTitle>
        <CardDescription>What postings are compared against. {skills.length > 0 && `${skills.length} so far.`}</CardDescription>
        <CardAction>
          <Button size="sm" onClick={() => setEditing(null)}>
            <Plus />
            Add skill
          </Button>
        </CardAction>
      </CardHeader>
      <CardContent className="space-y-5">
        {skills.length === 0 && (
          <p className="text-sm text-muted-foreground">
            Add the skills you&apos;d put on your CV, with an honest level and years of use.
          </p>
        )}
        {groupSkills(skills).map((group) => (
          <section key={group.category} aria-label={group.label} className="space-y-2">
            <h3 className="font-mono text-[11px] font-bold tracking-[0.06em] text-muted-foreground uppercase dark:font-sans dark:font-medium">
              {group.label}
            </h3>
            <ul className="flex flex-wrap gap-2">
              {group.skills.map((skill) => (
                <li key={skill.id}>
                  <button
                    type="button"
                    onClick={() => setEditing(skill)}
                    aria-label={`${skill.name}: level ${skill.level} of 5 (${LEVEL_LABELS[skill.level]}), ${formatYears(skill.years)}, ${
                      skill.evidenceCount > 0 ? `${skill.evidenceCount} evidence` : "no evidence yet"
                    }. Edit`}
                    className="flex flex-col items-start gap-1 rounded-lg border-2 bg-card px-3 py-2 text-left shadow-hard-sm transition-colors hover:bg-accent dark:border dark:shadow-none"
                  >
                    <span className="flex items-center gap-2 text-sm font-semibold">
                      {skill.name}
                      <LevelDots level={skill.level} />
                    </span>
                    <span className="font-mono text-[11px] text-muted-foreground">
                      {formatYears(skill.years)} ·{" "}
                      {skill.evidenceCount > 0 ? (
                        `${skill.evidenceCount} evidence`
                      ) : (
                        <span className="text-warning">no evidence yet</span>
                      )}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          </section>
        ))}
      </CardContent>
      {editing !== undefined && <SkillDialog skill={editing} onClose={() => setEditing(undefined)} />}
    </Card>
  );
}
