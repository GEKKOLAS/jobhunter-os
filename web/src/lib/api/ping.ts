import { z } from "zod";
import { api } from "@/lib/api/client";

export const pingSchema = z.object({
  status: z.literal("ok"),
  service: z.string(),
  time: z.string(),
});
export type Ping = z.infer<typeof pingSchema>;

export function ping() {
  return api("/ping", { schema: pingSchema });
}
