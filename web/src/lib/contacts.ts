import { ageLabel } from "@/lib/opportunities";
import type { Channel, Contact, Direction, Interaction, Relation } from "@/lib/schemas/contacts";

export const RELATION_LABELS: Record<Relation, string> = {
  recruiter: "Recruiter",
  hiring_manager: "Hiring manager",
  referral: "Referral",
  interviewer: "Interviewer",
  other: "Other",
};

export const CHANNEL_LABELS: Record<Channel, string> = {
  email: "Email",
  linkedin: "LinkedIn",
  phone: "Phone",
  video: "Video call",
  in_person: "In person",
  other: "Other",
};

export const DIRECTION_LABELS: Record<Direction, string> = { outbound: "You reached out", inbound: "They reached out" };

/** "Technical Recruiter at Contoso", or as much of it as is known. */
export function roleLine(c: Pick<Contact, "role" | "company">): string | null {
  if (c.role && c.company) return `${c.role} at ${c.company.name}`;
  return c.role ?? c.company?.name ?? null;
}

/** "Email from them", "Phone call you made": the one line that says what kind of entry it is. */
export function describeInteraction(i: Pick<Interaction, "channel" | "direction">): string {
  const channel = CHANNEL_LABELS[i.channel];
  return i.direction === "inbound" ? `${channel}, from them` : `${channel}, from you`;
}

/** "today", or "3 days ago": how long since you last talked. */
export function lastTalked(at: string, now: Date = new Date()): string {
  const age = ageLabel(at, now);
  return age === "today" ? age : `${age} ago`;
}

/** The value a `datetime-local` input takes for a moment, in the viewer's own time zone. */
export function toLocalInput(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** Where to reach someone, as ready-made links. Only what the server has already checked ends up in an href. */
export function reachLinks(c: Pick<Contact, "email" | "linkedInUrl" | "phone">): { label: string; href: string; external: boolean }[] {
  const links: { label: string; href: string; external: boolean }[] = [];
  if (c.email) links.push({ label: c.email, href: `mailto:${c.email}`, external: false });
  if (c.linkedInUrl) links.push({ label: "LinkedIn", href: c.linkedInUrl, external: true });
  if (c.phone) links.push({ label: c.phone, href: `tel:${c.phone.replace(/[^\d+]/g, "")}`, external: false });
  return links;
}
