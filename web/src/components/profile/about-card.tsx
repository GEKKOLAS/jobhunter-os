"use client";

import { Field, ToggleChip } from "@/components/profile/field";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { NativeSelect } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";
import { saveProfile } from "@/lib/api/profile";
import { describeApiError, fieldError, isConflict, NO_ERRORS, type FormErrors } from "@/lib/api/form-errors";
import { addTag } from "@/lib/profile";
import { queryKeys } from "@/lib/query/query-keys";
import {
  LANGUAGE_LEVELS,
  type Language,
  type Profile,
  SENIORITIES,
  WORK_MODES,
} from "@/lib/schemas/profile";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, X } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

const MODE_LABELS = { remote: "Remote", hybrid: "Hybrid", onsite: "On-site" } as const;

/**
 * Who you are and what you're after. Seeded from the saved profile; the page remounts it
 * (keyed by version) after every save, so it never needs to sync state in an effect.
 */
export function AboutCard({ profile }: { profile: Profile }) {
  const queryClient = useQueryClient();
  const [form, setForm] = useState(profile);
  const [roleDraft, setRoleDraft] = useState("");
  const [errors, setErrors] = useState<FormErrors>(NO_ERRORS);

  const save = useMutation({
    mutationFn: () => saveProfile({ ...form, targetRoles: addTag(form.targetRoles, roleDraft) }),
    onSuccess: (saved) => {
      queryClient.setQueryData(queryKeys.profile, saved);
      toast.success("Profile saved");
    },
    onError: (error) => {
      const described = describeApiError(error);
      if (isConflict(error)) {
        // Reloading remounts this card, so say it where it survives the remount.
        toast.error(described.message);
        void queryClient.invalidateQueries({ queryKey: queryKeys.profile });
      } else {
        setErrors(described);
      }
    },
  });

  const set = <K extends keyof Profile>(key: K, value: Profile[K]) => setForm((f) => ({ ...f, [key]: value }));
  const setLanguage = (index: number, language: Language) =>
    set("languages", form.languages.map((l, i) => (i === index ? language : l)));

  return (
    <Card>
      <CardHeader>
        <CardTitle>About you</CardTitle>
        <CardDescription>What you&apos;re looking for. Matching uses your target roles, seniority and work modes.</CardDescription>
      </CardHeader>
      <CardContent>
        <form
          className="space-y-5"
          noValidate
          onSubmit={(e) => {
            e.preventDefault();
            setErrors(NO_ERRORS);
            setRoleDraft("");
            save.mutate();
          }}
        >
          {errors.message && (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {errors.message}
            </p>
          )}

          <Field id="headline" label="Headline" error={fieldError(errors, "headline")} hint="One line, like the top of a CV.">
            <Input
              id="headline"
              maxLength={120}
              placeholder="Backend engineer (.NET, PostgreSQL)"
              value={form.headline}
              onChange={(e) => set("headline", e.target.value)}
            />
          </Field>

          <div className="grid gap-5 sm:grid-cols-2">
            <Field id="location" label="Location" error={fieldError(errors, "location")}>
              <Input id="location" maxLength={120} value={form.location} onChange={(e) => set("location", e.target.value)} />
            </Field>
            <Field id="seniority" label="Seniority" error={fieldError(errors, "seniority")}>
              <NativeSelect
                id="seniority"
                value={form.seniority ?? ""}
                onChange={(e) => set("seniority", (e.target.value || null) as Profile["seniority"])}
              >
                <option value="">Not set</option>
                {SENIORITIES.map((s) => (
                  <option key={s} value={s}>
                    {s[0].toUpperCase() + s.slice(1)}
                  </option>
                ))}
              </NativeSelect>
            </Field>
          </div>

          <fieldset className="space-y-1.5">
            <legend className="text-sm font-medium">Work modes</legend>
            <div className="flex flex-wrap gap-2">
              {WORK_MODES.map((mode) => (
                <ToggleChip
                  key={mode}
                  pressed={form.workModes.includes(mode)}
                  onToggle={() =>
                    set("workModes", form.workModes.includes(mode) ? form.workModes.filter((m) => m !== mode) : [...form.workModes, mode])
                  }
                >
                  {MODE_LABELS[mode]}
                </ToggleChip>
              ))}
            </div>
          </fieldset>

          <Field id="role" label="Target roles" error={fieldError(errors, "targetRoles")} hint="Press Enter to add. Up to 10.">
            <div className="space-y-2">
              {form.targetRoles.length > 0 && (
                <ul className="flex flex-wrap gap-2">
                  {form.targetRoles.map((role) => (
                    <li key={role} className="flex items-center gap-1 rounded-full bg-muted py-1 pr-1 pl-3 text-sm">
                      {role}
                      <button
                        type="button"
                        aria-label={`Remove ${role}`}
                        onClick={() => set("targetRoles", form.targetRoles.filter((r) => r !== role))}
                        className="rounded-full p-0.5 text-muted-foreground hover:bg-background hover:text-foreground"
                      >
                        <X className="size-3.5" />
                      </button>
                    </li>
                  ))}
                </ul>
              )}
              <Input
                id="role"
                maxLength={80}
                placeholder="Backend Engineer"
                value={roleDraft}
                onChange={(e) => setRoleDraft(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    e.preventDefault();
                    set("targetRoles", addTag(form.targetRoles, roleDraft));
                    setRoleDraft("");
                  }
                }}
              />
            </div>
          </Field>

          <Field id="summary" label="Summary" error={fieldError(errors, "summary")} hint="In your own words. Drafts quote it; they never embellish it.">
            <Textarea id="summary" rows={4} maxLength={2000} value={form.summary} onChange={(e) => set("summary", e.target.value)} />
          </Field>

          <fieldset className="space-y-2">
            <legend className="text-sm font-medium">Languages</legend>
            {form.languages.map((language, index) => (
              <div key={index} className="flex gap-2">
                <Input
                  aria-label={`Language ${index + 1}`}
                  maxLength={40}
                  placeholder="English"
                  value={language.name}
                  onChange={(e) => setLanguage(index, { ...language, name: e.target.value })}
                />
                <NativeSelect
                  aria-label={`Level of language ${index + 1}`}
                  value={language.level}
                  onChange={(e) => setLanguage(index, { ...language, level: e.target.value as Language["level"] })}
                >
                  {LANGUAGE_LEVELS.map((level) => (
                    <option key={level} value={level}>
                      {level[0].toUpperCase() + level.slice(1)}
                    </option>
                  ))}
                </NativeSelect>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label={`Remove language ${index + 1}`}
                  onClick={() => set("languages", form.languages.filter((_, i) => i !== index))}
                >
                  <X />
                </Button>
              </div>
            ))}
            {fieldError(errors, "languages") && <p className="text-sm text-destructive">{fieldError(errors, "languages")}</p>}
            {form.languages.length < 10 && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => set("languages", [...form.languages, { name: "", level: "professional" }])}
              >
                <Plus />
                Add language
              </Button>
            )}
          </fieldset>

          <Button type="submit" disabled={save.isPending}>
            {save.isPending ? "Saving…" : "Save profile"}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}
