import { Label } from "@/components/ui/label";
import type { ReactNode } from "react";

/** A label, its control, and either the field's error or a hint below it. */
export function Field({
  id,
  label,
  error,
  hint,
  children,
}: {
  id: string;
  label: string;
  error?: string;
  hint?: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      {children}
      {error ? (
        <p className="text-sm text-destructive">{error}</p>
      ) : (
        hint && <p className="text-xs text-muted-foreground">{hint}</p>
      )}
    </div>
  );
}

/** A pressed/unpressed chip for picking values from a small set. */
export function ToggleChip({
  pressed,
  onToggle,
  children,
}: {
  pressed: boolean;
  onToggle: () => void;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      aria-pressed={pressed}
      onClick={onToggle}
      className={`rounded-full border-2 px-3 py-1 text-sm font-medium transition-colors dark:border ${
        pressed
          ? "border-[#111111] bg-primary text-primary-foreground dark:border-transparent"
          : "bg-card text-muted-foreground hover:text-foreground"
      }`}
    >
      {children}
    </button>
  );
}
