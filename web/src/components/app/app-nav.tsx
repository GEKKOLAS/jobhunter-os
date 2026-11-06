"use client";

import { ApiStatus } from "@/components/app/api-status";
import { LogoMark } from "@/components/app/logo-mark";
import { UserAvatar } from "@/components/app/user-avatar";
import { useSignOut } from "@/lib/hooks/use-sign-out";
import { isActivePath, MODULE_GROUPS, MODULES } from "@/lib/modules";
import type { User } from "@/lib/schemas/auth";
import { LogOut } from "lucide-react";
import { UnreadBadge } from "@/components/follow-ups/unread-badge";
import Link from "next/link";
import { usePathname } from "next/navigation";

/** The desktop sidebar (`lg` and up), built from the module catalogue. */
export function AppNav({ user }: { user: User }) {
  const pathname = usePathname();
  const signOut = useSignOut();

  return (
    <aside className="hidden w-64 shrink-0 flex-col gap-5 border-r border-sidebar-border bg-sidebar px-3 py-4 text-sidebar-foreground lg:flex">
      <Link href="/dashboard" className="rounded-xl px-2 py-1.5">
        <LogoMark />
      </Link>

      <nav className="flex flex-1 flex-col gap-5" aria-label="Primary">
        {MODULE_GROUPS.map((group) => (
          <div key={group} className="flex flex-col gap-0.5">
            <p className="mb-1 px-3 font-mono text-[11px] font-bold tracking-[0.06em] text-sidebar-foreground/55 uppercase dark:font-sans dark:font-medium">
              {group}
            </p>
            {MODULES.filter((m) => m.group === group).map((m) => {
              const active = isActivePath(pathname, m.slug);
              const Icon = m.icon;
              return (
                <Link
                  key={m.slug}
                  href={`/${m.slug}`}
                  aria-current={active ? "page" : undefined}
                  className={`flex h-10 items-center gap-2.5 rounded-lg px-3 text-sm transition-colors dark:h-9 ${
                    active
                      ? "bg-sidebar-active font-bold text-sidebar-active-foreground dark:font-semibold"
                      : "font-medium text-sidebar-foreground/65 hover:bg-sidebar-accent hover:text-sidebar-foreground"
                  }`}
                >
                  <Icon className={`size-4 shrink-0 ${active ? "text-sidebar-icon" : ""}`} strokeWidth={2.25} />
                  {m.label}
                  {m.slug === "follow-ups" && <UnreadBadge className="ml-auto" />}
                </Link>
              );
            })}
          </div>
        ))}
      </nav>

      <ApiStatus className="px-3 text-sidebar-foreground/65" />

      <div className="flex items-center gap-2.5 border-t border-sidebar-border px-1 pt-3">
        <UserAvatar name={user.displayName} />
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-semibold">{user.displayName}</p>
          <p className="truncate text-xs text-sidebar-foreground/60">{user.email}</p>
        </div>
        <button
          type="button"
          aria-label="Sign out"
          title="Sign out"
          onClick={() => signOut.mutate()}
          disabled={signOut.isPending}
          className="flex size-8 shrink-0 items-center justify-center rounded-lg text-sidebar-foreground/60 transition-colors hover:bg-sidebar-accent hover:text-sidebar-foreground disabled:opacity-50"
        >
          <LogOut className="size-4" />
        </button>
      </div>
    </aside>
  );
}
