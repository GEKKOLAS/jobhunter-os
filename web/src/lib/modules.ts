import {
  BarChart3,
  BellRing,
  Briefcase,
  Columns3,
  LayoutDashboard,
  Radar,
  Settings,
  UserRound,
  Users,
  type LucideIcon,
} from "lucide-react";

/** The phases of docs/spec/04-implementation-plan.md; keep the two in step. */
export const PHASES = [
  { phase: 0, title: "Foundation", status: "done" },
  { phase: 1, title: "Identity", status: "next" },
  { phase: 2, title: "Profile & evidence", status: "planned" },
  { phase: 3, title: "Opportunities: capture, board, history", status: "planned" },
  { phase: 4, title: "Imports: text, URL, job boards", status: "planned" },
  { phase: 5, title: "Matching", status: "planned" },
  { phase: 6, title: "Contacts", status: "planned" },
  { phase: 7, title: "Follow-ups and Outreach Studio", status: "planned" },
  { phase: 8, title: "Analytics and dashboard", status: "planned" },
  { phase: 9, title: "Launch", status: "planned" },
] as const;

export type PhaseStatus = (typeof PHASES)[number]["status"];

export type ModuleGroup = "Pipeline" | "People" | "You";

export type AppModule = {
  /** Route segment: `/board`, `/contacts`… */
  slug: string;
  label: string;
  icon: LucideIcon;
  group: ModuleGroup;
  /** The phase that builds the module's page (spec 04). */
  phase: number;
  summary: string;
};

/** Every area of the app, in navigation order. */
export const MODULES: AppModule[] = [
  {
    slug: "dashboard",
    label: "Dashboard",
    icon: LayoutDashboard,
    group: "Pipeline",
    phase: 8,
    summary: "Today's reminders, applications waiting too long for an answer, and your top-priority opportunities.",
  },
  {
    slug: "board",
    label: "Board",
    icon: Columns3,
    group: "Pipeline",
    phase: 3,
    summary: "Every opportunity on a Kanban board, from saved to offer, with its fit score on the card.",
  },
  {
    slug: "opportunities",
    label: "Opportunities",
    icon: Briefcase,
    group: "Pipeline",
    phase: 3,
    summary: "The postings you captured, with their requirements, fit, stage history and contacts.",
  },
  {
    slug: "sources",
    label: "Sources",
    icon: Radar,
    group: "Pipeline",
    phase: 4,
    summary: "Import a posting from a link or pasted text, or browse a company's public job board and pick what to save.",
  },
  {
    slug: "contacts",
    label: "Contacts",
    icon: Users,
    group: "People",
    phase: 6,
    summary: "Recruiters and hiring managers, linked to opportunities, with every conversation logged.",
  },
  {
    slug: "follow-ups",
    label: "Follow-ups",
    icon: BellRing,
    group: "People",
    phase: 7,
    summary: "Reminders and email drafts. Nothing is sent until you approve it, and then it goes out through Outreach Studio.",
  },
  {
    slug: "profile",
    label: "Profile",
    icon: UserRound,
    group: "You",
    phase: 2,
    summary: "Your skills with level and years, and the projects and roles that back them up.",
  },
  {
    slug: "analytics",
    label: "Analytics",
    icon: BarChart3,
    group: "You",
    phase: 8,
    summary: "Response, interview and offer rates with their sample sizes, the skills postings ask for, and your most frequent gaps.",
  },
  {
    slug: "settings",
    label: "Settings",
    icon: Settings,
    group: "You",
    phase: 1,
    summary: "Your account and sessions, and the connection to Outreach Studio.",
  },
];

export const MODULE_GROUPS: ModuleGroup[] = ["Pipeline", "People", "You"];

export function findModule(slug: string) {
  return MODULES.find((m) => m.slug === slug);
}

export function phaseOf(phase: number) {
  return PHASES.find((p) => p.phase === phase);
}

export function isActivePath(pathname: string, slug: string) {
  return pathname === `/${slug}` || pathname.startsWith(`/${slug}/`);
}
