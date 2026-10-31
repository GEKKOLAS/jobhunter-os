"use client";

import { Field, ToggleChip } from "@/components/profile/field";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { NativeSelect } from "@/components/ui/native-select";
import { describeApiError, fieldError, isConflict, NO_ERRORS, type FormErrors } from "@/lib/api/form-errors";
import { createSkill, deleteSkill, searchCatalog, updateSkill } from "@/lib/api/profile";
import { useDebounced } from "@/lib/hooks/use-debounced";
import { CATEGORY_LABELS, LEVEL_LABELS } from "@/lib/profile";
import { queryKeys } from "@/lib/query/query-keys";
import { SKILL_CATEGORIES, type Skill, type SkillCategory, type SkillInput } from "@/lib/schemas/profile";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";

const EMPTY: SkillInput = { name: "", category: null, level: 3, years: 1, lastUsedYear: new Date().getFullYear() };

/** Add a skill (with catalog suggestions) or edit one. Mount it fresh for each use. */
export function SkillDialog({ skill, onClose }: { skill: Skill | null; onClose: () => void }) {
  const queryClient = useQueryClient();
  const [form, setForm] = useState<SkillInput>(
    skill
      ? { name: skill.name, category: skill.category, level: skill.level, years: skill.years, lastUsedYear: skill.lastUsedYear }
      : EMPTY,
  );
  const [picked, setPicked] = useState(skill !== null);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [errors, setErrors] = useState<FormErrors>(NO_ERRORS);

  const query = useDebounced(form.name.trim());
  const suggestions = useQuery({
    queryKey: queryKeys.catalog(query),
    queryFn: () => searchCatalog(query),
    enabled: !picked && query.length > 0,
    staleTime: Infinity,
  });

  const done = (message: string) => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.skills });
    void queryClient.invalidateQueries({ queryKey: queryKeys.evidence });
    toast.success(message);
    onClose();
  };
  const failed = (error: unknown) => {
    if (isConflict(error)) {
      toast.error(describeApiError(error).message);
      void queryClient.invalidateQueries({ queryKey: queryKeys.skills });
      onClose();
    } else {
      setErrors(describeApiError(error));
    }
  };

  const save = useMutation({
    mutationFn: () => (skill ? updateSkill(skill.id, { ...form, version: skill.version }) : createSkill(form)),
    onSuccess: (saved) => done(skill ? `${saved.name} updated` : `${saved.name} added`),
    onError: failed,
  });
  const remove = useMutation({
    mutationFn: () => deleteSkill(skill!.id),
    onSuccess: () => done(`${skill!.name} removed`),
    onError: failed,
  });

  const set = <K extends keyof SkillInput>(key: K, value: SkillInput[K]) => setForm((f) => ({ ...f, [key]: value }));
  const visibleSuggestions = (suggestions.data ?? []).filter((s) => s.name.toLowerCase() !== form.name.trim().toLowerCase());

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{skill ? `Edit ${skill.name}` : "Add a skill"}</DialogTitle>
          <DialogDescription>Rate it honestly. Level 3 means you can work with it without help.</DialogDescription>
        </DialogHeader>

        <form
          id="skill-form"
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

          <Field id="skill-name" label="Skill" error={fieldError(errors, "name")}>
            <Input
              id="skill-name"
              autoComplete="off"
              maxLength={60}
              placeholder="C#, PostgreSQL, Kubernetes…"
              value={form.name}
              onChange={(e) => {
                setPicked(false);
                set("name", e.target.value);
              }}
            />
          </Field>
          {visibleSuggestions.length > 0 && (
            <ul aria-label="Known skills" className="-mt-2 flex flex-wrap gap-1.5">
              {visibleSuggestions.map((s) => (
                <li key={s.key}>
                  <button
                    type="button"
                    onClick={() => {
                      setForm((f) => ({ ...f, name: s.name, category: s.category }));
                      setPicked(true);
                    }}
                    className="rounded-md border px-2 py-1 text-xs hover:bg-accent"
                  >
                    {s.name} <span className="text-muted-foreground">· {CATEGORY_LABELS[s.category]}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}

          <Field
            id="skill-category"
            label="Category"
            error={fieldError(errors, "category")}
            hint="Filled in for skills the catalog knows."
          >
            <NativeSelect
              id="skill-category"
              value={form.category ?? ""}
              onChange={(e) => set("category", (e.target.value || null) as SkillCategory | null)}
            >
              <option value="">From the catalog</option>
              {SKILL_CATEGORIES.map((c) => (
                <option key={c} value={c}>
                  {CATEGORY_LABELS[c]}
                </option>
              ))}
            </NativeSelect>
          </Field>

          <fieldset className="space-y-1.5">
            <legend className="text-sm font-medium">Level</legend>
            <div className="flex flex-wrap gap-1.5">
              {[1, 2, 3, 4, 5].map((level) => (
                <ToggleChip key={level} pressed={form.level === level} onToggle={() => set("level", level)}>
                  {level} · {LEVEL_LABELS[level]}
                </ToggleChip>
              ))}
            </div>
            {fieldError(errors, "level") && <p className="text-sm text-destructive">{fieldError(errors, "level")}</p>}
          </fieldset>

          <div className="grid grid-cols-2 gap-4">
            <Field id="skill-years" label="Years of use" error={fieldError(errors, "years")}>
              <Input
                id="skill-years"
                type="number"
                min={0}
                max={40}
                step={0.5}
                value={form.years}
                onChange={(e) => set("years", Number(e.target.value))}
              />
            </Field>
            <Field id="skill-last-used" label="Last used" error={fieldError(errors, "lastUsedYear")}>
              <Input
                id="skill-last-used"
                type="number"
                min={1970}
                max={new Date().getFullYear()}
                value={form.lastUsedYear ?? ""}
                onChange={(e) => set("lastUsedYear", e.target.value ? Number(e.target.value) : null)}
              />
            </Field>
          </div>
        </form>

        <DialogFooter className="gap-2 sm:justify-between">
          {skill ? (
            <Button
              type="button"
              variant="ghost"
              className="text-destructive"
              disabled={remove.isPending}
              onClick={() => (confirmDelete ? remove.mutate() : setConfirmDelete(true))}
            >
              {confirmDelete ? `Remove ${skill.name} and unlink its evidence?` : "Remove"}
            </Button>
          ) : (
            <span />
          )}
          <Button type="submit" form="skill-form" disabled={save.isPending}>
            {save.isPending ? "Saving…" : skill ? "Save" : "Add skill"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
