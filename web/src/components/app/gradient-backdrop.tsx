// Nocturne's faint accent glow. Mailroom is flat, so the light theme renders nothing.
export function GradientBackdrop() {
  return (
    <div aria-hidden className="pointer-events-none fixed inset-0 -z-10 hidden overflow-hidden dark:block">
      <div className="absolute -top-56 left-1/4 h-[26rem] w-[44rem] rounded-full bg-brand opacity-[0.09] blur-[90px]" />
      <div className="absolute -right-40 bottom-[-12rem] size-[26rem] rounded-full bg-brand-2 opacity-[0.05] blur-[90px]" />
    </div>
  );
}
