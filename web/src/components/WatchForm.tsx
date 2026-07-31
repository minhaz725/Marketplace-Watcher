import { useEffect, useState } from "react";
import { api } from "../api";

interface WatchFormProps {
  onCancel: () => void;
  onCreated: () => void;
}

export function WatchForm({ onCancel, onCreated }: WatchFormProps) {
  const [name, setName] = useState("");
  const [freeOnly, setFreeOnly] = useState(false);
  const [minPrice, setMinPrice] = useState("");
  const [maxPrice, setMaxPrice] = useState("");
  const [mustInclude, setMustInclude] = useState("");
  const [exclude, setExclude] = useState("");
  const [chatId, setChatId] = useState("");
  const [days, setDays] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Escape closes the form, which people expect from anything expandable.
  // The cleanup return matters: without it every open would stack another listener.
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") onCancel();
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [onCancel]);

  const trimmedName = name.trim();
  const min = minPrice === "" ? null : Number(minPrice);
  const max = maxPrice === "" ? null : Number(maxPrice);

  // A keyword in both lists makes the watch unsatisfiable — it would silently match nothing
  // forever, which is exactly the kind of failure that is painful to diagnose later.
  const contradictions = splitKeywords(mustInclude).filter((k) =>
    splitKeywords(exclude).some((e) => e.toLowerCase() === k.toLowerCase()),
  );

  // Validation is computed during render rather than stored in state — deriving it means
  // it can never fall out of sync with the fields it describes.
  const validationError =
    !freeOnly && trimmedName === ""
      ? "Give it a search term, or tick “free only”."
      : min !== null && Number.isNaN(min)
        ? "Min price must be a number."
        : max !== null && Number.isNaN(max)
          ? "Max price must be a number."
          : min !== null && max !== null && min > max
            ? "Min price can’t be higher than max price."
            : days.trim() !== "" && (Number.isNaN(Number(days)) || Number(days) < 1 || Number(days) > 90)
              ? "Days must be a whole number between 1 and 90."
              : contradictions.length > 0
              ? `“${contradictions.join(", ")}” is in both must-include and exclude — nothing could ever match.`
              : null;

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault(); // Stop the browser doing a full page reload.
    if (validationError || submitting) return;

    setSubmitting(true);
    setError(null);
    try {
      await api.createWatch({
        name: trimmedName === "" ? null : trimmedName,
        freeOnly,
        minPrice: freeOnly ? null : min,
        maxPrice: freeOnly ? null : max,
        mustIncludeKeywords: splitKeywords(mustInclude),
        excludeKeywords: splitKeywords(exclude),
        chatId: chatId.trim() === "" ? null : chatId.trim(),
        daysSinceListed: days.trim() === "" ? null : Number(days),
      });
      onCreated();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not create the watch");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="watch-form" onSubmit={handleSubmit}>
      <div className="watch-form-head">
        <strong>New watch</strong>
        <button type="button" className="icon-button" onClick={onCancel} aria-label="Cancel">
          ×
        </button>
      </div>

      <label>
        Search term
        {/* autoFocus so the form is usable straight from the keyboard */}
        <input
          autoFocus
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="e.g. Toyota Prius"
          disabled={submitting}
        />
      </label>

      <label className="checkbox">
        <input
          type="checkbox"
          checked={freeOnly}
          onChange={(e) => setFreeOnly(e.target.checked)}
          disabled={submitting}
        />
        Free items only
      </label>

      {/* Price inputs vanish when freeOnly is on — the server ignores them in that case,
          so showing them would imply they do something. */}
      {!freeOnly && (
        <div className="watch-form-row">
          <label>
            Min NZ$
            <input
              type="number"
              inputMode="numeric"
              value={minPrice}
              onChange={(e) => setMinPrice(e.target.value)}
              placeholder="0"
              disabled={submitting}
            />
          </label>
          <label>
            Max NZ$
            <input
              type="number"
              inputMode="numeric"
              value={maxPrice}
              onChange={(e) => setMaxPrice(e.target.value)}
              placeholder="any"
              disabled={submitting}
            />
          </label>
        </div>
      )}

      <label>
        Must include
        <input
          value={mustInclude}
          onChange={(e) => setMustInclude(e.target.value)}
          placeholder="prius, hybrid"
          disabled={submitting}
        />
      </label>

      <label>
        Exclude
        <input
          value={exclude}
          onChange={(e) => setExclude(e.target.value)}
          placeholder="wanted, parts, wrecking"
          disabled={submitting}
        />
      </label>

      <label>
        Listed within (days)
        <input
          type="number"
          inputMode="numeric"
          min={1}
          max={90}
          value={days}
          onChange={(e) => setDays(e.target.value)}
          placeholder="1"
          disabled={submitting}
        />
      </label>

      <label>
        Send to (Telegram chat id)
        <input
          value={chatId}
          onChange={(e) => setChatId(e.target.value)}
          placeholder="blank = default recipient"
          disabled={submitting}
        />
      </label>

      <p className="hint">
        Keywords are comma-separated and matched against the listing title, case-insensitively.
        Leave “send to” blank to use the default chat. A bot can’t message someone who hasn’t
        pressed Start on it first.
      </p>

      {(validationError || error) && (
        <p className="form-error">{error ?? validationError}</p>
      )}

      <div className="watch-form-actions">
        <button type="button" className="secondary" onClick={onCancel} disabled={submitting}>
          Cancel
        </button>
        {/* Disabled while invalid or in flight, so a double-click can't create two watches */}
        <button type="submit" disabled={validationError !== null || submitting}>
          {submitting ? "Creating…" : "Create watch"}
        </button>
      </div>
    </form>
  );
}

function splitKeywords(value: string): string[] {
  return value
    .split(",")
    .map((k) => k.trim())
    .filter((k) => k.length > 0);
}
