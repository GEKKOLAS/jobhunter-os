import { describe, expect, it } from "vitest";
import { ageLabel, formatSalary, moveLabel } from "@/lib/opportunities";

describe("moveLabel", () => {
  it("names forward moves, closes and undos differently", () => {
    expect(moveLabel("saved", "applied")).toBe("Move to Applied");
    expect(moveLabel("applied", "no_response")).toBe("Close: no response");
    expect(moveLabel("rejected", "screening", "screening")).toBe("Undo: back to Screening");
  });
});

describe("ageLabel", () => {
  const now = new Date("2026-10-31T12:00:00Z");
  it("counts whole days", () => {
    expect(ageLabel("2026-10-31T08:00:00Z", now)).toBe("today");
    expect(ageLabel("2026-10-30T11:00:00Z", now)).toBe("1 day");
    expect(ageLabel("2026-10-19T12:00:00Z", now)).toBe("12 days");
  });
});

describe("formatSalary", () => {
  const none = { salaryMin: null, salaryMax: null, salaryCurrency: null, salaryPeriod: null };

  it("shows only what the posting stated", () => {
    expect(formatSalary(none)).toBeNull();
    expect(formatSalary({ ...none, salaryMin: 4000, salaryMax: 5500, salaryCurrency: "USD", salaryPeriod: "month" })).toBe(
      "USD 4,000–5,500 / month",
    );
    expect(formatSalary({ ...none, salaryMax: 60000, salaryCurrency: "EUR", salaryPeriod: "year" })).toBe("EUR 60,000 / year");
  });
});
