import { BriefcaseBusiness } from "lucide-react";
import { cn } from "@/lib/utils";

// Light: a lime postage stamp with a perforated edge. Dark: the violet→blue tile.
// Same mark as Outreach Studio with this app's glyph, so the toolkit reads as one family.
export function LogoMark({ withWordmark = true, className }: { withWordmark?: boolean; className?: string }) {
  return (
    <div className={cn("flex items-center gap-2.5", className)}>
      <span className="flex size-8 shrink-0 -rotate-3 items-center justify-center rounded-[3px] bg-primary text-primary-foreground outline-2 outline-offset-[-5px] outline-[#111111] outline-dashed dark:rotate-0 dark:rounded-lg dark:bg-linear-to-br dark:from-brand dark:to-brand-2 dark:text-[#0a0a0f] dark:outline-0">
        <BriefcaseBusiness className="size-4" strokeWidth={2.25} />
      </span>
      {withWordmark && <span className="text-base font-bold tracking-tight">JobHunter OS</span>}
    </div>
  );
}
