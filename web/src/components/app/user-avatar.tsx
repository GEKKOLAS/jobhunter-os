import { Avatar, AvatarFallback } from "@/components/ui/avatar";

export function initials(name: string) {
  const parts = name.trim().split(/\s+/);
  return ((parts[0]?.[0] ?? "") + (parts[1]?.[0] ?? "")).toUpperCase() || "U";
}

/** A tilted dashed stamp in Mailroom, a soft gradient disc in Nocturne. */
export function UserAvatar({ name }: { name: string }) {
  return (
    <Avatar size="sm">
      <AvatarFallback className="-rotate-3 rounded-[3px] border-2 border-dashed border-[#111111] bg-[#ff9ed2] font-bold text-[#111111] dark:rotate-0 dark:rounded-full dark:border-0 dark:bg-linear-to-br dark:from-brand dark:to-brand-2 dark:text-white">
        {initials(name)}
      </AvatarFallback>
    </Avatar>
  );
}
