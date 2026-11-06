"use client";

import { countTicks, weekLabel } from "@/lib/analytics";
import { useElementWidth } from "@/lib/hooks/use-element-width";
import type { Week } from "@/lib/schemas/analytics";
import { type KeyboardEvent, useState } from "react";

const HEIGHT = 200;
const MARGIN = { top: 8, right: 8, bottom: 26, left: 32 };
const SERIES = [
  { key: "applications", label: "Applications sent", fill: "fill-viz-sent", swatch: "bg-viz-sent" },
  { key: "responses", label: "Answers received", fill: "fill-viz-answered", swatch: "bg-viz-answered" },
] as const;

/** A column rising from the baseline: square at the base, 4px round at the top. */
function columnPath(x: number, width: number, top: number, base: number): string | null {
  const height = base - top;
  if (height <= 0 || width <= 0) return null;
  const r = Math.min(4, width / 2, height);
  return `M${x},${base}V${top + r}Q${x},${top} ${x + r},${top}H${x + width - r}Q${x + width},${top} ${x + width},${top + r}V${base}Z`;
}

/**
 * Applications sent and answers received each week: two series, so a legend, columns side by
 * side with a 2px gap, a hover and keyboard readout per week, and the same numbers as a table.
 */
export function WeeklyChart({ weeks }: { weeks: Week[] }) {
  const [ref, width] = useElementWidth<HTMLDivElement>();
  const [active, setActive] = useState<number | null>(null);

  const max = Math.max(0, ...weeks.flatMap((w) => [w.applications, w.responses]));
  const ticks = countTicks(max);
  const top = ticks[ticks.length - 1];
  const innerWidth = Math.max(0, width - MARGIN.left - MARGIN.right);
  const innerHeight = HEIGHT - MARGIN.top - MARGIN.bottom;
  const band = weeks.length ? innerWidth / weeks.length : 0;
  const columnWidth = Math.max(1.5, Math.min(12, (band * 0.72 - 2) / 2));
  const y = (value: number) => MARGIN.top + innerHeight - (value / top) * innerHeight;
  const base = y(0);
  // Label as many weeks as fit, always including the latest.
  const labelEvery = Math.max(1, Math.ceil(weeks.length / Math.max(1, Math.floor(innerWidth / 56))));

  const onKeyDown = (event: KeyboardEvent<SVGSVGElement>) => {
    if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") return;
    event.preventDefault();
    const step = event.key === "ArrowLeft" ? -1 : 1;
    setActive((current) => Math.min(weeks.length - 1, Math.max(0, (current ?? weeks.length - 1) + (current === null ? 0 : step))));
  };

  const shown = active === null ? null : weeks[active];
  // Beside the week, never over its columns: to the right, or to the left near the edge.
  const TIP_WIDTH = 160;
  const bandRight = active === null ? 0 : MARGIN.left + (active + 1) * band;
  const tipX = bandRight + 8 + TIP_WIDTH <= width ? bandRight + 8 : Math.max(0, bandRight - band - 8 - TIP_WIDTH);

  return (
    <div className="space-y-3">
      <ul className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground" aria-label="Legend">
        {SERIES.map((s) => (
          <li key={s.key} className="flex items-center gap-1.5">
            <span className={`size-2.5 rounded-[2px] ${s.swatch}`} aria-hidden />
            {s.label}
          </li>
        ))}
      </ul>

      <div ref={ref} className="relative">
        {width > 0 && (
          <svg
            width={width}
            height={HEIGHT}
            role="img"
            aria-label={`Applications sent and answers received in each of ${weeks.length} weeks. Use the arrow keys to read a week, or open the table below.`}
            tabIndex={0}
            onKeyDown={onKeyDown}
            onFocus={() => setActive((current) => current ?? weeks.length - 1)}
            onBlur={() => setActive(null)}
            onPointerLeave={() => setActive(null)}
            className="block rounded-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            {ticks.map((t) => (
              <g key={t}>
                <line x1={MARGIN.left} x2={width - MARGIN.right} y1={y(t)} y2={y(t)} className="stroke-viz-grid" strokeWidth={1} shapeRendering="crispEdges" />
                <text x={MARGIN.left - 8} y={y(t)} dy="0.32em" textAnchor="end" className="fill-muted-foreground text-[11px] tabular-nums">
                  {t}
                </text>
              </g>
            ))}

            {active !== null && (
              <rect x={MARGIN.left + active * band} y={MARGIN.top} width={band} height={innerHeight} className="fill-muted" />
            )}

            {weeks.map((w, i) => {
              const center = MARGIN.left + (i + 0.5) * band;
              const sent = columnPath(center - 1 - columnWidth, columnWidth, y(w.applications), base);
              const answered = columnPath(center + 1, columnWidth, y(w.responses), base);
              const labelled = (weeks.length - 1 - i) % labelEvery === 0;
              const anchor = center + 24 > width ? "end" : center - 24 < 0 ? "start" : "middle";
              return (
                <g key={w.weekStart}>
                  <g opacity={w.partial ? 0.45 : 1}>
                    {sent && <path d={sent} className="fill-viz-sent" />}
                    {answered && <path d={answered} className="fill-viz-answered" />}
                  </g>
                  {labelled && (
                    <text x={center} y={HEIGHT - 8} textAnchor={anchor} className="fill-muted-foreground text-[11px]">
                      {weekLabel(w.weekStart)}
                    </text>
                  )}
                  {/* The whole week is the hit target, not the thin columns. */}
                  <rect
                    x={MARGIN.left + i * band}
                    y={MARGIN.top}
                    width={band}
                    height={innerHeight}
                    fill="transparent"
                    onPointerEnter={() => setActive(i)}
                    onPointerMove={() => setActive(i)}
                  />
                </g>
              );
            })}
          </svg>
        )}

        {shown && (
          <div
            role="status"
            className="pointer-events-none absolute top-0 z-10 rounded-md border bg-popover px-3 py-2 text-xs text-popover-foreground shadow-md"
            style={{ left: tipX, width: TIP_WIDTH }}
          >
            <p className="mb-1 text-muted-foreground">
              Week of {weekLabel(shown.weekStart)}
              {shown.partial && " (partial)"}
            </p>
            {SERIES.map((s) => (
              <p key={s.key} className="flex items-center gap-2">
                <span className={`h-0.5 w-3 shrink-0 ${s.swatch}`} aria-hidden />
                <span className="font-semibold tabular-nums">{shown[s.key]}</span>
                <span className="text-muted-foreground">{s.key === "applications" ? "sent" : "answers"}</span>
              </p>
            ))}
          </div>
        )}
      </div>

      <p className="text-xs text-muted-foreground">
        Weeks start on Monday. Lighter columns are weeks the range only partly covers. Answers count in the week they
        arrived, even for applications sent earlier.
      </p>

      <details className="text-sm">
        <summary className="cursor-pointer text-link hover:underline">Show as a table</summary>
        <table className="mt-2 w-full text-sm">
          <thead>
            <tr className="text-left text-xs text-muted-foreground">
              <th className="py-1.5 font-medium">Week of</th>
              <th className="py-1.5 text-right font-medium">Sent</th>
              <th className="py-1.5 text-right font-medium">Answers</th>
            </tr>
          </thead>
          <tbody className="tabular-nums">
            {weeks.map((w) => (
              <tr key={w.weekStart} className="border-t">
                <td className="py-1.5">
                  {weekLabel(w.weekStart)}
                  {w.partial && <span className="text-muted-foreground"> (partial)</span>}
                </td>
                <td className="py-1.5 text-right">{w.applications}</td>
                <td className="py-1.5 text-right">{w.responses}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </details>
    </div>
  );
}
