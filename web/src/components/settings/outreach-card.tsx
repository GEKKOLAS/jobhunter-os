"use client";

import { Field } from "@/components/profile/field";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { getOutreach, removeOutreach, saveOutreach, testOutreach } from "@/lib/api/follow-ups";
import { describeApiError, fieldError, NO_ERRORS, type FormErrors } from "@/lib/api/form-errors";
import { queryKeys } from "@/lib/query/query-keys";
import type { OutreachSettings, OutreachTest } from "@/lib/schemas/follow-ups";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { CheckCircle2, CircleAlert } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

/** The link to Outreach Studio, which sends approved drafts. The key is write-only: only its last four show. */
export function OutreachCard() {
  const settings = useQuery({ queryKey: queryKeys.outreach, queryFn: getOutreach });
  if (!settings.data) return null;
  return <OutreachForm key={settings.data.baseUrl ?? "new"} settings={settings.data} />;
}

function OutreachForm({ settings }: { settings: OutreachSettings }) {
  const queryClient = useQueryClient();
  const [baseUrl, setBaseUrl] = useState(settings.baseUrl ?? "");
  const [apiKey, setApiKey] = useState("");
  const [template, setTemplate] = useState(settings.templateVersionId ?? "");
  const [errors, setErrors] = useState<FormErrors>(NO_ERRORS);
  const [result, setResult] = useState<OutreachTest | null>(null);

  const refresh = () => void queryClient.invalidateQueries({ queryKey: queryKeys.outreach });
  const save = useMutation({
    mutationFn: () => saveOutreach({ baseUrl, apiKey: apiKey.trim() || null, templateVersionId: template.trim() }),
    onSuccess: () => {
      setApiKey("");
      setResult(null);
      refresh();
      toast.success("Saved. Test it to be sure.");
    },
    onError: (error) => setErrors(describeApiError(error)),
  });
  const test = useMutation({
    mutationFn: testOutreach,
    onSuccess: (r) => {
      setResult(r);
      refresh();
    },
    onError: (error) => setErrors(describeApiError(error)),
  });
  const remove = useMutation({
    mutationFn: removeOutreach,
    onSuccess: () => {
      setResult(null);
      refresh();
      toast.success("Disconnected. Drafts are copied and marked sent by hand now.");
    },
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>Outreach Studio</CardTitle>
        <CardDescription>
          Sends the drafts you approve, from your default mailbox there. Without it, you copy a draft and mark it sent yourself.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form
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
          <Field id="outreach-url" label="Address" error={fieldError(errors, "baseUrl")}>
            <Input id="outreach-url" type="url" placeholder="https://" value={baseUrl} onChange={(e) => setBaseUrl(e.target.value)} />
          </Field>
          <Field
            id="outreach-key"
            label="API key"
            error={fieldError(errors, "apiKey")}
            hint={settings.keyHint ? `Saved, ending in ${settings.keyHint}. Leave blank to keep it.` : "From Outreach Studio's settings. It starts with mth_."}
          >
            <Input id="outreach-key" type="password" autoComplete="off" value={apiKey} onChange={(e) => setApiKey(e.target.value)} />
          </Field>
          <Field
            id="outreach-template"
            label="Template version"
            error={fieldError(errors, "templateVersionId")}
            hint="The id of a plain template version whose subject is {{subject}} and whose body shows {{body}}."
          >
            <Input id="outreach-template" autoComplete="off" value={template} onChange={(e) => setTemplate(e.target.value)} />
          </Field>

          {result && (
            <p role="status" className={`flex items-start gap-2 text-sm ${result.ok ? "" : "text-destructive"}`}>
              {result.ok ? <CheckCircle2 aria-hidden className="mt-0.5 size-4 shrink-0 text-success" /> : <CircleAlert aria-hidden className="mt-0.5 size-4 shrink-0" />}
              <span>
                {result.ok ? `Connected as ${result.displayName}.` : "Not connected."} {result.message}
              </span>
            </p>
          )}
          {!result && settings.lastTestedAt && (
            <p className="text-xs text-muted-foreground">
              Last tested {new Date(settings.lastTestedAt).toLocaleString()}: {settings.lastTestOk ? "worked" : "failed"}.
            </p>
          )}

          <div className="flex flex-wrap gap-2">
            <Button type="submit" disabled={save.isPending || !baseUrl.trim() || !template.trim()}>
              {save.isPending ? "Saving…" : "Save"}
            </Button>
            {settings.configured && (
              <>
                <Button type="button" variant="outline" disabled={test.isPending} onClick={() => test.mutate()}>
                  {test.isPending ? "Testing…" : "Test connection"}
                </Button>
                <Button type="button" variant="ghost" disabled={remove.isPending} onClick={() => remove.mutate()}>
                  Disconnect
                </Button>
              </>
            )}
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
