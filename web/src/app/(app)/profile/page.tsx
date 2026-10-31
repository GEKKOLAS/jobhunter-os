"use client";

import { PageHeader } from "@/components/app/page-header";
import { AboutCard } from "@/components/profile/about-card";
import { EvidenceCard } from "@/components/profile/evidence-card";
import { SkillsCard } from "@/components/profile/skills-card";
import { getProfile, listEvidence, listSkills } from "@/lib/api/profile";
import { queryKeys } from "@/lib/query/query-keys";
import { useQuery } from "@tanstack/react-query";
import { UserRound } from "lucide-react";

export default function ProfilePage() {
  const profile = useQuery({ queryKey: queryKeys.profile, queryFn: getProfile });
  const skills = useQuery({ queryKey: queryKeys.skills, queryFn: listSkills });
  const evidence = useQuery({ queryKey: queryKeys.evidence, queryFn: listEvidence });

  const unbacked = skills.data?.filter((s) => s.evidenceCount === 0).length ?? 0;
  const failed = profile.isError || skills.isError || evidence.isError;

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-6">
      <PageHeader
        icon={UserRound}
        eyebrow={
          skills.data && evidence.data
            ? `${skills.data.length} skills · ${evidence.data.length} evidence${unbacked > 0 ? ` · ${unbacked} without evidence` : ""}`
            : undefined
        }
        title="Profile"
        description="What every posting is measured against. Everything here is in your own words."
      />

      {failed && <p className="text-sm text-destructive">Could not load your profile. Reload to try again.</p>}

      {/* Keyed by version: after a save (or a conflict reload) the form starts from the saved values. */}
      {profile.data && <AboutCard key={profile.data.version} profile={profile.data} />}
      {skills.data && <SkillsCard skills={skills.data} />}
      {skills.data && evidence.data && <EvidenceCard items={evidence.data} skills={skills.data} />}
    </div>
  );
}
