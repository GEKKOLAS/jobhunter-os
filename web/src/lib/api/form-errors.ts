import { ApiError } from "@/lib/api/client";

export type FormErrors = { message: string | null; fields: Record<string, string> };

export const NO_ERRORS: FormErrors = { message: null, fields: {} };

/**
 * Splits an API failure into per-field messages (422 `errors`, first message each, keys
 * as the API sends them, e.g. `links[0].url`) and one message for the rest.
 */
export function describeApiError(error: unknown): FormErrors {
  if (!(error instanceof ApiError)) return { message: "Something went wrong. Please try again.", fields: {} };
  if (error.status === 429) return { message: "Too many attempts. Wait a moment and try again.", fields: {} };
  if (error.errorCode === "concurrency.conflict") {
    return { message: "This changed in another tab or device. It has been reloaded; make your edit again.", fields: {} };
  }

  const fields: Record<string, string> = {};
  for (const [field, messages] of Object.entries(error.fieldErrors ?? {})) {
    if (messages[0]) fields[field] = messages[0];
  }
  const hasFields = Object.keys(fields).length > 0;
  return { message: hasFields ? null : (error.message ?? "Request failed."), fields };
}

/** A stale `version`: the data changed elsewhere and must be reloaded. */
export function isConflict(error: unknown): boolean {
  return error instanceof ApiError && error.errorCode === "concurrency.conflict";
}

/** The first message for a field or any of its children (`links` matches `links[1].url`). */
export function fieldError(errors: FormErrors, field: string): string | undefined {
  return (
    errors.fields[field] ??
    Object.entries(errors.fields).find(([key]) => key.startsWith(`${field}[`) || key.startsWith(`${field}.`))?.[1]
  );
}
