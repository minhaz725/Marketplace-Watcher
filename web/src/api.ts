// Empty on purpose: requests go to whatever host served the page, and Vite proxies /api
// through to the .NET app (see vite.config.ts). That keeps everything same-origin, so this
// works unchanged from another device on the WiFi — a hardcoded "localhost:5000" would mean
// *that device's* localhost, not the machine running the API.
const BASE_URL = "";

// These describe the JSON shapes the API returns. TypeScript types are COMPILE-TIME ONLY —
// they vanish in the built JS and enforce nothing at runtime. They catch you writing
// listing.titel in the editor; they do not validate what the server actually sent.
export interface Listing {
  signature: string;
  title: string;
  price: number | null;
  location: string | null;
  url: string | null;
  watchName: string | null;
  seenAt: string; // ISO 8601 — JSON has no date type, so it arrives as a string
}

export interface Watch {
  id: number;
  name: string | null;
  freeOnly: boolean;
  minPrice: number | null;
  maxPrice: number | null;
  mustIncludeKeywords: string[];
  excludeKeywords: string[];
  isActive: boolean;
}

export interface Status {
  healthy: boolean;
  lastSuccessfulPollAt: string | null;
  minutesSinceLastSuccess: number | null;
  lastPollNewCount: number;
  totalAlertsSent: number;
  consecutiveFailures: number;
  lastError: string;
  activeWatches: number;
  seenListings: number;
}

// fetch() only rejects on network failure — a 500 still "succeeds" as far as the promise is
// concerned. Checking response.ok is what turns an error status into a thrown error.
async function get<T>(path: string): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`);
  if (!response.ok) {
    throw new Error(`GET ${path} failed: ${response.status}`);
  }
  return response.json() as Promise<T>;
}

/** Matches the server's CreateWatchRequest. Omitted fields are null/empty server-side. */
export interface CreateWatchRequest {
  name: string | null;
  freeOnly: boolean;
  minPrice: number | null;
  maxPrice: number | null;
  mustIncludeKeywords: string[];
  excludeKeywords: string[];
}

export const api = {
  listings: (watch?: string | null, limit = 100) => {
    // encodeURIComponent so a watch name with spaces or "&" survives the query string.
    const watchParam = watch ? `&watch=${encodeURIComponent(watch)}` : "";
    return get<Listing[]>(`/api/listings?limit=${limit}${watchParam}`);
  },
  watches: () => get<Watch[]>("/api/watches"),
  status: () => get<Status>("/api/status"),

  pollNow: async (): Promise<void> => {
    const response = await fetch(`${BASE_URL}/api/poll`, { method: "POST" });
    if (!response.ok) {
      throw new Error(`POST /api/poll failed: ${response.status}`);
    }
  },

  createWatch: async (request: CreateWatchRequest): Promise<Watch> => {
    const response = await fetch(`${BASE_URL}/api/watches`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      // The server explains *why* a 400 happened; surfacing the status alone would
      // leave the user guessing.
      const detail = await response.text();
      throw new Error(detail || `POST /api/watches failed: ${response.status}`);
    }

    return response.json() as Promise<Watch>;
  },

  deleteWatch: async (id: number): Promise<void> => {
    const response = await fetch(`${BASE_URL}/api/watches/${id}`, { method: "DELETE" });
    if (!response.ok) {
      throw new Error(`DELETE /api/watches/${id} failed: ${response.status}`);
    }
  },
};
