"use client";

import { LogoMark } from "@/components/app/logo-mark";
import { UserAvatar } from "@/components/app/user-avatar";
import { ThemeToggle } from "@/components/theme/theme-toggle";
import { findModule, isActivePath } from "@/lib/modules";
import type { User } from "@/lib/schemas/auth";
import Link from "next/link";
import { usePathname } from "next/navigation";

// The phone shell (below `lg`): a slim top bar and four thumb-reachable tabs for what you
// do on the move: check today, move a card, answer a reminder, fix your profile.
// The dashboard lists every other area; the avatar opens Settings (and sign-out).
const TABS = ["dashboard", "board", "follow-ups", "profile"].map((slug) => findModule(slug)!);

export function MobileTopBar({ user }: { user: User }) {
  return (
    <header className="sticky top-0 z-30 flex items-center justify-between gap-3 bg-background/85 px-4 pt-[max(0.75rem,env(safe-area-inset-top))] pb-2 backdrop-blur-xl lg:hidden">
      <Link href="/dashboard">
        <LogoMark />
      </Link>
      <div className="flex items-center gap-2">
        <ThemeToggle className="size-9" />
        <Link href="/settings" aria-label={`Settings for ${user.displayName}`} className="rounded-full">
          <UserAvatar name={user.displayName} />
        </Link>
      </div>
    </header>
  );
}

export function MobileTabBar() {
  const pathname = usePathname();

  return (
    <nav
      aria-label="Primary"
      className="fixed inset-x-0 bottom-0 z-40 flex justify-around border-t-2 bg-card px-2 pt-2 pb-[max(0.625rem,env(safe-area-inset-bottom))] lg:hidden dark:border-t dark:bg-[#0e0e14]/95 dark:backdrop-blur-xl"
    >
      {TABS.map((tab) => {
        const active = isActivePath(pathname, tab.slug);
        const Icon = tab.icon;
        return (
          <Link
            key={tab.slug}
            href={`/${tab.slug}`}
            aria-current={active ? "page" : undefined}
            className={`flex w-20 flex-col items-center gap-1 text-[11px] font-semibold ${
              active ? "text-foreground" : "text-muted-foreground"
            }`}
          >
            <span
              className={`flex h-8 w-14 items-center justify-center rounded-full transition-colors ${
                active ? "bg-primary text-primary-foreground dark:bg-muted dark:text-link" : ""
              }`}
            >
              <Icon className="size-5" strokeWidth={2.25} />
            </span>
            {tab.label}
          </Link>
        );
      })}
    </nav>
  );
}
