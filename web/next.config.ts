import type { NextConfig } from "next";

// API origin. The browser only ever talks to this Next.js origin, so the session
// cookie stays first-party (docs/spec/02-architecture.md §1).
const apiUrl = process.env.API_URL ?? "http://localhost:5101";

const nextConfig: NextConfig = {
  async rewrites() {
    return [
      { source: "/api/:path*", destination: `${apiUrl}/api/:path*` },
      { source: "/healthz", destination: `${apiUrl}/healthz` },
    ];
  },
};

export default nextConfig;
