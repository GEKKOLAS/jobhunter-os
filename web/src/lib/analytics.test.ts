import { countTicks, days, describeMedian, describeRate, endOfToday, percent, weekLabel } from "@/lib/analytics";
import { describe, expect, it } from "vitest";

describe("describeRate", () => {
  it("shows a percentage with what it came from", () => {
    expect(describeRate({ numerator: 2, denominator: 5, value: 0.4 })).toEqual({ headline: "40%", detail: "2 of 5", known: true });
  });

  it("says too few to say when the API withheld the value", () => {
    expect(describeRate({ numerator: 1, denominator: 4, value: null })).toEqual({ headline: "Too few to say", detail: "1 of 4", known: false });
  });

  it("says none yet when there is nothing to count", () => {
    expect(describeRate({ numerator: 0, denominator: 0, value: null })).toMatchObject({ headline: "None yet", known: false });
  });

  it("shows zero as zero, not as unknown", () => {
    expect(describeRate({ numerator: 0, denominator: 6, value: 0 })).toMatchObject({ headline: "0%", known: true });
  });
});

describe("describeMedian", () => {
  it("names days and the answers behind them", () => {
    expect(describeMedian({ days: 3.5, count: 7 })).toEqual({ headline: "3.5 days", detail: "7 answers", known: true });
    expect(describeMedian({ days: 1, count: 5 }).headline).toBe("1 day");
  });

  it("holds back under five answers and says when there are none", () => {
    expect(describeMedian({ days: null, count: 1 })).toEqual({ headline: "Too few to say", detail: "1 answer", known: false });
    expect(describeMedian({ days: null, count: 0 }).headline).toBe("None yet");
  });
});

describe("formatting", () => {
  it("rounds percentages and pluralizes days", () => {
    expect(percent(0.3333)).toBe("33%");
    expect(percent(1)).toBe("100%");
    expect(days(0)).toBe("0 days");
  });

  it("reads a week start as a calendar date in any zone", () => {
    expect(weekLabel("2026-10-05", "en-US")).toBe("Oct 5");
    expect(weekLabel("2026-01-01", "en-US")).toBe("Jan 1");
  });

  it("ends today at the last millisecond of the local day", () => {
    const end = endOfToday(new Date(2026, 10, 5, 9, 30));
    expect([end.getDate(), end.getHours(), end.getMinutes(), end.getMilliseconds()]).toEqual([5, 23, 59, 999]);
  });
});

describe("countTicks", () => {
  it("counts in ones for small numbers and never goes below one", () => {
    expect(countTicks(0)).toEqual([0, 1]);
    expect(countTicks(3)).toEqual([0, 1, 2, 3]);
    expect(countTicks(5)).toEqual([0, 1, 2, 3, 4, 5]);
  });

  it("uses steps of 2, 5, 10… so there are at most six ticks", () => {
    expect(countTicks(7)).toEqual([0, 2, 4, 6, 8]);
    expect(countTicks(23)).toEqual([0, 5, 10, 15, 20, 25]);
    expect(countTicks(140)).toEqual([0, 50, 100, 150]);
  });
});
