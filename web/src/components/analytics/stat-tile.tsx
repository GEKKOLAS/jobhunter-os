import { Card, CardContent } from "@/components/ui/card";
import type { Reading } from "@/lib/analytics";
import { cn } from "@/lib/utils";

/** One number with what it came from. Without enough data it says so instead of showing a figure. */
export function StatTile({ label, reading, hint }: { label: string; reading: Reading; hint?: string }) {
  return (
    <Card className="gap-0 py-4">
      <CardContent className="flex h-full flex-col gap-1">
        <p className="text-sm text-muted-foreground">{label}</p>
        <p
          className={cn(
            "font-heading font-semibold tracking-tight",
            reading.known ? "text-3xl" : "text-lg text-muted-foreground",
          )}
        >
          {reading.headline}
        </p>
        <p className="mt-auto text-xs text-muted-foreground">{reading.detail}</p>
        {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
      </CardContent>
    </Card>
  );
}
