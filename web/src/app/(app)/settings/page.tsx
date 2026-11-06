"use client";

import { PageHeader } from "@/components/app/page-header";
import { ActivityCard } from "@/components/settings/activity-card";
import { OutreachCard } from "@/components/settings/outreach-card";
import { PasswordForm } from "@/components/settings/password-form";
import { ProfileForm } from "@/components/settings/profile-form";
import { SessionsCard } from "@/components/settings/sessions-card";
import { SignOutCard } from "@/components/settings/sign-out-card";
import { Separator } from "@/components/ui/separator";
import { useSession } from "@/lib/hooks/use-session";
import { Settings as SettingsIcon } from "lucide-react";

export default function SettingsPage() {
  const { data: user } = useSession();
  if (!user) return null;

  return (
    <div className="mx-auto max-w-2xl space-y-8">
      <PageHeader icon={SettingsIcon} title="Settings" description="Your account, its sessions, its security and the connection to Outreach Studio." />
      <ProfileForm user={user} />
      <Separator />
      <PasswordForm />
      <Separator />
      <OutreachCard />
      <Separator />
      <SessionsCard />
      <ActivityCard />
      <Separator />
      <SignOutCard />
    </div>
  );
}
