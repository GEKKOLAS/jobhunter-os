import { describe, expect, it } from "vitest";
import { parseBoardLink, type PostingPreview, previewToInput } from "@/lib/schemas/imports";

describe("parseBoardLink", () => {
  it.each([
    ["https://boards.greenhouse.io/contoso", "greenhouse", "contoso"],
    ["https://job-boards.greenhouse.io/discord/jobs/8840756002", "greenhouse", "discord"],
    ["boards-api.greenhouse.io/v1/boards/stripe/jobs", "greenhouse", "stripe"],
    ["https://jobs.lever.co/leverdemo/ad208490", "lever", "leverdemo"],
    ["jobs.lever.co/leverdemo", "lever", "leverdemo"],
    ["https://jobs.ashbyhq.com/ashby", "ashby", "ashby"],
    ["  https://www.jobs.ashbyhq.com/some-co/uuid  ", "ashby", "some-co"],
  ])("reads %s", (input, provider, board) => {
    expect(parseBoardLink(input)).toEqual({ provider, board });
  });

  it.each(["contoso", "https://careers.contoso.com/jobs", "https://evil.example/boards.greenhouse.io/contoso", ""])(
    "ignores %s",
    (input) => {
      expect(parseBoardLink(input)).toBeNull();
    },
  );
});

describe("previewToInput", () => {
  const preview: PostingPreview = {
    title: null,
    companyName: "Contoso",
    location: null,
    workMode: "remote",
    employmentType: null,
    salaryMin: null,
    salaryMax: null,
    salaryCurrency: null,
    salaryPeriod: null,
    sourceUrl: "https://example.com/job",
    description: "Build APIs.",
    source: "url",
    sourceRef: "abc",
    parsedFrom: "page_text",
    warnings: [],
    existingOpportunityId: null,
  };

  it("fills the form, keeps the source for dedupe, and leaves missing fields blank to fill in", () => {
    const input = previewToInput(preview);

    expect(input.title).toBe("");
    expect(input.companyName).toBe("Contoso");
    expect(input.workMode).toBe("remote");
    expect(input.source).toBe("url");
    expect(input.sourceRef).toBe("abc");
    expect(input.stage).toBe("saved");
  });

  it("leaves stage and priority out when the posting is already saved: saving only updates it", () => {
    const input = previewToInput({ ...preview, existingOpportunityId: "o1" });

    expect(input.stage).toBeUndefined();
    expect(input.priority).toBeUndefined();
  });
});
