"use client";

import { DraftDialog } from "@/components/follow-ups/draft-dialog";
import { Field, ToggleChip } from "@/components/profile/field";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { NativeSelect } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";
import { createDraft, getAiCapabilities } from "@/lib/api/follow-ups";
import { describeApiError, NO_ERRORS, type FormErrors } from "@/lib/api/form-errors";
import { DRAFT_KIND_LABELS, parseFacts } from "@/lib/follow-ups";
import { queryKeys } from "@/lib/query/query-keys";
import { type CreatedDraft, DRAFT_KINDS, type DraftKind } from "@/lib/schemas/follow-ups";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

/**
 * Starts a draft for an opportunity: who it's to, and, when Claude is set up, the facts it may
 * use. It writes from those alone, and its answer is checked before you see it.
 */
export function NewDraftDialog({
  opportunityId,
  people,
  initialKind = "follow_up",
  onClose,
}: {
  opportunityId: string | null;
  people: { contactId: string; name: string }[];
  initialKind?: DraftKind;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const ai = useQuery({ queryKey: queryKeys.aiCapabilities, queryFn: getAiCapabilities, staleTime: Infinity });
  const [kind, setKind] = useState<DraftKind>(initialKind);
  // Null until picked: the first linked person is the default even if they load after this opens.
  const [picked, setPicked] = useState<string | null>(null);
  const contactId = picked ?? people[0]?.contactId ?? "";
  const setContactId = setPicked;
  const [generate, setGenerate] = useState(true);
  const [facts, setFacts] = useState("");
  const [errors, setErrors] = useState<FormErrors>(NO_ERRORS);
  const [created, setCreated] = useState<CreatedDraft | null>(null);

  const canWrite = ai.data?.drafts ?? false;
  const start = useMutation({
    mutationFn: () => createDraft({ kind, opportunityId, contactId: contactId || null, generate: canWrite && generate, facts: parseFacts(facts) }),
    onSuccess: (result) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.drafts });
      setCreated(result);
    },
    onError: (error) => setErrors(describeApiError(error)),
  });

  if (created) return <DraftDialog draft={created.draft} note={created.generationNote} onClose={onClose} />;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>New email</DialogTitle>
          <DialogDescription>
            {canWrite
              ? "Claude can write a first version from the facts you give it, and nothing else. You review it before anything is sent."
              : "You write it, then approve it before it goes anywhere."}
          </DialogDescription>
        </DialogHeader>

        {errors.message && (
          <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {errors.message}
          </p>
        )}

        <div className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field id="new-draft-kind" label="Kind">
              <NativeSelect id="new-draft-kind" value={kind} onChange={(e) => setKind(e.target.value as DraftKind)}>
                {DRAFT_KINDS.map((k) => (
                  <option key={k} value={k}>
                    {DRAFT_KIND_LABELS[k]}
                  </option>
                ))}
              </NativeSelect>
            </Field>
            <Field id="new-draft-to" label="To" hint={people.length === 0 ? "Link someone to this opportunity to write to them." : undefined}>
              <NativeSelect id="new-draft-to" value={contactId} onChange={(e) => setContactId(e.target.value)}>
                <option value="">Nobody yet</option>
                {people.map((p) => (
                  <option key={p.contactId} value={p.contactId}>
                    {p.name}
                  </option>
                ))}
              </NativeSelect>
            </Field>
          </div>

          {canWrite && (
            <>
              <div className="flex flex-wrap gap-1.5" role="group" aria-label="How to start">
                <ToggleChip pressed={generate} onToggle={() => setGenerate(true)}>
                  Claude writes a first version
                </ToggleChip>
                <ToggleChip pressed={!generate} onToggle={() => setGenerate(false)}>
                  Start blank
                </ToggleChip>
              </div>
              {generate && (
                <Field id="new-draft-facts" label="What it may say" hint="One fact per line, in your own words. It won't add anything you don't put here.">
                  <Textarea
                    id="new-draft-facts"
                    rows={4}
                    placeholder={"I built a payments API in C# at my last job.\nWe spoke on Tuesday about the team."}
                    value={facts}
                    onChange={(e) => setFacts(e.target.value)}
                  />
                </Field>
              )}
            </>
          )}
        </div>

        <DialogFooter>
          <Button disabled={start.isPending} onClick={() => start.mutate()}>
            {start.isPending ? (canWrite && generate ? "Writing…" : "Starting…") : canWrite && generate ? "Write it" : "Start the draft"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
