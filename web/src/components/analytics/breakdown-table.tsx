import { AnalyticsNote } from "@/components/analytics/analytics-note";
import { percent } from "@/lib/analytics";
import type { Breakdown, Rate } from "@/lib/schemas/analytics";

function CountWithRate({ count, rate }: { count: number; rate: Rate }) {
  return (
    <>
      <span className="font-semibold">{count}</span>
      {rate.value !== null && <span className="block text-xs text-muted-foreground">{percent(rate.value)}</span>}
    </>
  );
}

/**
 * Applications by source or by role, each count beside the rate it makes. A row with too few
 * applications shows its counts only, and the note under the table says why.
 */
export function BreakdownTable({ rows, heading, nameOf }: { rows: Breakdown[]; heading: string; nameOf: (key: string | null) => string }) {
  const withheld = rows.some((r) => r.applications > 0 && r.responseRate.value === null);

  return (
    <div className="space-y-2">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left text-xs text-muted-foreground">
            <th className="py-1.5 pr-2 font-medium">{heading}</th>
            <th className="py-1.5 pl-2 text-right font-medium">Sent</th>
            <th className="py-1.5 pl-2 text-right font-medium">Answered</th>
            <th className="py-1.5 pl-2 text-right font-medium">Interviews</th>
          </tr>
        </thead>
        <tbody className="tabular-nums">
          {rows.map((r) => (
            <tr key={r.key ?? "(other)"} className="border-t align-top">
              <td className="py-2 pr-2 break-words">{nameOf(r.key)}</td>
              <td className="py-2 pl-2 text-right font-semibold">{r.applications}</td>
              <td className="py-2 pl-2 text-right">
                <CountWithRate count={r.responses} rate={r.responseRate} />
              </td>
              <td className="py-2 pl-2 text-right">
                <CountWithRate count={r.interviews} rate={r.interviewRate} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {withheld && <AnalyticsNote>A row shows rates once it has at least 5 applications.</AnalyticsNote>}
    </div>
  );
}
