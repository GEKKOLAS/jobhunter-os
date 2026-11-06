"use client";

import { BreakdownTable } from "@/components/analytics/breakdown-table";
import { FunnelBars } from "@/components/analytics/funnel-bars";
import { SkillBars } from "@/components/analytics/skill-bars";
import { StatTile } from "@/components/analytics/stat-tile";
import { WeeklyChart } from "@/components/analytics/weekly-chart";
import { PageHeader } from "@/components/app/page-header";
import { ToggleChip } from "@/components/profile/field";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { describeMedian, describeRate, RANGE_LABELS, SOURCE_LABELS, viewerTimeZone } from "@/lib/analytics";
import { getSkillsReport, getSummary } from "@/lib/api/analytics";
import { queryKeys } from "@/lib/query/query-keys";
import { RANGES, type RangeDays } from "@/lib/schemas/analytics";
import { cn } from "@/lib/utils";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { BarChart3 } from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";

export default function AnalyticsPage() {
  return (
    <Suspense fallback={null}>
      <AnalyticsView />
    </Suspense>
  );
}

const rangeOf = (value: string | null): RangeDays => (RANGES.find((r) => String(r) === value) ?? 90) as RangeDays;

/** F6: how the search is going, from your own records. The range lives in the URL. */
function AnalyticsView() {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const days = rangeOf(params.get("days"));
  const [tz] = useState(viewerTimeZone);

  const summary = useQuery({
    queryKey: queryKeys.analyticsSummary(days, tz),
    queryFn: () => getSummary(days, tz),
    placeholderData: keepPreviousData,
  });
  const skills = useQuery({
    queryKey: queryKeys.analyticsSkills(days),
    queryFn: () => getSkillsReport(days),
    placeholderData: keepPreviousData,
  });

  const setDays = (value: RangeDays) =>
    router.replace(value === 90 ? pathname : `${pathname}?days=${value}`, { scroll: false });

  const s = summary.data;
  const nothingYet = s && s.saved === 0 && s.applications === 0 && s.weekly.every((w) => w.responses === 0);
  const noRoles = s && s.byRole.every((r) => r.key === null);
  const noResponse = s && describeRate(s.rates.noResponse);

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6">
      <PageHeader
        icon={BarChart3}
        eyebrow={`Last ${RANGE_LABELS[days]}`}
        title="Analytics"
        description="How your search is going, from your own records. Small samples say so instead of showing a percentage."
      />

      <div className="flex flex-wrap gap-1.5" role="group" aria-label="Range">
        {RANGES.map((r) => (
          <ToggleChip key={r} pressed={r === days} onToggle={() => setDays(r)}>
            {RANGE_LABELS[r]}
          </ToggleChip>
        ))}
      </div>

      {summary.isError && <p className="text-sm text-destructive">Could not load your analytics. Reload to try again.</p>}

      {s && (
        <div className={cn("flex flex-col gap-6 transition-opacity", summary.isPlaceholderData && "opacity-60")}>
          {nothingYet && (
            <Card>
              <CardHeader>
                <CardTitle>Nothing to measure yet</CardTitle>
                <CardDescription>
                  These numbers fill in as you save opportunities and move them across the{" "}
                  <Link href="/board" className="text-link hover:underline">board</Link>. Nothing here is estimated: every number
                  counts your own records.
                </CardDescription>
              </CardHeader>
            </Card>
          )}

          <section aria-label="Headline numbers" className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
            <StatTile
              label="Applications sent"
              reading={{ headline: String(s.applications), detail: `${s.saved} saved in this period`, known: true }}
            />
            <StatTile label="Got an answer" reading={describeRate(s.rates.response)} />
            <StatTile label="Reached interviews" reading={describeRate(s.rates.interview)} />
            <StatTile label="Reached an offer" reading={describeRate(s.rates.offer)} />
            <StatTile label="Typical wait for an answer" reading={describeMedian(s.timeToFirstResponse)} hint="Median, answered ones only" />
          </section>

          <Card>
            <CardHeader>
              <CardTitle>How far your applications got</CardTitle>
              <CardDescription>
                The {s.applications} sent in this period. A rejection counts as an answer. Recent applications may still hear
                back.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <FunnelBars funnel={s.funnel} />
              {noResponse && s.rates.noResponse.denominator > 0 && (
                <p className="text-sm text-muted-foreground">
                  No answer after 30 days: {s.rates.noResponse.numerator} of the {s.rates.noResponse.denominator} sent at least
                  30 days ago{noResponse.known && ` (${noResponse.headline})`}.
                </p>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Week by week</CardTitle>
            </CardHeader>
            <CardContent>
              {s.weekly.some((w) => w.applications > 0 || w.responses > 0) ? (
                <WeeklyChart weeks={s.weekly} />
              ) : (
                <p className="text-sm text-muted-foreground">Nothing sent or answered in this period.</p>
              )}
            </CardContent>
          </Card>

          <div className="grid gap-6 lg:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle>By source</CardTitle>
                <CardDescription>Where the postings you applied to came from.</CardDescription>
              </CardHeader>
              <CardContent>
                {s.bySource.length > 0 ? (
                  <BreakdownTable rows={s.bySource} heading="Source" nameOf={(key) => SOURCE_LABELS[key ?? ""] ?? key ?? ""} />
                ) : (
                  <p className="text-sm text-muted-foreground">No applications in this period.</p>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>By target role</CardTitle>
                <CardDescription>Each application counts under the first of your target roles its title contains.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                {s.byRole.length > 0 && !(noRoles && s.applications === 0) && (
                  <BreakdownTable rows={s.byRole} heading="Role" nameOf={(key) => key ?? "Other"} />
                )}
                {noRoles && (
                  <p className="text-sm text-muted-foreground">
                    Add target roles to <Link href="/profile" className="text-link hover:underline">your profile</Link> to see
                    which kind of job answers.
                  </p>
                )}
                {!noRoles && s.applications === 0 && <p className="text-sm text-muted-foreground">No applications in this period.</p>}
              </CardContent>
            </Card>
          </div>
        </div>
      )}

      {skills.isError && <p className="text-sm text-destructive">Could not load the skills report. Reload to try again.</p>}
      {skills.data && (
        <div className={cn("grid gap-6 transition-opacity lg:grid-cols-2", skills.isPlaceholderData && "opacity-60")}>
          <Card>
            <CardHeader>
              <CardTitle>Skills postings ask for</CardTitle>
              <CardDescription>
                From the confirmed requirements of the {skills.data.postings}{" "}
                {skills.data.postings === 1 ? "posting" : "postings"} you saved in this period.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {skills.data.requested.length > 0 ? (
                <SkillBars skills={skills.data.requested} postings={skills.data.postings} />
              ) : (
                <p className="text-sm text-muted-foreground">
                  Confirm the requirements on an opportunity&apos;s page and the skills it asks for show up here.
                </p>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Most frequent gaps</CardTitle>
              <CardDescription>Asked for, and neither it nor a related skill is on your profile.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              {skills.data.gaps.length > 0 ? (
                <>
                  <SkillBars skills={skills.data.gaps} postings={skills.data.postings} />
                  <p className="text-xs text-muted-foreground">
                    If you do have one of these, <Link href="/profile" className="text-link hover:underline">add it to your profile</Link>{" "}
                    with the evidence for it.
                  </p>
                </>
              ) : (
                <p className="text-sm text-muted-foreground">
                  {skills.data.postings > 0 ? "No gaps in the postings you saved in this period." : "Nothing to compare yet."}
                </p>
              )}
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  );
}
