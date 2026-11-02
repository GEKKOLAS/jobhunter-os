"use client";

import { OpportunityDialog } from "@/components/opportunities/opportunity-dialog";
import { Field, ToggleChip } from "@/components/profile/field";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { describeApiError, fieldError, NO_ERRORS, type FormErrors } from "@/lib/api/form-errors";
import { importFromText, importFromUrl } from "@/lib/api/imports";
import { PARSED_FROM_NOTES, type PostingPreview, previewToInput } from "@/lib/schemas/imports";
import { useMutation } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";

/** Nothing is saved here: an import opens the capture dialog, filled in, for the user to check. */
export function ImportCard() {
  const router = useRouter();
  const [mode, setMode] = useState<"link" | "paste">("link");
  const [url, setUrl] = useState("");
  const [text, setText] = useState("");
  const [errors, setErrors] = useState<FormErrors>(NO_ERRORS);
  const [preview, setPreview] = useState<PostingPreview | null>(null);

  const read = useMutation({
    mutationFn: () => (mode === "link" ? importFromUrl(url) : importFromText(text)),
    onSuccess: setPreview,
    onError: (error) => setErrors(describeApiError(error)),
  });

  const pick = (next: "link" | "paste") => {
    setMode(next);
    setErrors(NO_ERRORS);
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Import one posting</CardTitle>
        <CardDescription>You check what was read before anything is saved.</CardDescription>
      </CardHeader>
      <CardContent>
        <form
          className="space-y-4"
          noValidate
          onSubmit={(e) => {
            e.preventDefault();
            setErrors(NO_ERRORS);
            read.mutate();
          }}
        >
          <div className="flex gap-1.5" role="group" aria-label="Import from">
            <ToggleChip pressed={mode === "link"} onToggle={() => pick("link")}>
              From a link
            </ToggleChip>
            <ToggleChip pressed={mode === "paste"} onToggle={() => pick("paste")}>
              Paste the text
            </ToggleChip>
          </div>

          {errors.message && (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {errors.message}
              {errors.message.includes("paste") && mode === "link" && (
                <>
                  {" "}
                  <button type="button" className="font-semibold underline" onClick={() => pick("paste")}>
                    Paste it now
                  </button>
                </>
              )}
            </p>
          )}

          {mode === "link" ? (
            <Field
              id="import-url"
              label="Posting link"
              error={fieldError(errors, "url")}
              hint="Links to Greenhouse, Lever and Ashby jobs read the company's public job board. Other pages are read once, like opening them."
            >
              <Input
                id="import-url"
                type="url"
                placeholder="https://"
                value={url}
                onChange={(e) => setUrl(e.target.value)}
              />
            </Field>
          ) : (
            <Field id="import-text" label="Posting text" error={fieldError(errors, "text")} hint="The whole posting: role, company, requirements.">
              <Textarea id="import-text" rows={8} maxLength={50000} value={text} onChange={(e) => setText(e.target.value)} />
            </Field>
          )}

          <Button type="submit" disabled={read.isPending || (mode === "link" ? !url.trim() : !text.trim())}>
            {read.isPending ? "Reading…" : "Read posting"}
          </Button>
        </form>
      </CardContent>

      {preview && (
        <OpportunityDialog
          key={preview.sourceRef ?? preview.description}
          draft={previewToInput(preview)}
          saveLabel={preview.existingOpportunityId ? "Update it" : undefined}
          note={<PreviewNote preview={preview} />}
          onClose={() => setPreview(null)}
          onSaved={(saved) => {
            setUrl("");
            setText("");
            router.push(`/opportunities/${saved.id}`);
          }}
        />
      )}
    </Card>
  );
}

/** Where the draft came from, how sure it is, and whether it's already saved. */
function PreviewNote({ preview }: { preview: PostingPreview }) {
  return (
    <span className="flex flex-col gap-2">
      <span>{PARSED_FROM_NOTES[preview.parsedFrom]}</span>
      {preview.warnings.map((warning) => (
        <span key={warning} className="text-warning">
          {warning}
        </span>
      ))}
      {preview.existingOpportunityId && (
        <span role="status" className="text-info">
          You already saved this one (
          <Link href={`/opportunities/${preview.existingOpportunityId}`} className="font-semibold underline">
            open it
          </Link>
          ). Saving again updates the posting and keeps its stage and priority.
        </span>
      )}
    </span>
  );
}
