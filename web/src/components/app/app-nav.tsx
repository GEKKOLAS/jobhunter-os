"use client";

import { ApiStatus } from "@/components/app/api-status";
import { LogoMark } from "@/components/app/logo-mark";
import { isActivePath, MODULE_GROUPS, MODULES } from "@/lib/modules";
import Link from "next/link";
import { usePathname } from "next/navigation";

/** The desktop sidebar (`lg` and up), built from the module catalogue. */
export function AppNav() {
  const pathname = usePathname();

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
                </Link>
              );
            })}
          </div>
        ))}
      </nav>

      <div className="flex flex-col gap-1 border-t border-sidebar-border px-3 pt-3">
        <ApiStatus />
        <p className="text-xs text-sidebar-foreground/55">Phase 0 · sign-in arrives in phase 1</p>
      </div>
    </aside>
  );
}
