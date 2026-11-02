"use client";

import { Field, ToggleChip } from "@/components/profile/field";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { NativeSelect } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";
import { logInteraction } from "@/lib/api/contacts";
import { describeApiError, fieldError, NO_ERRORS, type FormErrors } from "@/lib/api/form-errors";
import { CHANNEL_LABELS, DIRECTION_LABELS, toLocalInput } from "@/lib/contacts";
import { CHANNELS, type Channel, DIRECTIONS, type Direction } from "@/lib/schemas/contacts";
import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";

/**
 * Logs one conversation with a contact. A picker of opportunities is offered when there's a
 * choice to make; on an opportunity's own page it's already decided.
 */
export function InteractionForm({
  contactId,
  opportunities,
  fixedOpportunityId,
  onLogged,
  onCancel,
}: {
  contactId: string;
  /** What it could be about. */
  opportunities?: { id: string; title: string }[];
  fixedOpportunityId?: string;
  onLogged: () => void;
  onCancel?: () => void;
}) {
  const [channel, setChannel] = useState<Channel>("email");
  const [direction, setDirection] = useState<Direction>("inbound");
  const [occurredAt, setOccurredAt] = useState(() => toLocalInput(new Date()));
  const [summary, setSummary] = useState("");
  const [opportunityId, setOpportunityId] = useState("");
  const [errors, setErrors] = useState<FormErrors>(NO_ERRORS);

  const log = useMutation({
    mutationFn: () =>
      logInteraction(contactId, {
        channel,
        direction,
        // The input is in the viewer's time zone; the server keeps the moment itself.
        occurredAt: new Date(occurredAt).toISOString(),
        summary,
        opportunityId: fixedOpportunityId ?? (opportunityId || null),
      }),
    onSuccess: () => {
      toast.success("Logged");
      setSummary("");
      setOpportunityId("");
      setOccurredAt(toLocalInput(new Date()));
      setErrors(NO_ERRORS);
      onLogged();
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
        log.mutate();
      }}
    >
      {errors.message && (
        <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {errors.message}
        </p>
      )}

      <div className="flex flex-wrap gap-1.5" role="group" aria-label="Who reached out">
        {DIRECTIONS.map((d) => (
          <ToggleChip key={d} pressed={direction === d} onToggle={() => setDirection(d)}>
            {DIRECTION_LABELS[d]}
          </ToggleChip>
        ))}
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <Field id="interaction-channel" label="How" error={fieldError(errors, "channel")}>
          <NativeSelect id="interaction-channel" value={channel} onChange={(e) => setChannel(e.target.value as Channel)}>
            {CHANNELS.map((c) => (
              <option key={c} value={c}>
                {CHANNEL_LABELS[c]}
              </option>
            ))}
          </NativeSelect>
        </Field>
        <Field id="interaction-when" label="When" error={fieldError(errors, "occurredAt")}>
          <Input id="interaction-when" type="datetime-local" max={toLocalInput(new Date())} value={occurredAt} onChange={(e) => setOccurredAt(e.target.value)} />
        </Field>
      </div>

      {!fixedOpportunityId && opportunities && opportunities.length > 0 && (
        <Field id="interaction-opportunity" label="About" error={fieldError(errors, "opportunityId")}>
          <NativeSelect id="interaction-opportunity" value={opportunityId} onChange={(e) => setOpportunityId(e.target.value)}>
            <option value="">Nothing in particular</option>
            {opportunities.map((o) => (
              <option key={o.id} value={o.id}>
                {o.title}
              </option>
            ))}
          </NativeSelect>
        </Field>
      )}

      <Field id="interaction-summary" label="What was said" error={fieldError(errors, "summary")}>
        <Textarea id="interaction-summary" rows={3} maxLength={2000} value={summary} onChange={(e) => setSummary(e.target.value)} />
      </Field>

      <div className="flex flex-wrap gap-2">
        <Button type="submit" size="sm" disabled={log.isPending || !summary.trim() || !occurredAt}>
          {log.isPending ? "Logging…" : "Log it"}
        </Button>
        {onCancel && (
          <Button type="button" variant="ghost" size="sm" onClick={onCancel}>
            Cancel
          </Button>
        )}
      </div>
    </form>
  );
}
