import { describeRate, FUNNEL_LABELS } from "@/lib/analytics";
import type { Summary } from "@/lib/schemas/analytics";

/**
 * The applications sent in the range and how far they got: one hue, one baseline, the count at
 * each bar's tip. "Saved" isn't drawn here: it's a different group of opportunities (spec 03).
 */
export function FunnelBars({ funnel }: { funnel: Summary["funnel"] }) {
  const cohort = funnel.filter((s) => s.step !== "saved");
  const max = Math.max(1, ...cohort.map((s) => s.count));

  return (
    <ol className="space-y-3">
      {cohort.map((s) => {
        const rate = s.rate && describeRate(s.rate);
        return (
          <li key={s.step} className="grid items-center gap-x-3 gap-y-1 sm:grid-cols-[8.5rem_1fr]">
            <span className="text-sm">{FUNNEL_LABELS[s.step]}</span>
            <div className="flex min-w-0 items-center gap-2">
              {/* The bar shares the row with its label, so it scales within what the label leaves. */}
              <div
                className="h-4 shrink-0 rounded-r-[4px] bg-viz-sent"
                style={{ width: `calc((100% - 8.5rem) * ${s.count / max})` }}
                aria-hidden
              />
              <span className="shrink-0 text-sm tabular-nums">
                <span className="font-semibold">{s.count}</span>
                {rate && (
                  <span className="text-muted-foreground">
                    {" · "}
                    {rate.known ? `${rate.headline} of sent` : "too few to say"}
                  </span>
                )}
              </span>
            </div>
          </li>
        );
      })}
    </ol>
  );
}
