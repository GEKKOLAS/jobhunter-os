import type { ReactNode } from "react";

/** A small line that explains a number, kept in one style across the analytics pages. */
export function AnalyticsNote({ children }: { children: ReactNode }) {
  return <p className="text-xs text-muted-foreground">{children}</p>;
}
