import { describe, expect, it } from "vitest";
import { ApiError } from "@/lib/api/client";
import { describeApiError, fieldError } from "@/lib/api/form-errors";

describe("describeApiError", () => {
  it("maps validation errors to fields and leaves no general message", () => {
    const errors = describeApiError(
      new ApiError(422, "validation_failed", "Validation failed.", { level: ["Too high."], "links[0].url": ["Use https."] }),
    );
    expect(errors.message).toBeNull();
    expect(errors.fields.level).toBe("Too high.");
    expect(fieldError(errors, "links")).toBe("Use https.");
  });

  it("explains version conflicts and other failures in one message", () => {
    expect(describeApiError(new ApiError(409, "concurrency.conflict")).message).toMatch(/reloaded/);
    expect(describeApiError(new ApiError(409, "skill.duplicate", '"C#" is already in your skills.')).message).toBe(
      '"C#" is already in your skills.',
    );
    expect(describeApiError(new Error("network")).message).toMatch(/Something went wrong/);
  });
});
