import { BAND_LABELS } from "@/lib/matching";
import type { Band } from "@/lib/schemas/matching";

/** The score on a card or a row. Only ever shown with a score: an opportunity without one has no chip. */
export function FitChip({ score, band }: { score: number | null; band: Band | null }) {
  if (score === null || band === null) return null;
  return (
    <span
      title={`${BAND_LABELS[band]}. An estimate, not a prediction.`}
      className="inline-block shrink-0 rounded-sm bg-primary px-1.5 py-0.5 font-mono text-[10px] font-bold whitespace-nowrap text-primary-foreground dark:bg-primary/15 dark:text-primary"
    >
      fit {score}
    </span>
  );
}
