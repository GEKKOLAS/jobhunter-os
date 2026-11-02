"use client";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { NativeSelect } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";
import { describeApiError, fieldError, NO_ERRORS, type FormErrors } from "@/lib/api/form-errors";
import { extractRequirements, getRequirements, saveRequirements } from "@/lib/api/matching";
import { searchCatalog } from "@/lib/api/profile";
import { useDebounced } from "@/lib/hooks/use-debounced";
import { queryKeys } from "@/lib/query/query-keys";
import { type Requirement, type RequirementDraft, type RequirementKind, toDraft } from "@/lib/schemas/matching";
import type { Opportunity } from "@/lib/schemas/opportunities";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Sparkles, Trash2 } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

const KIND_LABELS: Record<RequirementKind, string> = { must: "Must have", nice: "Nice to have" };

const EXTRACTOR_LABELS = {
  rules: "Read by pattern rules, which miss nuance.",
  ai: "Read by Claude.",
  user: "",
};

/**
 * What the posting asks for. Anything read automatically is a draft until confirmed, and only
 * confirmed rows count toward the fit (spec 03, Matching).
 */
export function RequirementsCard({ opportunity }: { opportunity: Opportunity }) {
  const queryClient = useQueryClient();
  const requirements = useQuery({
    queryKey: queryKeys.requirements(opportunity.id),
    queryFn: () => getRequirements(opportunity.id),
  });
  const [editing, setEditing] = useState<RequirementDraft[] | null>(null);
  const [errors, setErrors] = useState<FormErrors>(NO_ERRORS);

  // The prefix covers the fit and the opportunity, whose suggested priority may follow. The board shows both.
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.opportunities });
    void queryClient.invalidateQueries({ queryKey: queryKeys.board });
  };

  const extract = useMutation({
    mutationFn: () => extractRequirements(opportunity.id),
    onSuccess: (result) => {
      setErrors(NO_ERRORS);
      refresh();
      if (result.fellBackToRules) toast.warning("Claude couldn't read this one, so the pattern rules did. Check the list closely.");
      else if (result.items.length === 0) toast.info("Nothing recognizable in the posting. You can list the requirements yourself.");
    },
    onError: (error) => setErrors(describeApiError(error)),
  });

  const save = useMutation({
    mutationFn: (items: RequirementDraft[]) => saveRequirements(opportunity.id, items),
    onSuccess: () => {
      setEditing(null);
      setErrors(NO_ERRORS);
      refresh();
      toast.success("Requirements confirmed");
    },
    onError: (error) => setErrors(describeApiError(error)),
  });

  const items = requirements.data?.items ?? [];
  const drafts = items.filter((r) => !r.confirmed);
  const extractor = requirements.data?.extractor ?? "rules";
  const readLabel = extract.isPending ? "Reading…" : items.length > 0 ? "Read the posting again" : "Read the posting";

  return (
    <Card>
      <CardHeader>
        <CardTitle>What the posting asks for</CardTitle>
        <CardDescription>
          {extractor === "ai" ? "Claude drafts this list" : "Pattern rules draft this list"}, and you check it. Only what you confirm counts
          toward the fit.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {requirements.isError && <p className="text-sm text-destructive">Could not load the requirements. Reload to try again.</p>}

        {errors.message && (
          <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {errors.message}
          </p>
        )}

        {editing ? (
          <RequirementsEditor
            rows={editing}
            errors={errors}
            pending={save.isPending}
            onChange={setEditing}
            onSave={() => {
              setErrors(NO_ERRORS);
              save.mutate(editing);
            }}
            onCancel={() => {
              setEditing(null);
              setErrors(NO_ERRORS);
            }}
          />
        ) : (
          <>
            {requirements.data && items.length === 0 && <p className="text-sm text-muted-foreground">Nothing listed yet.</p>}

            {drafts.length > 0 && (
              <div role="status" className="flex flex-wrap items-center justify-between gap-3 rounded-lg border-2 bg-warning-soft p-3 dark:border">
                <p className="min-w-0 flex-1 basis-56 text-sm">
                  {drafts.length === 1 ? "1 draft isn't" : `${drafts.length} drafts aren't`} counted yet.{" "}
                  {EXTRACTOR_LABELS[drafts[0].origin]} Check them, then confirm.
                </p>
                <Button size="sm" disabled={save.isPending} onClick={() => save.mutate(items.map(toDraft))}>
                  {save.isPending ? "Confirming…" : "Confirm the list"}
                </Button>
              </div>
            )}

            {items.length > 0 && (
              <ul className="divide-y">
                {items.map((requirement) => (
                  <RequirementRow key={requirement.id} requirement={requirement} />
                ))}
              </ul>
            )}

            <div className="flex flex-wrap gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={extract.isPending || !opportunity.description.trim()}
                title={opportunity.description.trim() ? undefined : "This opportunity has no posting text"}
                onClick={() => {
                  setErrors(NO_ERRORS);
                  extract.mutate();
                }}
              >
                <Sparkles />
                {readLabel}
              </Button>
              <Button variant="outline" size="sm" onClick={() => setEditing(items.length > 0 ? items.map(toDraft) : [blankRow()])}>
                {items.length > 0 ? "Edit the list" : "List them yourself"}
              </Button>
            </div>
          </>
        )}
      </CardContent>
    </Card>
  );
}

function RequirementRow({ requirement: r }: { requirement: Requirement }) {
  return (
    <li className="flex flex-wrap items-start justify-between gap-x-3 gap-y-1 py-2">
      <div className="min-w-0 flex-1 basis-56">
        <p className="text-sm font-medium break-words">{r.text}</p>
        <p className="text-xs text-muted-foreground">
          {[r.skillName ?? "No single skill: not scored", r.yearsMin !== null && `${r.yearsMin}+ years`].filter(Boolean).join(" · ")}
        </p>
      </div>
      <div className="flex shrink-0 flex-wrap items-center gap-1.5">
        <Badge variant={r.kind === "must" ? "secondary" : "outline"}>{KIND_LABELS[r.kind]}</Badge>
        {!r.confirmed && <Badge variant="warning">Draft</Badge>}
      </div>
    </li>
  );
}

const blankRow = (): RequirementDraft => ({ text: "", kind: "must", skill: "", yearsMin: null });

function RequirementsEditor({
  rows,
  errors,
  pending,
  onChange,
  onSave,
  onCancel,
}: {
  rows: RequirementDraft[];
  errors: FormErrors;
  pending: boolean;
  onChange: (rows: RequirementDraft[]) => void;
  onSave: () => void;
  onCancel: () => void;
}) {
  const [typing, setTyping] = useState("");
  const query = useDebounced(typing.trim());
  const suggestions = useQuery({
    queryKey: queryKeys.catalog(query),
    queryFn: () => searchCatalog(query),
    enabled: query.length > 0,
    staleTime: Infinity,
  });

  const update = (index: number, patch: Partial<RequirementDraft>) =>
    onChange(rows.map((row, i) => (i === index ? { ...row, ...patch } : row)));

  const listError = errors.fields["items"];

  return (
    <form
      className="space-y-4"
      noValidate
      onSubmit={(e) => {
        e.preventDefault();
        onSave();
      }}
    >
      {listError && (
        <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {listError}
        </p>
      )}

      <datalist id="requirement-skills">
        {suggestions.data?.map((s) => <option key={s.key} value={s.name} />)}
      </datalist>

      <ul className="space-y-4">
        {rows.map((row, index) => {
          const error = fieldError(errors, `items[${index}]`);
          return (
            <li key={index} className="space-y-2 rounded-lg border-2 p-3 dark:border">
              {/* Up to 300 characters: a one-line input would hide most of a long one. */}
              <Textarea
                aria-label={`Requirement ${index + 1}`}
                aria-invalid={Boolean(error)}
                rows={2}
                maxLength={300}
                placeholder="What the posting asks for"
                value={row.text}
                onChange={(e) => update(index, { text: e.target.value })}
              />
              <div className="grid grid-cols-[1fr_1fr_auto] gap-2 sm:grid-cols-[1fr_8rem_6rem_auto]">
                <Input
                  aria-label={`Skill for requirement ${index + 1}`}
                  className="col-span-3 sm:col-span-1"
                  list="requirement-skills"
                  autoComplete="off"
                  maxLength={60}
                  placeholder="Skill (blank: not scored)"
                  value={row.skill}
                  onFocus={(e) => setTyping(e.target.value)}
                  onChange={(e) => {
                    setTyping(e.target.value);
                    update(index, { skill: e.target.value });
                  }}
                />
                <NativeSelect
                  aria-label={`Kind of requirement ${index + 1}`}
                  value={row.kind}
                  onChange={(e) => update(index, { kind: e.target.value as RequirementKind })}
                >
                  <option value="must">{KIND_LABELS.must}</option>
                  <option value="nice">{KIND_LABELS.nice}</option>
                </NativeSelect>
                <Input
                  aria-label={`Years for requirement ${index + 1}`}
                  type="number"
                  min={0}
                  max={40}
                  placeholder="Years"
                  value={row.yearsMin ?? ""}
                  onChange={(e) => update(index, { yearsMin: e.target.value === "" ? null : Number(e.target.value) })}
                />
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label={`Remove requirement ${index + 1}`}
                  onClick={() => onChange(rows.filter((_, i) => i !== index))}
                >
                  <Trash2 />
                </Button>
              </div>
              {error && <p className="text-sm text-destructive">{error}</p>}
            </li>
          );
        })}
      </ul>

      <div className="flex flex-wrap gap-2">
        <Button type="button" variant="outline" size="sm" disabled={rows.length >= 50} onClick={() => onChange([...rows, blankRow()])}>
          <Plus />
          Add a requirement
        </Button>
        <Button type="submit" size="sm" disabled={pending}>
          {pending ? "Saving…" : "Save and confirm"}
        </Button>
        <Button type="button" variant="ghost" size="sm" onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </form>
  );
}
