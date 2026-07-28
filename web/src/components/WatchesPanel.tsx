import { useState } from "react";
import { api, type Watch } from "../api";
import { WatchForm } from "./WatchForm";

interface WatchesPanelProps {
  watches: Watch[];
  selected: string | null;
  onSelect: (watchName: string | null) => void;
  onChanged: () => void;
}

export function WatchesPanel({ watches, selected, onSelect, onChanged }: WatchesPanelProps) {
  const [creating, setCreating] = useState(false);
  const [deletingId, setDeletingId] = useState<number | null>(null);

  async function handleDelete(watch: Watch) {
    const label = watch.name ?? "this watch";
    // Destructive and one click away, so confirm first.
    if (!window.confirm(`Stop watching “${label}”?`)) return;

    setDeletingId(watch.id);
    try {
      await api.deleteWatch(watch.id);
      // If the deleted watch was the active filter, clear it — otherwise the table
      // would keep filtering by a watch no longer in the list.
      if (selected === watch.name) onSelect(null);
      onChanged();
    } finally {
      setDeletingId(null);
    }
  }

  return (
    <aside className="watches">
      <div className="watches-head">
        <h2>Watches</h2>
        {!creating && (
          <button type="button" className="small" onClick={() => setCreating(true)}>
            + New
          </button>
        )}
      </div>

      {creating && (
        <WatchForm
          onCancel={() => setCreating(false)}
          onCreated={() => {
            setCreating(false);
            onChanged(); // Re-fetch so the new watch appears immediately.
          }}
        />
      )}

      {watches.length === 0 && !creating ? (
        <p className="muted">None active. Create one to start watching.</p>
      ) : (
        <ul>
          {watches.map((watch) => {
            const name = watch.name ?? "(free / match-all)";
            const isSelected = selected === watch.name;

            return (
              <li key={watch.id} className="watch-row">
                {/* A real <button>, not a clickable <div>: focusable and keyboard-operable
                    for free. Clicking the selected one again clears the filter. */}
                <button
                  type="button"
                  className={isSelected ? "watch-card watch-card-active" : "watch-card"}
                  aria-pressed={isSelected}
                  onClick={() => onSelect(isSelected ? null : watch.name)}
                >
                  <span className="watch-name">{name}</span>
                  <span className="muted">{describeFilters(watch)}</span>
                </button>

                <button
                  type="button"
                  className="icon-button watch-delete"
                  onClick={() => handleDelete(watch)}
                  disabled={deletingId === watch.id}
                  aria-label={`Stop watching ${name}`}
                  title="Stop watching"
                >
                  ×
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

  if (watch.mustIncludeKeywords.length > 0) {
    parts.push(`must include ${watch.mustIncludeKeywords.join(", ")}`);
  }

  if (watch.excludeKeywords.length > 0) {
    parts.push(`excluding ${watch.excludeKeywords.join(", ")}`);
  }

  return parts.length > 0 ? parts.join(" · ") : "no filters";
}
