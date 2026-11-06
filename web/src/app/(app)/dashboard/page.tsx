"use client";

import { PageHeader } from "@/components/app/page-header";
import { ReminderDialog } from "@/components/follow-ups/reminder-dialog";
import { ReminderList } from "@/components/follow-ups/reminder-list";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { days, endOfToday } from "@/lib/analytics";
import { getDashboard } from "@/lib/api/analytics";
import { listReminders } from "@/lib/api/follow-ups";
import { BAND_BADGES, BAND_LABELS } from "@/lib/matching";
import { MOBILE_TAB_SLUGS, MODULES } from "@/lib/modules";
import { STAGE_LABELS } from "@/lib/opportunities";
import { queryKeys } from "@/lib/query/query-keys";
import { CLOSED_STAGES, OPEN_STAGES } from "@/lib/schemas/opportunities";
import { useQuery } from "@tanstack/react-query";
import { ArrowRight, BellPlus, LayoutDashboard } from "lucide-react";
import Link from "next/link";
import { useState } from "react";

const today = new Intl.DateTimeFormat(undefined, { weekday: "long", month: "long", day: "numeric" });

/** F7: what's due today, what has gone quiet, and what to work on next (spec 03, Analytics). */
export default function DashboardPage() {
  const [until] = useState(() => endOfToday().toISOString());
  const [remindFor, setRemindFor] = useState<{ id: string; title: string } | null>(null);

  const dashboard = useQuery({ queryKey: queryKeys.dashboard, queryFn: getDashboard });
  const due = useQuery({
    queryKey: queryKeys.reminderList({ status: "open", until }),
    queryFn: () => listReminders({ status: "open", until }),
  });

  const d = dashboard.data;
  const total = d ? Object.values(d.stages).reduce((sum, n) => sum + n, 0) : 0;
  const closed = d ? CLOSED_STAGES.filter((s) => d.stages[s] > 0) : [];

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-6">
      <PageHeader
        icon={LayoutDashboard}
        eyebrow={today.format(new Date())}
        title="Dashboard"
        description="What's due today, what has gone quiet, and what to work on next."
      />

      {dashboard.isError && <p className="text-sm text-destructive">Could not load your dashboard. Reload to try again.</p>}

      {d && total === 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Start here</CardTitle>
            <CardDescription>Nothing in your pipeline yet. Three steps get it going:</CardDescription>
          </CardHeader>
          <CardContent>
            <ol className="list-decimal space-y-1.5 pl-5 text-sm">
              <li>
                Add your skills and the evidence for them on <Link href="/profile" className="text-link hover:underline">your profile</Link>, so fits mean something.
              </li>
              <li>
                Save a posting from a link, pasted text or a company&apos;s job board in{" "}
                <Link href="/sources" className="text-link hover:underline">Sources</Link>, or add one on the{" "}
                <Link href="/board" className="text-link hover:underline">board</Link>.
              </li>
              <li>Move it to Applied when you apply. A follow-up reminder appears a week later.</li>
            </ol>
          </CardContent>
        </Card>
      )}

      {d && total > 0 && (
        <section aria-labelledby="pipeline" className="space-y-2">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <h2 id="pipeline" className="font-heading text-xl font-bold tracking-[-0.01em] dark:text-base dark:font-semibold">
              Pipeline
            </h2>
            <Link href="/board" className="text-sm text-link hover:underline">
              Open the board
            </Link>
          </div>
          <ul className="grid grid-cols-3 gap-2 sm:grid-cols-6">
            {OPEN_STAGES.map((stage) => (
              <li key={stage}>
                <Card className="gap-0 py-3">
                  <CardContent className="px-3">
                    <p className="text-xs text-muted-foreground">{STAGE_LABELS[stage]}</p>
                    <p className="font-heading text-2xl font-semibold">{d.stages[stage]}</p>
                  </CardContent>
                </Card>
              </li>
            ))}
          </ul>
          {closed.length > 0 && (
            <p className="text-sm text-muted-foreground">
              Closed: {closed.map((s) => `${d.stages[s]} ${STAGE_LABELS[s].toLowerCase()}`).join(", ")}.
            </p>
          )}
        </section>
      )}

      <div className="grid gap-6 lg:grid-cols-2">
        <div className="flex flex-col gap-6">
          <Card>
            <CardHeader>
              <CardTitle>Due today</CardTitle>
              <CardDescription>Overdue and today&apos;s reminders.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              {due.isError && <p className="text-sm text-destructive">Could not load your reminders.</p>}
              {due.data && due.data.length === 0 && <p className="text-sm text-muted-foreground">Nothing due today.</p>}
              {due.data && due.data.length > 0 && <ReminderList reminders={due.data} />}
              <Link href="/follow-ups" className="inline-flex items-center gap-1 text-sm text-link hover:underline">
                All follow-ups <ArrowRight className="size-3.5" />
              </Link>
            </CardContent>
          </Card>
        </div>

        {/* What to chase, then what to start: the two lists that act on opportunities. */}
        <div className="flex flex-col gap-6">
          {d && (
            <Card>
              <CardHeader>
                <CardTitle>Gone quiet</CardTitle>
                <CardDescription>Applied 10 or more days ago, no answer yet, and no reminder set.</CardDescription>
              </CardHeader>
              <CardContent>
                {d.quiet.total === 0 ? (
                  <p className="text-sm text-muted-foreground">Nothing waiting without a reminder.</p>
                ) : (
                  <ul className="divide-y">
                    {d.quiet.items.map((o) => (
                      <li key={o.id} className="flex flex-wrap items-center justify-between gap-x-3 gap-y-2 py-2">
                        <div className="min-w-0 flex-1 basis-48">
                          <Link href={`/opportunities/${o.id}`} className="text-sm font-medium break-words hover:underline">
                            {o.title}
                          </Link>
                          <p className="text-xs text-muted-foreground">
                            {o.companyName} · applied {days(o.daysWaiting)} ago
                          </p>
                        </div>
                        <Button variant="outline" size="sm" onClick={() => setRemindFor({ id: o.id, title: o.title })}>
                          <BellPlus />
                          Remind me
                        </Button>
                      </li>
                    ))}
                  </ul>
                )}
                {d.quiet.total > d.quiet.items.length && (
                  <p className="pt-2 text-xs text-muted-foreground">And {d.quiet.total - d.quiet.items.length} more.</p>
                )}
              </CardContent>
            </Card>
          )}

          {d && (
            <Card>
              <CardHeader>
                <CardTitle>Next up</CardTitle>
                <CardDescription>Not applied to yet: by priority, then fit, then the oldest.</CardDescription>
              </CardHeader>
              <CardContent>
                {d.nextUp.total === 0 ? (
                  <p className="text-sm text-muted-foreground">Nothing saved or in preparation.</p>
                ) : (
                  <ul className="divide-y">
                    {d.nextUp.items.map((o) => (
                      <li key={o.id} className="space-y-1 py-2">
                        <Link href={`/opportunities/${o.id}`} className="block text-sm font-medium break-words hover:underline">
                          {o.title}
                        </Link>
                        <div className="flex flex-wrap items-center gap-1.5 text-xs text-muted-foreground">
                          <span>{o.companyName}</span>
                          <span aria-hidden>·</span>
                          <span>{STAGE_LABELS[o.stage]}</span>
                          {o.priority === "high" && <Badge variant="secondary">High priority</Badge>}
                          {o.priority === "low" && <Badge variant="outline">Low priority</Badge>}
                          {o.band && o.score !== null && (
                            <Badge variant={BAND_BADGES[o.band]}>
                              {BAND_LABELS[o.band]} · {o.score}
                            </Badge>
                          )}
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
                {d.nextUp.total > d.nextUp.items.length && (
                  <p className="pt-2 text-xs text-muted-foreground">
                    And {d.nextUp.total - d.nextUp.items.length} more on the{" "}
                    <Link href="/board" className="text-link hover:underline">board</Link>.
                  </p>
                )}
              </CardContent>
            </Card>
          )}
        </div>
      </div>

      <Link href="/analytics" className="inline-flex items-center gap-1 self-start text-sm text-link hover:underline">
        How is it going? See your analytics <ArrowRight className="size-3.5" />
      </Link>

      {/* On phones the tab bar holds four areas; this is the way to the rest. */}
      <section aria-labelledby="more" className="space-y-2 lg:hidden">
        <h2 id="more" className="font-heading text-xl font-bold tracking-[-0.01em] dark:text-base dark:font-semibold">
          Everything else
        </h2>
        <ul className="grid grid-cols-2 gap-2">
          {MODULES.filter((m) => !MOBILE_TAB_SLUGS.includes(m.slug)).map((m) => {
            const Icon = m.icon;
            return (
              <li key={m.slug}>
                <Link href={`/${m.slug}`} className="block h-full">
                  <Card className="h-full gap-0 py-3 transition-colors hover:bg-accent">
                    <CardContent className="flex items-center gap-2 px-3 text-sm font-semibold">
                      <Icon className="size-4 shrink-0" strokeWidth={2.25} />
                      {m.label}
                    </CardContent>
                  </Card>
                </Link>
              </li>
            );
          })}
        </ul>
      </section>

      {remindFor && (
        <ReminderDialog
          opportunityId={remindFor.id}
          initial={{ title: `Follow up on ${remindFor.title}`.slice(0, 200), kind: "follow_up" }}
          onClose={() => setRemindFor(null)}
        />
      )}
    </div>
  );
}
