"use client";

import { listSessions, revokeSession } from "@/lib/api/auth";
import { queryKeys } from "@/lib/query/query-keys";
import type { Session } from "@/lib/schemas/auth";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

function formatWhen(iso: string): string {
  return new Date(iso).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
}

export function SessionsCard() {
  const queryClient = useQueryClient();
  const { data: sessions, isLoading, isError } = useQuery({
    queryKey: queryKeys.sessions,
    queryFn: listSessions,
  });

  const revoke = useMutation({
    mutationFn: revokeSession,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.sessions });
      toast.success("Session revoked");
    },
    onError: () => toast.error("Could not revoke session"),
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Active sessions</CardTitle>
        <CardDescription>Devices currently signed in to your account.</CardDescription>
      </CardHeader>
      <CardContent>
        {isLoading && <p className="text-sm text-muted-foreground">Loading sessions…</p>}
        {isError && <p className="text-sm text-destructive">Could not load sessions.</p>}
        {sessions && sessions.length === 0 && (
          <p className="text-sm text-muted-foreground">No active sessions.</p>
        )}
        {sessions && sessions.length > 0 && (
          <ul className="divide-y">
            {sessions.map((session: Session) => (
              <li key={session.id} className="flex items-center justify-between gap-4 py-3">
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium">
                    {session.userAgent ?? "Unknown device"}
                    {session.current && (
                      <span className="ml-2 rounded-full bg-link/10 px-2 py-0.5 text-xs font-medium text-link">
                        This device
                      </span>
                    )}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {session.ip ?? "unknown IP"} · last active {formatWhen(session.lastSeenAt)}
                  </p>
                </div>
                {!session.current && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => revoke.mutate(session.id)}
                    disabled={revoke.isPending}
                  >
                    Revoke
                  </Button>
                )}
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
