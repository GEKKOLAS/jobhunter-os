"use client";

import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getFit } from "@/lib/api/matching";
import {
  BAND_BADGES,
  BAND_LABELS,
  describeBasis,
  describeNote,
  describeOutcome,
  describeSuggestion,
  FIT_DISCLAIMER,
  groupOutcomes,
  MISSING_REASONS,
} from "@/lib/matching";
import { queryKeys } from "@/lib/query/query-keys";
import type { Fit, Outcome } from "@/lib/schemas/matching";
import type { Opportunity } from "@/lib/schemas/opportunities";
import { useQuery } from "@tanstack/react-query";
import { Check, CircleDashed, CircleDot, Minus } from "lucide-react";
import Link from "next/link";

const COVERAGE_ICONS = {
  covered: <Check aria-hidden className="mt-0.5 size-4 shrink-0 text-success" />,
  partial: <CircleDot aria-hidden className="mt-0.5 size-4 shrink-0 text-warning" />,
  gap: <Minus aria-hidden className="mt-0.5 size-4 shrink-0 text-destructive" />,
  not_scored: <CircleDashed aria-hidden className="mt-0.5 size-4 shrink-0 text-muted-foreground" />,
} satisfies Record<Outcome["coverage"], React.ReactNode>;

/** The estimate and its reasons. The number comes from code on the server, and everything behind it is shown. */
export function FitPanel({ opportunity }: { opportunity: Opportunity }) {
  const fit = useQuery({ queryKey: queryKeys.fit(opportunity.id), queryFn: () => getFit(opportunity.id) });

  return (
    <Card>
      <CardHeader>
        <CardTitle>Fit</CardTitle>
        <CardDescription>{FIT_DISCLAIMER}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        {fit.isError && <p className="text-sm text-destructive">Could not work out the fit. Reload to try again.</p>}
        {fit.isPending && <p className="text-sm text-muted-foreground">Working it out…</p>}
        {fit.data && <FitBody fit={fit.data} opportunity={opportunity} />}
        <HowItWorks />
      </CardContent>
    </Card>
  );
}

function FitBody({ fit, opportunity: o }: { fit: Fit; opportunity: Opportunity }) {
  return (
    <>
      {fit.score !== null && fit.band !== null ? (
        <div className="space-y-1.5">
          <div className="flex flex-wrap items-center gap-3">
            <span className="font-mono text-4xl leading-none font-bold tabular-nums">{fit.score}</span>
            <Badge variant={BAND_BADGES[fit.band]}>{BAND_LABELS[fit.band]}</Badge>
          </div>
          <p className="text-sm text-muted-foreground">{describeBasis(fit.outcomes)}</p>
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">
          {fit.missingReason && MISSING_REASONS[fit.missingReason]}
          {fit.missingReason === "no_skills" && (
            <>
              {" "}
              <Link href="/profile" className="text-link hover:underline">
                Add your skills
              </Link>
            </>
          )}
        </p>
      )}

      {fit.suggestedPriority && <p className="text-sm">{describeSuggestion(fit.suggestedPriority, o.priority, o.prioritySetBy)}</p>}

      {groupOutcomes(fit.outcomes).map((section) => (
        <section key={section.coverage} className="space-y-2">
          <h3 className="text-sm font-semibold">
            {section.title} <span className="font-normal text-muted-foreground">({section.items.length})</span>
          </h3>
          {/* The same reason for every row: said once, not once per row. */}
          {section.coverage === "not_scored" && <p className="text-xs text-muted-foreground">These aren&apos;t about one skill, so they don&apos;t change the score.</p>}
          <ul className="space-y-2">
            {section.items.map((outcome, index) => (
              <li key={`${outcome.text}-${index}`} className="flex gap-2">
                {COVERAGE_ICONS[outcome.coverage]}
                <div className="min-w-0">
                  <p className={`text-sm break-words ${outcome.coverage === "not_scored" ? "" : "font-medium"}`}>
                    {outcome.text}
                    {outcome.kind === "nice" && <span className="font-normal text-muted-foreground"> · nice to have</span>}
                  </p>
                  {outcome.coverage !== "not_scored" && <p className="text-sm text-muted-foreground">{describeOutcome(outcome)}</p>}
                </div>
              </li>
            ))}
          </ul>
        </section>
      ))}

      {fit.notes.length > 0 && (
        <section className="space-y-2">
          <h3 className="text-sm font-semibold">Worth checking</h3>
          <p className="text-xs text-muted-foreground">Not part of the score.</p>
          <ul className="list-disc space-y-1 pl-5 text-sm">
            {fit.notes.map((note) => (
              <li key={note.code}>{describeNote(note)}</li>
            ))}
          </ul>
        </section>
      )}
    </>
  );
}

function HowItWorks() {
  return (
    <details className="text-sm text-muted-foreground">
      <summary className="cursor-pointer font-medium text-foreground">How this estimate works</summary>
      <ul className="mt-2 list-disc space-y-1 pl-5">
        <li>Only requirements you confirmed count. A must-have weighs 3, a nice-to-have weighs 1.</li>
        <li>
          You earn the full weight with the skill at level 3 or above and any years it asks for; half if you have the skill but fall
          short, or only a closely related one; nothing otherwise.
        </li>
        <li>The score is what you earned out of the total, as a percentage. 75 and up is strong, 55 good, 35 partial, below that weak.</li>
        <li>Requirements that don&apos;t name a skill are listed but not scored. Work mode, location and seniority are notes, never points.</li>
        <li>
          A strong fit saved or being prepared and under 14 days old suggests high priority, and a weak one suggests low. A priority you
          chose yourself is never changed.
        </li>
      </ul>
    </details>
  );
}
