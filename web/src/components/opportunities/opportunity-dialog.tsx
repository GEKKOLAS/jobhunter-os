"use client";

import { Field, ToggleChip } from "@/components/profile/field";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { NativeSelect } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";
import { describeApiError, fieldError, isConflict, NO_ERRORS, type FormErrors } from "@/lib/api/form-errors";
import { createOpportunity, listCompanies, updateOpportunity } from "@/lib/api/opportunities";
import { EMPLOYMENT_LABELS, PRIORITY_LABELS, STAGE_LABELS, WORK_MODE_LABELS } from "@/lib/opportunities";
import { queryKeys } from "@/lib/query/query-keys";
import {
  EMPLOYMENT_TYPES,
  OPEN_STAGES,
  type OpenStage,
  type Opportunity,
  type OpportunityInput,
  PRIORITIES,
  SALARY_PERIODS,
  WORK_MODES,
} from "@/lib/schemas/opportunities";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";

const EMPTY: OpportunityInput = {
  title: "",
  companyName: "",
  location: null,
  workMode: null,
  employmentType: null,
  salaryMin: null,
  salaryMax: null,
  salaryCurrency: null,
  salaryPeriod: null,
  sourceUrl: null,
  description: "",
  priority: "normal",
  stage: "saved",
};

function fromOpportunity(o: Opportunity): OpportunityInput {
  return {
    title: o.title,
    companyName: o.company.name,
    location: o.location,
    workMode: o.workMode,
    employmentType: o.employmentType,
    salaryMin: o.salaryMin,
    salaryMax: o.salaryMax,
    salaryCurrency: o.salaryCurrency,
    salaryPeriod: o.salaryPeriod,
    sourceUrl: o.sourceUrl,
    description: o.description,
    priority: o.priority,
  };
}

const orNull = (value: string) => (value.trim() === "" ? null : value);
const numberOrNull = (value: string) => (value === "" ? null : Number(value));

/**
 * Capture a job by hand, or edit one. The company is typed by name: an existing one is
 * matched (ignoring case) and a new one is created. Mount it fresh for each use.
 */
export function OpportunityDialog({
  opportunity,
  initialStage,
  onClose,
  onSaved,
}: {
  opportunity?: Opportunity;
  initialStage?: OpenStage;
  onClose: () => void;
  onSaved?: (saved: Opportunity) => void;
}) {
  const queryClient = useQueryClient();
  const [form, setForm] = useState<OpportunityInput>(
    opportunity ? fromOpportunity(opportunity) : { ...EMPTY, stage: initialStage ?? "saved" },
  );
  const [errors, setErrors] = useState<FormErrors>(NO_ERRORS);
  const companies = useQuery({ queryKey: queryKeys.companies, queryFn: listCompanies });

  const save = useMutation({
    mutationFn: () =>
      opportunity
        ? updateOpportunity(opportunity.id, { ...form, version: opportunity.version })
        : createOpportunity(form),
    onSuccess: (saved) => {
      queryClient.setQueryData(queryKeys.opportunity(saved.id), saved);
      void queryClient.invalidateQueries({ queryKey: queryKeys.board });
      void queryClient.invalidateQueries({ queryKey: queryKeys.opportunities });
      void queryClient.invalidateQueries({ queryKey: queryKeys.companies });
      toast.success(opportunity ? "Saved" : `${saved.title} at ${saved.company.name} added`);
      onSaved?.(saved);
      onClose();
    },
    onError: (error) => {
      if (isConflict(error) && opportunity) {
        toast.error(describeApiError(error).message);
        void queryClient.invalidateQueries({ queryKey: queryKeys.opportunity(opportunity.id) });
        onClose();
      } else {
        setErrors(describeApiError(error));
      }
    },
  });

  const set = <K extends keyof OpportunityInput>(key: K, value: OpportunityInput[K]) =>
    setForm((f) => ({ ...f, [key]: value }));

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>{opportunity ? "Edit opportunity" : "New opportunity"}</DialogTitle>
          <DialogDescription>
            {opportunity ? "Stage changes happen from the board or the Move menu." : "Paste what the posting says. Only what it states."}
          </DialogDescription>
        </DialogHeader>

        <form
          id="opportunity-form"
          className="space-y-4"
          noValidate
          onSubmit={(e) => {
            e.preventDefault();
            setErrors(NO_ERRORS);
            save.mutate();
          }}
        >
          {errors.message && (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {errors.message}
            </p>
          )}

          <Field id="opp-title" label="Role" error={fieldError(errors, "title")}>
            <Input id="opp-title" maxLength={160} placeholder="Backend Engineer" value={form.title} onChange={(e) => set("title", e.target.value)} />
          </Field>

          <div className="grid gap-4 sm:grid-cols-2">
            <Field id="opp-company" label="Company" error={fieldError(errors, "companyName")} hint="Matched to your companies, or added.">
              <Input
                id="opp-company"
                list="opp-company-options"
                maxLength={120}
                autoComplete="off"
                value={form.companyName}
                onChange={(e) => set("companyName", e.target.value)}
              />
              <datalist id="opp-company-options">
                {companies.data?.map((c) => <option key={c.id} value={c.name} />)}
              </datalist>
            </Field>
            <Field id="opp-location" label="Location" error={fieldError(errors, "location")}>
              <Input
                id="opp-location"
                maxLength={120}
                placeholder="Remote, LATAM"
                value={form.location ?? ""}
                onChange={(e) => set("location", orNull(e.target.value))}
              />
            </Field>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <Field id="opp-work-mode" label="Work mode" error={fieldError(errors, "workMode")}>
              <NativeSelect
                id="opp-work-mode"
                value={form.workMode ?? ""}
                onChange={(e) => set("workMode", (e.target.value || null) as OpportunityInput["workMode"])}
              >
                <option value="">Not stated</option>
                {WORK_MODES.map((m) => (
                  <option key={m} value={m}>
                    {WORK_MODE_LABELS[m]}
                  </option>
                ))}
              </NativeSelect>
            </Field>
            <Field id="opp-employment" label="Type" error={fieldError(errors, "employmentType")}>
              <NativeSelect
                id="opp-employment"
                value={form.employmentType ?? ""}
                onChange={(e) => set("employmentType", (e.target.value || null) as OpportunityInput["employmentType"])}
              >
                <option value="">Not stated</option>
                {EMPLOYMENT_TYPES.map((t) => (
                  <option key={t} value={t}>
                    {EMPLOYMENT_LABELS[t]}
                  </option>
                ))}
              </NativeSelect>
            </Field>
          </div>

          <fieldset className="space-y-1.5">
            <legend className="text-sm font-medium">Salary, if the posting states one</legend>
            <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
              <Input
                aria-label="Minimum"
                type="number"
                min={0}
                placeholder="Min"
                value={form.salaryMin ?? ""}
                onChange={(e) => set("salaryMin", numberOrNull(e.target.value))}
              />
              <Input
                aria-label="Maximum"
                type="number"
                min={0}
                placeholder="Max"
                value={form.salaryMax ?? ""}
                onChange={(e) => set("salaryMax", numberOrNull(e.target.value))}
              />
              <Input
                aria-label="Currency"
                maxLength={3}
                placeholder="USD"
                value={form.salaryCurrency ?? ""}
                onChange={(e) => set("salaryCurrency", orNull(e.target.value.toUpperCase()))}
              />
              <NativeSelect
                aria-label="Per"
                value={form.salaryPeriod ?? ""}
                onChange={(e) => set("salaryPeriod", (e.target.value || null) as OpportunityInput["salaryPeriod"])}
              >
                <option value="">Per…</option>
                {SALARY_PERIODS.map((p) => (
                  <option key={p} value={p}>
                    per {p}
                  </option>
                ))}
              </NativeSelect>
            </div>
            {(["salaryMin", "salaryMax", "salaryCurrency", "salaryPeriod"] as const).map(
              (f) => fieldError(errors, f) && <p key={f} className="text-sm text-destructive">{fieldError(errors, f)}</p>,
            )}
          </fieldset>

          <Field id="opp-url" label="Posting link" error={fieldError(errors, "sourceUrl")}>
            <Input
              id="opp-url"
              type="url"
              placeholder="https://"
              value={form.sourceUrl ?? ""}
              onChange={(e) => set("sourceUrl", orNull(e.target.value))}
            />
          </Field>

          <fieldset className="space-y-1.5">
            <legend className="text-sm font-medium">Priority</legend>
            <div className="flex gap-1.5">
              {PRIORITIES.map((p) => (
                <ToggleChip key={p} pressed={form.priority === p} onToggle={() => set("priority", p)}>
                  {PRIORITY_LABELS[p]}
                </ToggleChip>
              ))}
            </div>
          </fieldset>

          {!opportunity && (
            <Field id="opp-stage" label="Starts at" error={fieldError(errors, "stage")} hint="Already applied? Start it there.">
              <NativeSelect id="opp-stage" value={form.stage} onChange={(e) => set("stage", e.target.value as OpenStage)}>
                {OPEN_STAGES.map((s) => (
                  <option key={s} value={s}>
                    {STAGE_LABELS[s]}
                  </option>
                ))}
              </NativeSelect>
            </Field>
          )}

          <Field id="opp-description" label="Posting text" error={fieldError(errors, "description")} hint="Kept as plain text. Matching reads it in phase 5.">
            <Textarea
              id="opp-description"
              rows={6}
              maxLength={50000}
              value={form.description}
              onChange={(e) => set("description", e.target.value)}
            />
          </Field>
        </form>

        <DialogFooter>
          <Button type="submit" form="opportunity-form" disabled={save.isPending}>
            {save.isPending ? "Saving…" : opportunity ? "Save" : "Add opportunity"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
