import { GradientBackdrop } from "@/components/app/gradient-backdrop";
import { LogoMark } from "@/components/app/logo-mark";
import { ThemeToggle } from "@/components/theme/theme-toggle";

// A sketch of the pipeline board: what you'll find after signing in. Decorative only;
// the companies and roles are made up.
const COLUMNS = [
  { title: "Saved", cards: [{ role: "Backend Engineer", company: "Northwind", fit: 82 }, { role: "Platform Engineer", company: "Contoso", fit: 64 }] },
  { title: "Applied", cards: [{ role: ".NET Developer", company: "Fabrikam", fit: 77 }] },
  { title: "Interviewing", cards: [{ role: "Full-Stack Engineer", company: "Tailspin", fit: 71 }] },
];

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="relative flex min-h-full flex-1">
      <GradientBackdrop />

      <section
        aria-label="About JobHunter OS"
        className="relative hidden flex-1 flex-col justify-between overflow-hidden bg-sidebar p-12 text-sidebar-foreground lg:flex"
      >
        <LogoMark />

        <div aria-hidden className="grid max-w-xl grid-cols-3 gap-3">
          {COLUMNS.map((column, i) => (
            <div key={column.title} className={i === 1 ? "translate-y-6" : ""}>
              <p className="mb-2 font-mono text-[11px] font-bold tracking-[0.06em] text-sidebar-foreground/55 uppercase dark:font-sans dark:font-medium">
                {column.title}
              </p>
              <div className="flex flex-col gap-2">
                {column.cards.map((card) => (
                  <div
                    key={card.role}
                    className="rounded-lg border-2 border-white bg-[#16161d] p-3 shadow-[5px_5px_0_0_var(--primary)] dark:border dark:border-white/10 dark:bg-white/[0.04] dark:shadow-none"
                  >
                    <p className="text-[13px] leading-tight font-semibold text-white">{card.role}</p>
                    <p className="mt-0.5 text-[11px] text-white/55">{card.company}</p>
                    <p className="mt-2 inline-block rounded-sm bg-primary px-1.5 py-0.5 font-mono text-[10px] font-bold text-primary-foreground dark:bg-primary/15 dark:text-primary">
                      fit {card.fit}
                    </p>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>

        <div className="max-w-xl">
          <h2 className="font-heading text-5xl leading-[1.02] font-bold tracking-[-0.045em] xl:text-6xl dark:text-5xl dark:font-semibold dark:tracking-[-0.035em]">
            Fewer applications.
            <br />
            <span className="text-sidebar-primary">Better ones.</span>
          </h2>
          <p className="mt-4 text-[15px] leading-relaxed text-sidebar-foreground/65">
            Keep every opportunity, recruiter and follow-up in one place, see how each posting fits your real
            experience, and know what is working.
          </p>
        </div>
      </section>

      <div className="relative flex flex-1 flex-col items-center justify-center px-4 py-12">
        <div className="absolute top-4 right-4">
          <ThemeToggle />
        </div>
        <div className="w-full max-w-sm">
          <div className="mb-6 flex flex-col items-center text-center lg:hidden">
            <LogoMark withWordmark={false} className="mb-4" />
            <p className="font-heading text-2xl font-bold tracking-tight">JobHunter OS</p>
            <p className="text-sm text-muted-foreground">The operations center for your job search.</p>
          </div>
          {children}
        </div>
      </div>
    </div>
  );
}
