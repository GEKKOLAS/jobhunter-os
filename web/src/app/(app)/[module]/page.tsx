import { ModulePlaceholder } from "@/components/app/module-placeholder";
import { findModule, MODULES, phaseOf } from "@/lib/modules";
import { notFound } from "next/navigation";

// Areas that don't have a page of their own yet. The phase that builds an area adds
// `app/(app)/<slug>/page.tsx` and marks itself done, which drops the placeholder here.
// The dashboard is the roadmap until phase 8, so it always has its own page.
export const dynamicParams = false;

export function generateStaticParams() {
  return MODULES
    .filter((m) => m.slug !== "dashboard" && phaseOf(m.phase)?.status !== "done")
    .map((m) => ({ module: m.slug }));
}

export default async function ModulePage({ params }: { params: Promise<{ module: string }> }) {
  const { module: slug } = await params;
  const area = findModule(slug);
  if (!area) notFound();
  return <ModulePlaceholder area={area} />;
}
