import { EMPLOYMENT_TYPES, type OpportunityInput, SALARY_PERIODS, WORK_MODES } from "@/lib/schemas/opportunities";
import { z } from "zod";

export const PROVIDERS = ["greenhouse", "lever", "ashby"] as const;
export type Provider = (typeof PROVIDERS)[number];

export const previewSchema = z.object({
  title: z.string().nullable(),
  companyName: z.string().nullable(),
  location: z.string().nullable(),
  workMode: z.enum(WORK_MODES).nullable(),
  employmentType: z.enum(EMPLOYMENT_TYPES).nullable(),
  salaryMin: z.number().nullable(),
  salaryMax: z.number().nullable(),
  salaryCurrency: z.string().nullable(),
  salaryPeriod: z.enum(SALARY_PERIODS).nullable(),
  sourceUrl: z.string().nullable(),
  description: z.string(),
  source: z.string(),
  sourceRef: z.string().nullable(),
  parsedFrom: z.enum(["job_board", "structured_data", "page_text", "pasted_text"]),
  warnings: z.array(z.string()),
  existingOpportunityId: z.string().nullable(),
});
export type PostingPreview = z.infer<typeof previewSchema>;

export const sourceSchema = z.object({
  id: z.string(),
  provider: z.enum(PROVIDERS),
  board: z.string(),
  label: z.string(),
  lastCheckedAt: z.string().nullable(),
});
export type JobSource = z.infer<typeof sourceSchema>;

export const sourcePostingSchema = z.object({
  ref: z.string(),
  title: z.string(),
  location: z.string().nullable(),
  url: z.string().nullable(),
  alreadySaved: z.boolean(),
});
export type SourcePosting = z.infer<typeof sourcePostingSchema>;

/** A preview as the capture dialog's starting values. Saving keeps its source, so a re-import updates. */
export function previewToInput(preview: PostingPreview): OpportunityInput {
  return {
    title: preview.title ?? "",
    companyName: preview.companyName ?? "",
    location: preview.location,
    workMode: preview.workMode,
    employmentType: preview.employmentType,
    salaryMin: preview.salaryMin,
    salaryMax: preview.salaryMax,
    salaryCurrency: preview.salaryCurrency,
    salaryPeriod: preview.salaryPeriod,
    sourceUrl: preview.sourceUrl,
    description: preview.description,
    // Saving again updates the one already saved, whose stage and priority stay as they are.
    priority: preview.existingOpportunityId ? undefined : "normal",
    stage: preview.existingOpportunityId ? undefined : "saved",
    source: preview.source,
    sourceRef: preview.sourceRef,
  };
}

/** How sure to sound about a preview, in the dialog's description. */
export const PARSED_FROM_NOTES: Record<PostingPreview["parsedFrom"], string> = {
  job_board: "Read from the company's public job board.",
  structured_data: "Read from the page's job data. Check it before saving.",
  page_text: "A best guess from the page's text. Check every field before saving.",
  pasted_text: "A best guess from the text you pasted. Check every field before saving.",
};

const BOARD_LINKS: { provider: Provider; pattern: RegExp }[] = [
  { provider: "greenhouse", pattern: /^(?:job-)?boards(?:-api)?\.greenhouse\.io\/(?:v1\/boards\/)?([\w-]+)/ },
  { provider: "lever", pattern: /^jobs\.lever\.co\/([\w-]+)/ },
  { provider: "ashby", pattern: /^jobs\.ashbyhq\.com\/([\w-]+)/ },
];

/** A pasted board or job link → which board it is, so nobody has to know what a "board token" means. */
export function parseBoardLink(input: string): { provider: Provider; board: string } | null {
  const text = input.trim().replace(/^https?:\/\//i, "").replace(/^www\./i, "");
  for (const { provider, pattern } of BOARD_LINKS) {
    const match = text.match(pattern);
    if (match) return { provider, board: match[1] };
  }
  return null;
}
