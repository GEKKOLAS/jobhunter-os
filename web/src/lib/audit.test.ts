import { describe, expect, it } from "vitest";
import { auditLabel, isSecurityWarning } from "@/lib/audit";

describe("auditLabel", () => {
  it("names known actions", () => {
    expect(auditLabel("auth.login_failed")).toBe("Failed sign-in attempt");
    expect(auditLabel("auth.password_changed")).toBe("Password changed");
  });

  it("tidies codes it doesn't know yet", () => {
    expect(auditLabel("opportunity.stage_changed")).toBe("Opportunity stage changed");
  });
});

describe("isSecurityWarning", () => {
  it("flags failed sign-ins only", () => {
    expect(isSecurityWarning("auth.login_failed")).toBe(true);
    expect(isSecurityWarning("auth.login")).toBe(false);
  });
});
