"use client";

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { listAuditLogs } from "@/lib/api/auth";
import { auditLabel, isSecurityWarning } from "@/lib/audit";
import { queryKeys } from "@/lib/query/query-keys";
import { useQuery } from "@tanstack/react-query";
import { TriangleAlert } from "lucide-react";

const when = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" });

/** The last ten events on the account, so an unexpected sign-in stands out. */
export function ActivityCard() {
  const { data, isLoading, isError } = useQuery({ queryKey: queryKeys.auditLogs, queryFn: () => listAuditLogs(10) });

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Recent activity</CardTitle>
        <CardDescription>Sign-ins and security changes on your account.</CardDescription>
      </CardHeader>
      <CardContent>
        {isLoading && <p className="text-sm text-muted-foreground">Loading activity…</p>}
        {isError && <p className="text-sm text-destructive">Could not load activity.</p>}
        {data && (
          <ul className="divide-y">
            {data.items.map((entry) => (
              <li
                key={entry.id}
                className="flex flex-col gap-0.5 py-2.5 sm:flex-row sm:items-center sm:justify-between sm:gap-4"
              >
                <span className={`flex items-center gap-2 text-sm ${isSecurityWarning(entry.action) ? "text-warning" : ""}`}>
                  {isSecurityWarning(entry.action) && <TriangleAlert aria-hidden className="size-4 shrink-0" />}
                  {auditLabel(entry.action)}
                </span>
                <span className="shrink-0 font-mono text-xs text-muted-foreground">
                  {when.format(new Date(entry.createdAt))}
                  {entry.ip && ` · ${entry.ip}`}
                </span>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
