"use client";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { useSignOut } from "@/lib/hooks/use-sign-out";
import { LogOut } from "lucide-react";

export function SignOutCard() {
  const signOut = useSignOut();
  const signOutEverywhere = useSignOut({ everywhere: true });
  const pending = signOut.isPending || signOutEverywhere.isPending;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Sign out</CardTitle>
        <CardDescription>Lost a device? Signing out everywhere ends every session, this one included.</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-wrap gap-2">
        <Button variant="outline" onClick={() => signOut.mutate()} disabled={pending}>
          <LogOut />
          Sign out
        </Button>
        <Button variant="destructive" onClick={() => signOutEverywhere.mutate()} disabled={pending}>
          Sign out everywhere
        </Button>
      </CardContent>
    </Card>
  );
}
