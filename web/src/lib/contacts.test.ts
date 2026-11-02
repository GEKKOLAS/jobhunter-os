import { describe, expect, it } from "vitest";
import { describeInteraction, lastTalked, reachLinks, roleLine, toLocalInput } from "@/lib/contacts";

describe("roleLine", () => {
  const company = { id: "c1", name: "Contoso" };

  it("says as much as is known", () => {
    expect(roleLine({ role: "Recruiter", company })).toBe("Recruiter at Contoso");
    expect(roleLine({ role: "Recruiter", company: null })).toBe("Recruiter");
    expect(roleLine({ role: null, company })).toBe("Contoso");
    expect(roleLine({ role: null, company: null })).toBeNull();
  });
});

describe("describeInteraction", () => {
  it("names the channel and who started it", () => {
    expect(describeInteraction({ channel: "email", direction: "inbound" })).toBe("Email, from them");
    expect(describeInteraction({ channel: "in_person", direction: "outbound" })).toBe("In person, from you");
  });
});

describe("lastTalked", () => {
  const now = new Date("2026-11-02T12:00:00Z");

  it("reads as a phrase", () => {
    expect(lastTalked("2026-11-02T08:00:00Z", now)).toBe("today");
    expect(lastTalked("2026-11-01T11:00:00Z", now)).toBe("1 day ago");
    expect(lastTalked("2026-10-23T12:00:00Z", now)).toBe("10 days ago");
  });
});

describe("toLocalInput", () => {
  it("formats a moment for a datetime-local input, in local time", () => {
    expect(toLocalInput(new Date(2026, 0, 5, 9, 3))).toBe("2026-01-05T09:03");
    expect(toLocalInput(new Date(2026, 10, 25, 23, 59, 59))).toBe("2026-11-25T23:59");
  });

  it("round-trips through the Date a form would build", () => {
    const moment = new Date(2026, 5, 15, 14, 30);
    expect(new Date(toLocalInput(moment)).getTime()).toBe(moment.getTime());
  });
});

describe("reachLinks", () => {
  it("builds links only for what's there, in a steady order", () => {
    expect(reachLinks({ email: null, linkedInUrl: null, phone: null })).toEqual([]);
    expect(reachLinks({ email: "ada@example.com", linkedInUrl: "https://linkedin.com/in/ada", phone: "+57 (300) 123-4567" })).toEqual([
      { label: "ada@example.com", href: "mailto:ada@example.com", external: false },
      { label: "LinkedIn", href: "https://linkedin.com/in/ada", external: true },
      { label: "+57 (300) 123-4567", href: "tel:+573001234567", external: false },
    ]);
  });
});
