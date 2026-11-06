import type { SkillsReport } from "@/lib/schemas/analytics";
import { Check } from "lucide-react";

type Skill = SkillsReport["gaps"][number] & { inProfile?: boolean };

/** Skills ranked by how many postings name them: one hue, thin bars, the count in words beside each. */
export function SkillBars({ skills, postings }: { skills: Skill[]; postings: number }) {
  const max = Math.max(1, ...skills.map((s) => s.postings));

  return (
    <ol className="space-y-3">
      {skills.map((s) => (
        <li key={s.key} className="space-y-1">
          <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-0.5 text-sm">
            <span className="flex flex-wrap items-baseline gap-x-2">
              <span className="font-medium break-words">{s.name}</span>
              {s.inProfile === true && (
                <span className="inline-flex items-center gap-1 text-xs text-success">
                  <Check className="size-3" aria-hidden />
                  On your profile
                </span>
              )}
              {s.inProfile === false && <span className="text-xs text-muted-foreground">Not on your profile</span>}
            </span>
            <span className="text-xs text-muted-foreground tabular-nums">
              {s.postings} of {postings} {postings === 1 ? "posting" : "postings"}
              {s.must > 0 && ` · ${s.must} must`}
            </span>
          </div>
          <div className="h-2 rounded-r-[4px] bg-viz-sent" style={{ width: `${(s.postings / max) * 100}%` }} aria-hidden />
        </li>
      ))}
    </ol>
  );
}
