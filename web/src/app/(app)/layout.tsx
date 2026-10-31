"use client";

import { AppNav } from "@/components/app/app-nav";
import { GradientBackdrop } from "@/components/app/gradient-backdrop";
import { MobileTabBar, MobileTopBar } from "@/components/app/mobile-shell";
import { ThemeToggle } from "@/components/theme/theme-toggle";
import { useSession } from "@/lib/hooks/use-session";
import { isActivePath, MODULES } from "@/lib/modules";
import { loginPathFor } from "@/lib/navigation";
import { usePathname, useRouter } from "next/navigation";
import { useEffect } from "react";

export default function AppLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const { data: user, isLoading } = useSession();

  // Signed out (or the session expired): sign in, then come back here.
  useEffect(() => {
    if (!isLoading && user === null) router.replace(loginPathFor(pathname));
  }, [isLoading, user, router, pathname]);

  if (isLoading || !user) {
    return (
      <div className="flex min-h-full flex-1 items-center justify-center">
        <p className="text-sm text-muted-foreground">Loading…</p>
      </div>
    );
  }

  const activeLabel = MODULES.find((m) => isActivePath(pathname, m.slug))?.label;
  const firstName = user.displayName.trim().split(/\s+/)[0] || "Your";

  return (
    <div className="relative flex min-h-full flex-1">
      <GradientBackdrop />
      <AppNav user={user} />
      <div className="flex min-w-0 flex-1 flex-col">
        <MobileTopBar user={user} />
        <header className="hidden h-14 shrink-0 items-center justify-between gap-4 border-b-2 bg-background/80 px-8 backdrop-blur-xl lg:flex dark:border-b">
          <nav aria-label="Breadcrumb" className="flex min-w-0 items-center gap-2 text-sm text-muted-foreground">
            <span className="truncate">{firstName}&apos;s search</span>
            {activeLabel && (
              <>
                <span aria-hidden>/</span>
                <span className="font-semibold text-foreground">{activeLabel}</span>
              </>
            )}
          </nav>
          <ThemeToggle />
        </header>
        {/* Bottom padding on phones clears the fixed tab bar. */}
        <main className="flex-1 overflow-y-auto px-4 pt-2 pb-28 lg:px-8 lg:py-8">{children}</main>
      </div>
      <MobileTabBar />
    </div>
  );
}
