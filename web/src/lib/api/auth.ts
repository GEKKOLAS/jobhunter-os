import { api } from "@/lib/api/client";
import {
  type ChangePasswordInput,
  type ForgotPasswordInput,
  type LoginInput,
  type RegisterInput,
  auditLogSchema,
  type Session,
  sessionSchema,
  type UpdateProfileInput,
  type User,
  userSchema,
} from "@/lib/schemas/auth";
import { z } from "zod";

const authResponseSchema = z.object({ user: userSchema });
const sessionsResponseSchema = z.object({ items: z.array(sessionSchema) });

export function register(input: RegisterInput): Promise<{ user: User }> {
  return api("/auth/register", { body: input, schema: authResponseSchema });
}

export function login(input: LoginInput): Promise<{ user: User }> {
  return api("/auth/login", { body: input, schema: authResponseSchema });
}

export function logout(): Promise<void> {
  return api("/auth/logout", { method: "POST" });
}

export function forgotPassword(input: ForgotPasswordInput): Promise<void> {
  return api("/auth/password/forgot", { body: input });
}

export function resetPassword(input: { token: string; newPassword: string }): Promise<void> {
  return api("/auth/password/reset", { body: input });
}

export function getMe(): Promise<User> {
  return api("/me", { schema: userSchema });
}

export function updateProfile(input: UpdateProfileInput): Promise<User> {
  return api("/me", { method: "PATCH", body: input, schema: userSchema });
}

export function changePassword(input: Omit<ChangePasswordInput, "confirmPassword">): Promise<void> {
  return api("/me/password", { method: "POST", body: input });
}

export function listSessions(): Promise<Session[]> {
  return api("/auth/sessions", { schema: sessionsResponseSchema }).then((r) => r.items);
}

export function revokeSession(id: string): Promise<void> {
  return api(`/auth/sessions/${id}`, { method: "DELETE" });
}

const auditLogsResponseSchema = z.object({ items: z.array(auditLogSchema), totalCount: z.number() });

export function listAuditLogs(pageSize = 10) {
  return api(`/audit-logs?pageSize=${pageSize}`, { schema: auditLogsResponseSchema });
}

export function logoutEverywhere(): Promise<void> {
  return api("/auth/logout-all", { method: "POST" });
}
