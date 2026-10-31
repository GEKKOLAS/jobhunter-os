"use client";

import { logout, logoutEverywhere } from "@/lib/api/auth";
import { useMutation } from "@tanstack/react-query";

/**
 * Ends this session (or every session) and goes to sign-in with a full page load.
 *
 * Not a client-side navigation: clearing the cached user first would make the app
 * layout's guard redirect to `/login?next=<this page>` before this redirect runs, and a
 * full load also drops everything the app held in memory. It leaves even when the
 * request fails (an already-expired session, say), since the user asked to be out.
 */
export function useSignOut({ everywhere = false }: { everywhere?: boolean } = {}) {
  return useMutation({
    mutationFn: everywhere ? logoutEverywhere : logout,
    onSettled: () => window.location.replace("/login"),
  });
}
