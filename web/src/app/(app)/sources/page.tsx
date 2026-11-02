"use client";

import { PageHeader } from "@/components/app/page-header";
import { ImportCard } from "@/components/sources/import-card";
import { SourcesCard } from "@/components/sources/sources-card";
import { Radar } from "lucide-react";

export default function SourcesPage() {
  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <PageHeader
        icon={Radar}
        title="Sources"
        description="Bring postings in from a link, from pasted text, or from a company's public job board. Nothing is saved until you say so."
      />
      <ImportCard />
      <SourcesCard />
    </div>
  );
}
