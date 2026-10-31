import { ModulePlaceholder } from "@/components/app/module-placeholder";
import { findModule, MODULES } from "@/lib/modules";
import { notFound } from "next/navigation";

// Areas that don't have a page of their own yet. A phase adds `app/(app)/<slug>/page.tsx`,
// which takes precedence over this dynamic segment.
export const dynamicParams = false;

export function generateStaticParams() {
  return MODULES.filter((m) => m.slug !== "dashboard").map((m) => ({ module: m.slug }));
}

export default async function ModulePage({ params }: { params: Promise<{ module: string }> }) {
  const { module: slug } = await params;
  const area = findModule(slug);
  if (!area) notFound();
  return <ModulePlaceholder area={area} />;
}
