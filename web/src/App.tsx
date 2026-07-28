import { useCallback, useEffect, useState } from "react";
import { api, type Listing, type Status, type Watch } from "./api";
import { ListingsTable } from "./components/ListingsTable";
import { StatusBar } from "./components/StatusBar";
import { WatchesPanel } from "./components/WatchesPanel";

const REFRESH_MS = 10_000;

export default function App() {
  // useState returns [currentValue, setter]. Calling the setter re-runs this function and
  // React updates the DOM. You never touch the DOM yourself — you change state and describe
  // what the UI looks like for that state.
  const [listings, setListings] = useState<Listing[]>([]);
  const [watches, setWatches] = useState<Watch[]>([]);
  const [status, setStatus] = useState<Status | null>(null);
  const [polling, setPolling] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // null = show every watch's listings. Holds the watch NAME because that is what
  // /api/listings filters on (Seen stores WatchName, denormalized at match time).
  const [selectedWatch, setSelectedWatch] = useState<string | null>(null);

  // useCallback keeps this the same function object between renders, so the useEffect below
  // doesn't see it as "changed" every time and restart its timer.
  const refresh = useCallback(async () => {
    try {
      // All three requests start together rather than waiting in turn.
      const [nextListings, nextWatches, nextStatus] = await Promise.all([
        api.listings(selectedWatch),
        api.watches(),
        api.status(),
      ]);
      setListings(nextListings);
      setWatches(nextWatches);
      setStatus(nextStatus);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Request failed");
    }
    // selectedWatch is a dependency: refresh must be rebuilt when it changes, which makes
    // the useEffect below re-run and re-fetch immediately on click.
  }, [selectedWatch]);

  // useEffect runs code AFTER render, for things outside React's world — here, fetching and
  // a timer. The [refresh] at the end is the dependency list: re-run only if that changes.
  // The returned function is cleanup, which React calls before re-running the effect and
  // when the component goes away — without it the interval would leak.
  useEffect(() => {
    void refresh();
    const timer = setInterval(() => void refresh(), REFRESH_MS);
    return () => clearInterval(timer);
  }, [refresh]);

  async function handlePollNow() {
    setPolling(true);
    try {
      await api.pollNow();
      // The worker scrapes asynchronously, so give it a moment before re-reading. Crude but
      // honest — pushing the result back would need a websocket.
      setTimeout(() => void refresh(), 3000);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Poll failed");
    } finally {
      setPolling(false);
    }
  }

  return (
    <div className="app">
      <header>
        <h1>Marketplace Watcher</h1>
      </header>

      <StatusBar status={status} onPollNow={handlePollNow} polling={polling} />

      {/* && renders the right side only when the left is truthy — the idiomatic
          "show this only if" in JSX, since you cannot put an if statement here. */}
      {error && <div className="banner-error">Cannot reach the API — {error}</div>}

      <main>
        <section className="listings">
          <h2>{selectedWatch === null ? "Listings" : `Listings · ${selectedWatch}`}</h2>
          <ListingsTable listings={listings} />
        </section>
        <WatchesPanel
          watches={watches}
          selected={selectedWatch}
          onSelect={setSelectedWatch}
          onChanged={() => void refresh()}
        />
      </main>
    </div>
  );
}
