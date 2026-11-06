"use client";

import { Field } from "@/components/profile/field";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { approveDraft, discardDraft, getOutreach, markDraftSent, sendDraft, updateDraft } from "@/lib/api/follow-ups";
import { describeApiError, fieldError, isConflict, NO_ERRORS, type FormErrors } from "@/lib/api/form-errors";
import { DRAFT_KIND_LABELS, DRAFT_STATUS_LABELS, draftAsText } from "@/lib/follow-ups";
import { queryKeys } from "@/lib/query/query-keys";
import type { Draft } from "@/lib/schemas/follow-ups";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Copy, Send } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { toast } from "sonner";

/**
 * One draft, from editing to sent. Approval is its own step and any edit takes it back, so what
 * goes out is exactly what was approved. Nothing is sent but by the Send click (PRD principle 3).
 */
export function DraftDialog({ draft: initial, note, onClose }: { draft: Draft; note?: string | null; onClose: () => void }) {
  const queryClient = useQueryClient();
  const [draft, setDraft] = useState(initial);
  const [toEmail, setToEmail] = useState(initial.toEmail ?? "");
  const [subject, setSubject] = useState(initial.subject);
  const [body, setBody] = useState(initial.body);
  const [errors, setErrors] = useState<FormErrors>(NO_ERRORS);
  const outreach = useQuery({ queryKey: queryKeys.outreach, queryFn: getOutreach });

  const closed = draft.status === "sent" || draft.status === "discarded";
  const dirty = toEmail !== (draft.toEmail ?? "") || subject !== draft.subject || body !== draft.body;

  const refresh = (next?: Draft) => {
    if (next) setDraft(next);
    void queryClient.invalidateQueries({ queryKey: queryKeys.drafts });
    // A sent draft logs the email on the contact, so their log and lists change too.
    void queryClient.invalidateQueries({ queryKey: queryKeys.contacts });
  };
  const failed = (error: unknown) => {
    if (isConflict(error)) {
      toast.error(describeApiError(error).message);
      refresh();
      onClose();
    } else {
      setErrors(describeApiError(error));
    }
  };

  const save = useMutation({
    mutationFn: () => updateDraft(draft.id, { toEmail: toEmail.trim() || null, subject, body, version: draft.version }),
    onSuccess: (saved) => {
      refresh(saved);
      toast.success(draft.status === "approved" ? "Saved. Approve it again before sending." : "Saved");
    },
    onError: failed,
  });
  const approve = useMutation({ mutationFn: () => approveDraft(draft.id), onSuccess: (d) => refresh(d), onError: failed });
  const send = useMutation({
    mutationFn: () => sendDraft(draft.id),
    onSuccess: (d) => {
      refresh(d);
      toast.success("Sent through Outreach Studio");
    },
    onError: failed,
  });
  const markSent = useMutation({
    mutationFn: () => markDraftSent(draft.id),
    onSuccess: (d) => {
      refresh(d);
      toast.success("Marked as sent");
    },
    onError: failed,
  });
  const discard = useMutation({
    mutationFn: () => discardDraft(draft.id),
    onSuccess: () => {
      refresh();
      onClose();
    },
    onError: failed,
  });
  const busy = save.isPending || approve.isPending || send.isPending || markSent.isPending || discard.isPending;

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(draftAsText({ subject, body }));
      toast.success("Copied. Paste it into your email, then mark it sent.");
    } catch {
      toast.error("Couldn't copy here. Select the text and copy it yourself.");
    }
  };

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-xl">
        <DialogHeader>
          <DialogTitle className="flex flex-wrap items-center gap-2">
            {DRAFT_KIND_LABELS[draft.kind]}
            <Badge variant={draft.status === "approved" ? "success" : draft.status === "sent" ? "info" : "outline"}>
              {DRAFT_STATUS_LABELS[draft.status]}
            </Badge>
            {draft.origin === "ai" && <Badge variant="secondary">Written by Claude</Badge>}
          </DialogTitle>
          <DialogDescription>
            {[draft.contactName && `To ${draft.contactName}`, draft.opportunityTitle].filter(Boolean).join(" · ") || "An email of your own."}
          </DialogDescription>
        </DialogHeader>

        {note && (
          <p role="status" className="rounded-md bg-warning-soft px-3 py-2 text-sm">
            {note}
          </p>
        )}
        {errors.message && (
          <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {errors.message}
          </p>
        )}

        <div className="space-y-4">
          <Field id="draft-to" label="To" error={fieldError(errors, "toEmail")}>
            <Input id="draft-to" type="email" maxLength={254} disabled={closed} value={toEmail} onChange={(e) => setToEmail(e.target.value)} />
          </Field>
          <Field id="draft-subject" label="Subject" error={fieldError(errors, "subject")}>
            <Input id="draft-subject" maxLength={200} disabled={closed} value={subject} onChange={(e) => setSubject(e.target.value)} />
          </Field>
          <Field id="draft-body" label="Message" error={fieldError(errors, "body")} hint="Plain text. Check every word: once sent, it's yours.">
            <Textarea id="draft-body" rows={10} maxLength={10000} disabled={closed} value={body} onChange={(e) => setBody(e.target.value)} />
          </Field>
        </div>

        {draft.status === "sent" && (
          <p className="text-sm text-muted-foreground">
            Sent {draft.sentVia === "outreach_studio" ? "through Outreach Studio" : "by you"}
            {draft.sentAt && ` on ${new Date(draft.sentAt).toLocaleString()}`}
            {draft.contactId ? ", and logged on the contact." : "."}
          </p>
        )}

        {!closed && (
          <DialogFooter className="flex-wrap gap-2 sm:justify-between">
            <Button variant="ghost" size="sm" disabled={busy} onClick={() => discard.mutate()}>
              Discard
            </Button>
            <div className="flex flex-wrap justify-end gap-2">
              {dirty && (
                <Button variant="outline" disabled={busy} onClick={() => save.mutate()}>
                  {save.isPending ? "Saving…" : "Save"}
                </Button>
              )}
              {!dirty && draft.status === "draft" && (
                <Button disabled={busy || !subject.trim() || !body.trim()} onClick={() => approve.mutate()}>
                  Approve
                </Button>
              )}
              {!dirty && draft.status === "approved" && (outreach.data?.configured ? (
                <Button disabled={busy || !toEmail.trim()} onClick={() => send.mutate()}>
                  <Send />
                  {send.isPending ? "Sending…" : `Send to ${toEmail || "…"}`}
                </Button>
              ) : (
                <>
                  <Button variant="outline" onClick={copy}>
                    <Copy />
                    Copy
                  </Button>
                  <Button disabled={busy} onClick={() => markSent.mutate()}>
                    I sent it
                  </Button>
                </>
              ))}
            </div>
          </DialogFooter>
        )}
        {!closed && draft.status === "approved" && !toEmail.trim() && (
          <p className="text-xs text-muted-foreground">Add who it goes to before sending.</p>
        )}
        {!closed && draft.status === "approved" && outreach.data && !outreach.data.configured && (
          <p className="text-xs text-muted-foreground">
            To send from here instead, <Link href="/settings" className="text-link hover:underline">connect Outreach Studio</Link>.
          </p>
        )}
      </DialogContent>
    </Dialog>
  );
}
