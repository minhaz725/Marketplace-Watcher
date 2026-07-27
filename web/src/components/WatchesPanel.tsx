import type { Watch } from "../api";

interface WatchesPanelProps {
  watches: Watch[];
  selected: string | null;
  onSelect: (watchName: string | null) => void;
}

export function WatchesPanel({ watches, selected, onSelect }: WatchesPanelProps) {
  return (
    <aside className="watches">
      <h2>Watches</h2>

      {watches.length === 0 ? (
        <p className="muted">None active.</p>
      ) : (
        <ul>
          {watches.map((watch) => {
            const name = watch.name ?? "(free / match-all)";
            const isSelected = selected === watch.name;

            return (
              <li key={watch.id}>
                {/* A real <button>, not a clickable <div>: it is focusable and works with
                    the keyboard for free. Clicking the selected one again clears it. */}
                <button
                  type="button"
                  className={isSelected ? "watch-card watch-card-active" : "watch-card"}
                  aria-pressed={isSelected}
                  onClick={() => onSelect(isSelected ? null : watch.name)}
                >
                  <span className="watch-name">{name}</span>
                  <span className="muted">{describeFilters(watch)}</span>
                </button>
              </li>
            );
          })}
        </ul>
      )}

      {selected !== null && (
        <button type="button" className="clear-filter" onClick={() => onSelect(null)}>
          Show all listings
        </button>
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
