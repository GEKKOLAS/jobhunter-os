import { ApiStatus } from "@/components/app/api-status";
import { PageHeader } from "@/components/app/page-header";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { currentPhase, MODULES, PHASES, type PhaseStatus } from "@/lib/modules";
import { LayoutDashboard } from "lucide-react";
import Link from "next/link";

const STATUS: Record<PhaseStatus, { label: string; variant: "success" | "info" | "outline" }> = {
  done: { label: "Done", variant: "success" },
  next: { label: "Next", variant: "info" },
  planned: { label: "Planned", variant: "outline" },
};

// Until phase 8 builds the real dashboard, this page is the roadmap: every area of the
// app and the phase that builds it (docs/spec/04-implementation-plan.md).
export default function DashboardPage() {
  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-8">
      <PageHeader
        icon={LayoutDashboard}
        eyebrow={`Phase ${currentPhase().phase} · ${currentPhase().title}`}
        title="JobHunter OS"
        description="The operations center for your job search. Here is what gets built, in order."
      />

      <section aria-labelledby="areas" className="flex flex-col gap-3">
        <h2 id="areas" className="font-heading text-xl font-bold tracking-[-0.01em] dark:text-base dark:font-semibold">
          Areas
        </h2>
        <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {MODULES.filter((m) => m.slug !== "dashboard").map((m) => {
            const Icon = m.icon;
            return (
              <li key={m.slug}>
                <Link href={`/${m.slug}`} className="block h-full">
                  <Card className="h-full transition-colors hover:bg-accent">
                    <CardHeader>
                      <CardTitle className="flex items-center gap-2">
                        <Icon className="size-4 shrink-0" strokeWidth={2.25} />
                        {m.label}
                      </CardTitle>
                      <CardDescription>{m.summary}</CardDescription>
                    </CardHeader>
                    <CardContent className="mt-auto">
                      <span className="font-mono text-xs text-muted-foreground">Phase {m.phase}</span>
                    </CardContent>
                  </Card>
                </Link>
              </li>
            );
          })}
        </ul>
      </section>

      <section aria-labelledby="roadmap" className="flex flex-col gap-3">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h2 id="roadmap" className="font-heading text-xl font-bold tracking-[-0.01em] dark:text-base dark:font-semibold">
            Roadmap
          </h2>
          <ApiStatus className="text-muted-foreground" />
        </div>
        <Card className="gap-0 py-0">
          <ol>
            {PHASES.map((p) => (
              <li
                key={p.phase}
                className="flex items-center gap-4 border-b-2 px-4 py-3 last:border-b-0 dark:border-b dark:border-border/60"
              >
                <span className="w-6 shrink-0 font-mono text-sm font-bold text-muted-foreground">{p.phase}</span>
                <span className="min-w-0 flex-1 text-sm font-semibold">{p.title}</span>
                <Badge variant={STATUS[p.status].variant}>{STATUS[p.status].label}</Badge>
              </li>
            ))}
          </ol>
        </Card>
      </section>
    </div>
  );
}
