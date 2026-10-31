import { ApiError } from "@/lib/api/client";
import type { UseFormSetError, FieldValues, Path } from "react-hook-form";

/**
 * Applies a backend 422 field-error map onto a react-hook-form instance, and
 * returns a human message for anything not tied to a specific field.
 */
export function applyApiError<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
): string | null {
  if (!(error instanceof ApiError)) {
    return "Something went wrong. Please try again.";
  }

  if (error.status === 429) {
    return "Too many attempts. Please wait a moment and try again.";
  }

  if (error.fieldErrors) {
    let matched = false;
    for (const [field, messages] of Object.entries(error.fieldErrors)) {
      if (messages.length > 0) {
        setError(field as Path<T>, { message: messages[0] });
        matched = true;
      }
    }
    if (matched) return null;
  }

  return error.message ?? "Request failed.";
}
