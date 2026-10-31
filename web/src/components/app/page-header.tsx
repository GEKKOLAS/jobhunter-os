import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

export function PageHeader({
  title,
  description,
  eyebrow,
  icon: Icon,
  action,
}: {
  title: string;
  description?: string;
  /** Small mono line above the title, e.g. a date or a count. */
  eyebrow?: string;
  icon?: LucideIcon;
  action?: ReactNode;
}) {
  return (
    <header className="flex flex-wrap items-end justify-between gap-4">
      <div className="flex items-start gap-3.5">
        {Icon && (
          <span className="mt-1 flex size-10 shrink-0 -rotate-3 items-center justify-center rounded-lg border-2 bg-primary text-primary-foreground shadow-hard-sm dark:rotate-0 dark:border-0 dark:bg-primary/12 dark:text-primary dark:shadow-none">
            <Icon className="size-5" />
          </span>
        )}
        <div>
          {eyebrow && (
            <p className="mb-1.5 font-mono text-xs font-bold tracking-[0.04em] uppercase dark:font-medium dark:text-muted-foreground">
              {eyebrow}
            </p>
          )}
          <h1 className="font-heading text-4xl leading-tight font-bold tracking-[-0.035em] dark:text-[1.75rem] dark:font-semibold dark:tracking-tight">
            {title}
          </h1>
          {description && <p className="mt-1 text-muted-foreground">{description}</p>}
        </div>
      </div>
      {action}
    </header>
  );
}
