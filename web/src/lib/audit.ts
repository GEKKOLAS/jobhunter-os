/** Readable labels for audit action codes (src/JobHunter.Application/Identity/AuthModels.cs). */
const LABELS: Record<string, string> = {
  "auth.register": "Account created",
  "auth.login": "Signed in",
  "auth.login_failed": "Failed sign-in attempt",
  "auth.logout": "Signed out",
  "auth.password_reset": "Password reset by email link",
  "auth.password_changed": "Password changed",
  "auth.session_revoked": "Session revoked",
  "user.updated": "Profile updated",
};

/** The label for a code; unknown codes (from later phases) fall back to a tidied code. */
export function auditLabel(action: string): string {
  return LABELS[action] ?? action.replace(/[._]/g, " ").replace(/^\w/, (c) => c.toUpperCase());
}

/** Actions worth drawing the user's eye to. */
export function isSecurityWarning(action: string): boolean {
  return action === "auth.login_failed";
}
