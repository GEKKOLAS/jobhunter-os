import { api } from "@/lib/api/client";
import { dashboardSchema, type RangeDays, skillsSchema, summarySchema } from "@/lib/schemas/analytics";

export const getSummary = (days: RangeDays, tz: string) =>
  api(`/analytics/summary?${new URLSearchParams({ days: String(days), tz })}`, { schema: summarySchema });

export const getSkillsReport = (days: RangeDays) => api(`/analytics/skills?days=${days}`, { schema: skillsSchema });

export const getDashboard = () => api("/dashboard", { schema: dashboardSchema });
