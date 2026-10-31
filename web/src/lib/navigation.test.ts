import { describe, expect, it } from "vitest";
import { HOME_PATH, loginPathFor, safeNext } from "@/lib/navigation";

describe("safeNext", () => {
  it("keeps paths on this site, with their query", () => {
    expect(safeNext("/board")).toBe("/board");
    expect(safeNext("/opportunities/42?tab=fit")).toBe("/opportunities/42?tab=fit");
  });

  it.each([
    ["missing", null],
    ["empty", ""],
    ["absolute URL", "https://evil.example/board"],
    ["protocol-relative", "//evil.example"],
    ["backslash host", "/\\evil.example"],
    ["javascript URL", "javascript:alert(1)"],
    ["control character", "/\tboard"],
    ["auth page loop", "/login?next=/board"],
  ])("falls back to the dashboard for %s", (_, next) => {
    expect(safeNext(next)).toBe(HOME_PATH);
  });
});

describe("loginPathFor", () => {
  it("remembers where to come back to", () => {
    expect(loginPathFor("/follow-ups")).toBe("/login?next=%2Ffollow-ups");
  });

  it("doesn't add a pointless next for the home page", () => {
    expect(loginPathFor(HOME_PATH)).toBe("/login");
  });
});
