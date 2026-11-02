"use client";

import { Field } from "@/components/profile/field";
import { SourcePostings } from "@/components/sources/source-postings";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { NativeSelect } from "@/components/ui/native-select";
import { describeApiError, fieldError, NO_ERRORS, type FormErrors } from "@/lib/api/form-errors";
import { createSource, deleteSource, listSources } from "@/lib/api/imports";
import { queryKeys } from "@/lib/query/query-keys";
import { parseBoardLink, PROVIDERS, type Provider } from "@/lib/schemas/imports";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ChevronDown, ChevronRight, Plus, Trash2 } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

const PROVIDER_LABELS: Record<Provider, string> = { greenhouse: "Greenhouse", lever: "Lever", ashby: "Ashby" };

const when = new Intl.DateTimeFormat(undefined, { dateStyle: "medium" });

/** Company job boards the user follows. Browsing one is read-only: picking is what saves. */
export function SourcesCard() {
  const queryClient = useQueryClient();
  const sources = useQuery({ queryKey: queryKeys.sources, queryFn: listSources });
  const [open, setOpen] = useState<string | null>(null);

  const remove = useMutation({
    mutationFn: deleteSource,
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.sources }),
    onError: () => toast.error("Could not remove it."),
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>Company job boards</CardTitle>
        <CardDescription>
          Follow a company&apos;s public board on Greenhouse, Lever or Ashby, then pick the postings worth saving.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        <AddSourceForm />

        {sources.isError && <p className="text-sm text-destructive">Could not load your job boards.</p>}

        {sources.data && sources.data.length > 0 && (
          <ul className="space-y-2">
            {sources.data.map((source) => {
              const expanded = open === source.id;
              return (
                <li key={source.id} className="rounded-xl border-2 p-3 dark:border">
                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      aria-expanded={expanded}
                      onClick={() => setOpen(expanded ? null : source.id)}
                      className="flex min-h-9 min-w-0 flex-1 items-center gap-2 text-left"
                    >
                      {expanded ? <ChevronDown aria-hidden className="size-4 shrink-0" /> : <ChevronRight aria-hidden className="size-4 shrink-0" />}
                      <span className="truncate font-semibold">{source.label}</span>
                      <Badge variant="outline">{PROVIDER_LABELS[source.provider]}</Badge>
                    </button>
                    {source.lastCheckedAt && (
                      <span className="hidden font-mono text-xs text-muted-foreground sm:inline">
                        checked {when.format(new Date(source.lastCheckedAt))}
                      </span>
                    )}
                    <Button
                      variant="ghost"
                      size="icon-sm"
                      aria-label={`Stop following ${source.label}`}
                      disabled={remove.isPending}
                      onClick={() => remove.mutate(source.id)}
                    >
                      <Trash2 />
                    </Button>
                  </div>
                  {expanded && (
                    <div className="mt-3">
                      <SourcePostings source={source} />
                    </div>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

function AddSourceForm() {
  const queryClient = useQueryClient();
  const [provider, setProvider] = useState<Provider>("greenhouse");
  const [board, setBoard] = useState("");
  const [label, setLabel] = useState("");
  const [errors, setErrors] = useState<FormErrors>(NO_ERRORS);

  const add = useMutation({
    mutationFn: () => createSource({ provider, board: board.trim(), label: label.trim() || null }),
    onSuccess: (source) => {
      setBoard("");
      setLabel("");
      void queryClient.invalidateQueries({ queryKey: queryKeys.sources });
      toast.success(`Following ${source.label}`);
    },
    onError: (error) => setErrors(describeApiError(error)),
  });

  return (
    <form
      className="space-y-3"
      noValidate
      onSubmit={(e) => {
        e.preventDefault();
        setErrors(NO_ERRORS);
        add.mutate();
      }}
    >
      {errors.message && (
        <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {errors.message}
        </p>
      )}
      <div className="grid gap-3 sm:grid-cols-[9rem_1fr_1fr]">
        <Field id="source-provider" label="Hosted on" error={fieldError(errors, "provider")}>
          <NativeSelect id="source-provider" value={provider} onChange={(e) => setProvider(e.target.value as Provider)}>
            {PROVIDERS.map((p) => (
              <option key={p} value={p}>
                {PROVIDER_LABELS[p]}
              </option>
            ))}
          </NativeSelect>
        </Field>
        <Field id="source-board" label="Board link or name" error={fieldError(errors, "board")} hint="Paste the company's careers link.">
          <Input
            id="source-board"
            autoComplete="off"
            placeholder="jobs.lever.co/contoso"
            value={board}
            onChange={(e) => {
              // A pasted link says both where it's hosted and which board it is.
              const link = parseBoardLink(e.target.value);
              if (link) {
                setProvider(link.provider);
                setBoard(link.board);
              } else {
                setBoard(e.target.value);
              }
            }}
          />
        </Field>
        <Field id="source-label" label="Company (optional)" error={fieldError(errors, "label")}>
          <Input id="source-label" maxLength={80} value={label} onChange={(e) => setLabel(e.target.value)} />
        </Field>
      </div>
      <Button type="submit" variant="outline" size="sm" disabled={add.isPending || !board.trim()}>
        <Plus />
        {add.isPending ? "Checking the board…" : "Follow this board"}
      </Button>
    </form>
  );
}
