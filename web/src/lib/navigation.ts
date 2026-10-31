/** Where a signed-in user lands by default. */
export const HOME_PATH = "/dashboard";

/**
 * The `?next=` target after signing in, accepted only when it is a path on this site.
 * `//evil.example` and `/\evil.example` start with "/" but browsers treat them as
 * other hosts, so a plain `startsWith("/")` check would be an open redirect.
 */
export function safeNext(next: string | null | undefined): string {
  if (!next || !next.startsWith("/") || next.startsWith("//") || next.startsWith("/\\")) return HOME_PATH;
  // Reject control characters, which some browsers strip before resolving the URL.
  if (/[\u0000-\u001f\u007f]/.test(next)) return HOME_PATH;
  // Coming back to an auth page after signing in would loop.
  if (/^\/(login|register|reset-password)(\/|\?|$)/.test(next)) return HOME_PATH;
  return next;
}

/** The sign-in URL that returns to `pathname` afterwards. */
export function loginPathFor(pathname: string): string {
  const next = safeNext(pathname);
  return next === HOME_PATH ? "/login" : `/login?next=${encodeURIComponent(next)}`;
}
