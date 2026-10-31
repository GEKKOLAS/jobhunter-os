"use client";

import { getMe } from "@/lib/api/auth";
import { ApiError } from "@/lib/api/client";
import { queryKeys } from "@/lib/query/query-keys";
import { useQuery } from "@tanstack/react-query";

/**
 * Current-user query. A 401 resolves to `null` (unauthenticated) rather than
 * throwing so guards can branch on it without an error boundary.
 */
export function useSession() {
  return useQuery({
    queryKey: queryKeys.me,
    queryFn: async () => {
      try {
        return await getMe();
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) return null;
        throw error;
      }
    },
    staleTime: 60_000,
  });
}
