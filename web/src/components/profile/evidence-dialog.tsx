"use client";

import { Field, ToggleChip } from "@/components/profile/field";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { NativeSelect } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";
import { describeApiError, fieldError, isConflict, NO_ERRORS, type FormErrors } from "@/lib/api/form-errors";
import { createEvidence, deleteEvidence, updateEvidence } from "@/lib/api/profile";
import { queryKeys } from "@/lib/query/query-keys";
import { EVIDENCE_KINDS, type Evidence, type EvidenceInput, type EvidenceKind, type Skill } from "@/lib/schemas/profile";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, X } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

export const KIND_LABELS: Record<EvidenceKind, string> = {
  project: "Project",
  role: "Role",
  certification: "Certification",
  education: "Education",
};

const EMPTY: EvidenceInput = {
  kind: "project",
  title: "",
  organization: null,
  startDate: null,
  endDate: null,
  description: "",
  outcome: null,
  links: [],
  skillIds: [],
};

/** Add or edit one piece of evidence. Mount it fresh for each use. */
export function EvidenceDialog({ item, skills, onClose }: { item: Evidence | null; skills: Skill[]; onClose: () => void }) {
  const queryClient = useQueryClient();
  const [form, setForm] = useState<EvidenceInput>(
    item
      ? {
          kind: item.kind,
          title: item.title,
          organization: item.organization,
          startDate: item.startDate,
          endDate: item.endDate,
          description: item.description,
          outcome: item.outcome,
          links: item.links,
          skillIds: item.skillIds,
        }
      : EMPTY,
  );
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [errors, setErrors] = useState<FormErrors>(NO_ERRORS);

  const done = (message: string) => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.evidence });
    void queryClient.invalidateQueries({ queryKey: queryKeys.skills }); // evidence counts
    toast.success(message);
    onClose();
  };
  const failed = (error: unknown) => {
    if (isConflict(error)) {
      toast.error(describeApiError(error).message);
      void queryClient.invalidateQueries({ queryKey: queryKeys.evidence });
      onClose();
    } else {
      setErrors(describeApiError(error));
    }
  };

  const save = useMutation({
    mutationFn: () => (item ? updateEvidence(item.id, { ...form, version: item.version }) : createEvidence(form)),
    onSuccess: () => done(item ? "Evidence updated" : "Evidence added"),
    onError: failed,
  });
  const remove = useMutation({
    mutationFn: () => deleteEvidence(item!.id),
    onSuccess: () => done("Evidence removed"),
    onError: failed,
  });

  const set = <K extends keyof EvidenceInput>(key: K, value: EvidenceInput[K]) => setForm((f) => ({ ...f, [key]: value }));
  const toggleSkill = (id: string) =>
    set("skillIds", form.skillIds.includes(id) ? form.skillIds.filter((s) => s !== id) : [...form.skillIds, id]);

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>{item ? "Edit evidence" : "Add evidence"}</DialogTitle>
          <DialogDescription>A project, role, certification or degree that backs up your skills.</DialogDescription>
        </DialogHeader>

        <form
          id="evidence-form"
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

          <div className="grid gap-4 sm:grid-cols-[10rem_1fr]">
            <Field id="evidence-kind" label="Kind" error={fieldError(errors, "kind")}>
              <NativeSelect id="evidence-kind" value={form.kind} onChange={(e) => set("kind", e.target.value as EvidenceKind)}>
                {EVIDENCE_KINDS.map((k) => (
                  <option key={k} value={k}>
                    {KIND_LABELS[k]}
                  </option>
                ))}
              </NativeSelect>
            </Field>
            <Field id="evidence-title" label="Title" error={fieldError(errors, "title")}>
              <Input
                id="evidence-title"
                maxLength={120}
                placeholder="Mail Template Hub"
                value={form.title}
                onChange={(e) => set("title", e.target.value)}
              />
            </Field>
          </div>

          <Field id="evidence-org" label="Organization" error={fieldError(errors, "organization")} hint="Employer, client, school, or Personal.">
            <Input
              id="evidence-org"
              maxLength={120}
              value={form.organization ?? ""}
              onChange={(e) => set("organization", e.target.value || null)}
            />
          </Field>

          <div className="grid grid-cols-2 gap-4">
            <Field id="evidence-start" label="Started" error={fieldError(errors, "startDate")}>
              <Input
                id="evidence-start"
                type="date"
                value={form.startDate ?? ""}
                onChange={(e) => set("startDate", e.target.value || null)}
              />
            </Field>
            <Field id="evidence-end" label="Ended" error={fieldError(errors, "endDate")} hint="Leave empty if ongoing.">
              <Input
                id="evidence-end"
                type="date"
                value={form.endDate ?? ""}
                onChange={(e) => set("endDate", e.target.value || null)}
              />
            </Field>
          </div>

          <Field id="evidence-description" label="What it was" error={fieldError(errors, "description")}>
            <Textarea
              id="evidence-description"
              rows={3}
              maxLength={4000}
              value={form.description}
              onChange={(e) => set("description", e.target.value)}
            />
          </Field>

          <Field
            id="evidence-outcome"
            label="What came of it"
            error={fieldError(errors, "outcome")}
            hint="Your own words. Real results only; drafts quote this and never add to it."
          >
            <Textarea
              id="evidence-outcome"
              rows={2}
              maxLength={1000}
              value={form.outcome ?? ""}
              onChange={(e) => set("outcome", e.target.value || null)}
            />
          </Field>

          <fieldset className="space-y-2">
            <legend className="text-sm font-medium">Links</legend>
            {form.links.map((link, index) => (
              <div key={index} className="flex gap-2">
                <Input
                  aria-label={`Link ${index + 1} label`}
                  className="w-32 shrink-0"
                  maxLength={60}
                  placeholder="Repository"
                  value={link.label}
                  onChange={(e) => set("links", form.links.map((l, i) => (i === index ? { ...l, label: e.target.value } : l)))}
                />
                <Input
                  aria-label={`Link ${index + 1} URL`}
                  type="url"
                  placeholder="https://"
                  value={link.url}
                  onChange={(e) => set("links", form.links.map((l, i) => (i === index ? { ...l, url: e.target.value } : l)))}
                />
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label={`Remove link ${index + 1}`}
                  onClick={() => set("links", form.links.filter((_, i) => i !== index))}
                >
                  <X />
                </Button>
              </div>
            ))}
            {fieldError(errors, "links") && <p className="text-sm text-destructive">{fieldError(errors, "links")}</p>}
            {form.links.length < 5 && (
              <Button type="button" variant="outline" size="sm" onClick={() => set("links", [...form.links, { label: "", url: "" }])}>
                <Plus />
                Add link
              </Button>
            )}
          </fieldset>

          <fieldset className="space-y-2">
            <legend className="text-sm font-medium">Skills it shows</legend>
            {skills.length === 0 ? (
              <p className="text-xs text-muted-foreground">Add skills first; then link them here.</p>
            ) : (
              <div className="flex flex-wrap gap-1.5">
                {skills.map((skill) => (
                  <ToggleChip key={skill.id} pressed={form.skillIds.includes(skill.id)} onToggle={() => toggleSkill(skill.id)}>
                    {skill.name}
                  </ToggleChip>
                ))}
              </div>
            )}
            {fieldError(errors, "skillIds") && <p className="text-sm text-destructive">{fieldError(errors, "skillIds")}</p>}
          </fieldset>
        </form>

        <DialogFooter className="gap-2 sm:justify-between">
          {item ? (
            <Button
              type="button"
              variant="ghost"
              className="text-destructive"
              disabled={remove.isPending}
              onClick={() => (confirmDelete ? remove.mutate() : setConfirmDelete(true))}
            >
              {confirmDelete ? "Click again to remove" : "Remove"}
            </Button>
          ) : (
            <span />
          )}
          <Button type="submit" form="evidence-form" disabled={save.isPending}>
            {save.isPending ? "Saving…" : item ? "Save" : "Add evidence"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
