export const queryKeys = {
  ping: ["ping"] as const,
  me: ["me"] as const,
  sessions: ["sessions"] as const,
  auditLogs: ["audit-logs"] as const,
  profile: ["profile"] as const,
  skills: ["profile-skills"] as const,
  evidence: ["profile-evidence"] as const,
  catalog: (q: string) => ["skill-catalog", q] as const,
};
