"use client";

import { getNotifications } from "@/lib/api/follow-ups";
import { queryKeys } from "@/lib/query/query-keys";
import { useQuery } from "@tanstack/react-query";

/** How many reminders came due unseen. Checked every minute; the worker makes them every five. */
export function UnreadBadge({ className = "" }: { className?: string }) {
  const notifications = useQuery({ queryKey: queryKeys.notifications, queryFn: getNotifications, refetchInterval: 60_000 });
  const count = notifications.data?.unreadCount ?? 0;
  if (count === 0) return null;
  return (
    <span
      aria-label={`${count} due`}
      className={`inline-flex min-w-5 items-center justify-center rounded-full bg-primary px-1.5 font-mono text-[10px] leading-5 font-bold text-primary-foreground ${className}`}
    >
      {count > 99 ? "99+" : count}
    </span>
  );
}
