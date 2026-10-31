import { PageHeader } from "@/components/app/page-header";
import { Card, CardContent } from "@/components/ui/card";
import { phaseOf, type AppModule } from "@/lib/modules";
import Link from "next/link";

/** What an area will do and which phase builds it, until that phase replaces this page. */
export function ModulePlaceholder({ area }: { area: AppModule }) {
  const phase = phaseOf(area.phase);
  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <PageHeader
        icon={area.icon}
        eyebrow={`Phase ${area.phase}${phase ? ` · ${phase.title}` : ""}`}
        title={area.label}
        description={area.summary}
      />
      <Card>
        <CardContent className="flex flex-col gap-2 py-6">
          <p className="font-semibold">Not built yet.</p>
          <p className="text-sm text-muted-foreground">
            This page arrives in phase {area.phase}. The{" "}
            <Link href="/dashboard" className="text-link underline-offset-4 hover:underline">
              roadmap
            </Link>{" "}
            shows what is done and what comes next.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
