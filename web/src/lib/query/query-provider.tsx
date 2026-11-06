"use client";

import { MutationCache, QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";

import { ApiError } from "@/lib/api/client";
import { queryKeys } from "@/lib/query/query-keys";

function makeQueryClient() {
  const client: QueryClient = new QueryClient({
    mutationCache: new MutationCache({
      // Analytics and the dashboard are computed from everything, so any change can move them.
      onSuccess: () => void client.invalidateQueries({ queryKey: queryKeys.analytics }),
    }),
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        retry: (failureCount, error) => {
          // Client errors (4xx) are never fixed by retrying.
          if (error instanceof ApiError && error.status < 500) return false;
          return failureCount < 2;
        },
      },
    },
  });
  return client;
}

export function QueryProvider({ children }: { children: React.ReactNode }) {
  const [queryClient] = useState(makeQueryClient);
  return (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}
