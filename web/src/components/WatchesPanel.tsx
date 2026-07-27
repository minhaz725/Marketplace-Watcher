import type { Watch } from "../api";

export function WatchesPanel({ watches }: { watches: Watch[] }) {
  return (
    <aside className="watches">
      <h2>Watches</h2>
      {watches.length === 0 ? (
        <p className="muted">None active.</p>
      ) : (
        <ul>
          {watches.map((watch) => (
            <li key={watch.id}>
              <div className="watch-name">{watch.name ?? "(free / match-all)"}</div>
              <div className="muted">{describeFilters(watch)}</div>
            </li>
          ))}
        </ul>
      )}
    </aside>
  );
}

function describeFilters(watch: Watch): string {
  const parts: string[] = [];

  if (watch.freeOnly) {
    parts.push("free only");
  } else if (watch.minPrice !== null || watch.maxPrice !== null) {
    parts.push(`NZ$${watch.minPrice ?? 0}–${watch.maxPrice ?? "∞"}`);
  }

  if (watch.excludeKeywords.length > 0) {
    parts.push(`excluding ${watch.excludeKeywords.join(", ")}`);
  }

  return parts.length > 0 ? parts.join(" · ") : "no filters";
}
