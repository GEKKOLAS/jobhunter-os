"use client";

import { Field } from "@/components/profile/field";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { createContact, updateContact } from "@/lib/api/contacts";
import { describeApiError, fieldError, isConflict, NO_ERRORS, type FormErrors } from "@/lib/api/form-errors";
import { listCompanies } from "@/lib/api/opportunities";
import { queryKeys } from "@/lib/query/query-keys";
import type { Contact, ContactInput } from "@/lib/schemas/contacts";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";

const orNull = (value: string) => (value.trim() === "" ? null : value);

function fromContact(c: Contact | undefined, initialName = ""): ContactInput {
  return {
    name: c?.name ?? initialName,
    companyName: c?.company?.name ?? null,
    role: c?.role ?? null,
    email: c?.email ?? null,
    linkedInUrl: c?.linkedInUrl ?? null,
    phone: c?.phone ?? null,
    notes: c?.notes ?? null,
  };
}

/** Add someone or edit them. Only the name is needed. Mount it fresh for each use. */
export function ContactDialog({
  contact,
  initialName,
  onClose,
  onSaved,
}: {
  contact?: Contact;
  /** What was typed in a search that found nobody. */
  initialName?: string;
  onClose: () => void;
  onSaved?: (saved: Contact) => void;
}) {
  const queryClient = useQueryClient();
  const [form, setForm] = useState<ContactInput>(fromContact(contact, initialName));
  const [errors, setErrors] = useState<FormErrors>(NO_ERRORS);
  const companies = useQuery({ queryKey: queryKeys.companies, queryFn: listCompanies });

  const save = useMutation({
    mutationFn: () => (contact ? updateContact(contact.id, { ...form, version: contact.version }) : createContact(form)),
    onSuccess: (saved) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.contacts });
      void queryClient.invalidateQueries({ queryKey: queryKeys.companies });
      toast.success(contact ? "Saved" : `${saved.name} added`);
      onSaved?.(saved);
      onClose();
    },
    onError: (error) => {
      if (isConflict(error) && contact) {
        toast.error(describeApiError(error).message);
        void queryClient.invalidateQueries({ queryKey: queryKeys.contact(contact.id) });
        onClose();
      } else {
        setErrors(describeApiError(error));
      }
    },
  });

  const set = <K extends keyof ContactInput>(key: K, value: ContactInput[K]) => setForm((f) => ({ ...f, [key]: value }));

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{contact ? `Edit ${contact.name}` : "New contact"}</DialogTitle>
          <DialogDescription>Only the name is needed. This stays private to you.</DialogDescription>
        </DialogHeader>

        <form
          id="contact-form"
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

          <Field id="contact-name" label="Name" error={fieldError(errors, "name")}>
            <Input id="contact-name" maxLength={120} autoComplete="off" value={form.name} onChange={(e) => set("name", e.target.value)} />
          </Field>

          <div className="grid gap-4 sm:grid-cols-2">
            <Field id="contact-company" label="Company" error={fieldError(errors, "companyName")} hint="Matched to your companies, or added.">
              <Input
                id="contact-company"
                list="contact-company-options"
                maxLength={120}
                autoComplete="off"
                value={form.companyName ?? ""}
                onChange={(e) => set("companyName", orNull(e.target.value))}
              />
              <datalist id="contact-company-options">
                {companies.data?.map((c) => <option key={c.id} value={c.name} />)}
              </datalist>
            </Field>
            <Field id="contact-role" label="Role" error={fieldError(errors, "role")}>
              <Input
                id="contact-role"
                maxLength={120}
                autoComplete="off"
                placeholder="Technical Recruiter"
                value={form.role ?? ""}
                onChange={(e) => set("role", orNull(e.target.value))}
              />
            </Field>
          </div>

          <Field id="contact-email" label="Email" error={fieldError(errors, "email")}>
            <Input
              id="contact-email"
              type="email"
              maxLength={254}
              autoComplete="off"
              value={form.email ?? ""}
              onChange={(e) => set("email", orNull(e.target.value))}
            />
          </Field>

          <div className="grid gap-4 sm:grid-cols-2">
            <Field id="contact-linkedin" label="LinkedIn" error={fieldError(errors, "linkedInUrl")}>
              <Input
                id="contact-linkedin"
                type="url"
                maxLength={300}
                autoComplete="off"
                placeholder="linkedin.com/in/…"
                value={form.linkedInUrl ?? ""}
                onChange={(e) => set("linkedInUrl", orNull(e.target.value))}
              />
            </Field>
            <Field id="contact-phone" label="Phone" error={fieldError(errors, "phone")}>
              <Input
                id="contact-phone"
                type="tel"
                maxLength={40}
                autoComplete="off"
                value={form.phone ?? ""}
                onChange={(e) => set("phone", orNull(e.target.value))}
              />
            </Field>
          </div>

          <Field id="contact-notes" label="Notes" error={fieldError(errors, "notes")}>
            <Textarea id="contact-notes" rows={3} maxLength={2000} value={form.notes ?? ""} onChange={(e) => set("notes", orNull(e.target.value))} />
          </Field>
        </form>

        <DialogFooter>
          <Button type="submit" form="contact-form" disabled={save.isPending}>
            {save.isPending ? "Saving…" : contact ? "Save" : "Add contact"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
