"use client";

import { ping } from "@/lib/api/ping";
import { cn } from "@/lib/utils";
import { useQuery } from "@tanstack/react-query";

/** Whether the web app can reach the API through its `/api` proxy. Rechecks every 30 s. */
export function ApiStatus({ className }: { className?: string }) {
  const { isPending, isError } = useQuery({
    queryKey: ["ping"],
    queryFn: ping,
    refetchInterval: 30_000,
    retry: false,
  });

  const [dot, label] = isPending
    ? ["bg-muted-foreground", "Checking API…"]
    : isError
      ? ["bg-[#f87171]", "API offline"]
      : ["bg-[#4ade80]", "API online"];

  return (
    <span role="status" className={cn("inline-flex items-center gap-2 text-[13px]", className)}>
      <span aria-hidden className={`size-2 shrink-0 rounded-full ${dot}`} />
      {label}
    </span>
  );
}
