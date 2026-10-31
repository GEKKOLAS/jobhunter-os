import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { logout, logoutEverywhere } from "@/lib/api/auth";
import { useSignOut } from "@/lib/hooks/use-sign-out";

vi.mock("@/lib/api/auth", () => ({ logout: vi.fn(), logoutEverywhere: vi.fn() }));

const replace = vi.fn();

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe("useSignOut", () => {
  beforeEach(() => vi.stubGlobal("location", { replace }));
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
  });

  it("ends this session and lands on plain /login, without a ?next= back to this page", async () => {
    vi.mocked(logout).mockResolvedValue(undefined);
    const { result } = renderHook(() => useSignOut(), { wrapper });

    result.current.mutate();

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/login"));
    expect(logout).toHaveBeenCalledOnce();
    expect(logoutEverywhere).not.toHaveBeenCalled();
  });

  it("can end every session instead", async () => {
    vi.mocked(logoutEverywhere).mockResolvedValue(undefined);
    const { result } = renderHook(() => useSignOut({ everywhere: true }), { wrapper });

    result.current.mutate();

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/login"));
    expect(logoutEverywhere).toHaveBeenCalledOnce();
    expect(logout).not.toHaveBeenCalled();
  });

  it("still leaves when the request fails", async () => {
    vi.mocked(logout).mockRejectedValue(new Error("session already expired"));
    const { result } = renderHook(() => useSignOut(), { wrapper });

    result.current.mutate();

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/login"));
  });
});
