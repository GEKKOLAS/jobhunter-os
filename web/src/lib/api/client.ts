import { z } from "zod";

/** RFC 7807 ProblemDetails as emitted by the backend (docs/spec/06-api.md). */
export const problemDetailsSchema = z.object({
  type: z.string().optional(),
  title: z.string().optional(),
  status: z.number().optional(),
  detail: z.string().optional(),
  errorCode: z.string().optional(),
  traceId: z.string().optional(),
  errors: z.record(z.string(), z.array(z.string())).optional(),
});

export type ProblemDetails = z.infer<typeof problemDetailsSchema>;

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly errorCode: string,
    message?: string,
    readonly fieldErrors?: Record<string, string[]>,
  ) {
    super(message ?? errorCode);
    this.name = "ApiError";
  }

  static fromProblem(status: number, problem?: ProblemDetails): ApiError {
    return new ApiError(
      status,
      problem?.errorCode ?? `http_${status}`,
      problem?.title ?? problem?.detail,
      problem?.errors,
    );
  }
}

function getCsrfToken(): string {
  if (typeof document === "undefined") return "";
  const match = document.cookie.match(/(?:^|;\s*)mth_csrf=([^;]*)/);
  return match ? decodeURIComponent(match[1]) : "";
}

type RequestOptions<T> = Omit<RequestInit, "body"> & {
  body?: unknown;
  schema?: z.ZodType<T>;
};

/**
 * Fetch wrapper for the backend API. Session lives in an HttpOnly cookie
 * (same origin via the Next.js rewrite proxy); non-GET requests carry the
 * CSRF double-submit header.
 */
export async function api<T = unknown>(
  path: string,
  { body, schema, headers, ...init }: RequestOptions<T> = {},
): Promise<T> {
  const method = init.method ?? (body === undefined ? "GET" : "POST");

  const response = await fetch(`/api/v1${path}`, {
    ...init,
    method,
    credentials: "include",
    headers: {
      ...(body !== undefined && { "Content-Type": "application/json" }),
      ...(method !== "GET" && { "X-CSRF-Token": getCsrfToken() }),
      ...headers,
    },
    ...(body !== undefined && { body: JSON.stringify(body) }),
  });

  if (!response.ok) {
    const problem = problemDetailsSchema.safeParse(
      await response.json().catch(() => undefined),
    );
    throw ApiError.fromProblem(
      response.status,
      problem.success ? problem.data : undefined,
    );
  }

  if (response.status === 204) return undefined as T;
  const json: unknown = await response.json();
  return schema ? schema.parse(json) : (json as T);
}
