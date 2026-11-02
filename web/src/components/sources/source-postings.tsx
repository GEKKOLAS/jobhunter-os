"use client";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { describeApiError } from "@/lib/api/form-errors";
import { listSourcePostings, saveSourcePosting } from "@/lib/api/imports";
import { queryKeys } from "@/lib/query/query-keys";
import type { JobSource } from "@/lib/schemas/imports";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ExternalLink } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

/** How many rows to draw before asking to search: a large company's board has hundreds. */
const VISIBLE = 40;

/** A board's postings, fetched live. Reading saves nothing; the user picks what to keep. */
export function SourcePostings({ source }: { source: JobSource }) {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const postings = useQuery({
    queryKey: queryKeys.sourcePostings(source.id),
    queryFn: () => listSourcePostings(source.id),
    staleTime: 60_000,
  });

  const save = useMutation({
    mutationFn: (postingRef: string) => saveSourcePosting(source.id, postingRef),
    onSuccess: (saved) => {
      toast.success(`${saved.title} saved to your board`);
      void queryClient.invalidateQueries({ queryKey: queryKeys.sourcePostings(source.id) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.board });
      void queryClient.invalidateQueries({ queryKey: queryKeys.opportunities });
      void queryClient.invalidateQueries({ queryKey: queryKeys.companies });
    },
    onError: (error) => toast.error(describeApiError(error).message ?? "Could not save it."),
  });

  if (postings.isPending) return <p className="text-sm text-muted-foreground">Asking the job board…</p>;
  if (postings.isError) {
    return <p className="text-sm text-destructive">{describeApiError(postings.error).message ?? "Could not read this board."}</p>;
  }
  if (postings.data.length === 0) return <p className="text-sm text-muted-foreground">No open postings on this board right now.</p>;

  const term = search.trim().toLowerCase();
  const matches = postings.data.filter(
    (p) => !term || p.title.toLowerCase().includes(term) || p.location?.toLowerCase().includes(term),
  );

  return (
    <div className="space-y-2">
      <Input
        aria-label={`Search ${source.label}'s postings`}
        placeholder={`Search ${postings.data.length} postings by role or place`}
        value={search}
        onChange={(e) => setSearch(e.target.value)}
      />
      <ul className="divide-y">
        {matches.slice(0, VISIBLE).map((posting) => (
          <li key={posting.ref} className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 py-2">
            <div className="min-w-0 flex-1 basis-56">
              <p className="truncate text-sm font-medium">
                {posting.url ? (
                  <a href={posting.url} target="_blank" rel="noopener noreferrer" className="flex items-center gap-1 hover:underline">
                    <span className="truncate">{posting.title}</span>
                    <ExternalLink aria-hidden className="size-3 shrink-0" />
                  </a>
                ) : (
                  posting.title
                )}
              </p>
              {posting.location && <p className="truncate text-xs text-muted-foreground">{posting.location}</p>}
            </div>
            {posting.alreadySaved ? (
              <Badge variant="success">Saved</Badge>
            ) : (
              <Button
                variant="outline"
                size="sm"
                aria-label={`Save ${posting.title}`}
                disabled={save.isPending}
                onClick={() => save.mutate(posting.ref)}
              >
                Save
              </Button>
            )}
          </li>
        ))}
      </ul>
      {matches.length === 0 && <p className="text-sm text-muted-foreground">Nothing matches that search.</p>}
      {matches.length > VISIBLE && (
        <p className="text-xs text-muted-foreground">
          Showing {VISIBLE} of {matches.length}. Search to narrow them down.
        </p>
      )}
    </div>
  );
}
