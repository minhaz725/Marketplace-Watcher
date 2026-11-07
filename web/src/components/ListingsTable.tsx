import { useState } from "react";
import { api, type Listing } from "../api";

interface ListingsTableProps {
  listings: Listing[];
  onChanged: () => void;
}

export function ListingsTable({ listings, onChanged }: ListingsTableProps) {
  // Signatures whose toggle is in flight. Tracking the set rather than a single id means
  // two quick clicks on different rows don't fight over one piece of state.
  const [pending, setPending] = useState<Set<string>>(new Set());

  async function toggleSold(listing: Listing) {
    setPending((prev) => new Set(prev).add(listing.signature));
    try {
      await api.toggleSold(listing.signature);
      onChanged();
    } finally {
      setPending((prev) => {
        const next = new Set(prev);
        next.delete(listing.signature);
        return next;
      });
    }
  }

  async function toggleContacted(listing: Listing) {
    // Prefix the key so a contacted toggle doesn't share a pending slot with a sold toggle.
    const key = `contacted:${listing.signature}`;
    setPending((prev) => new Set(prev).add(key));
    try {
      await api.toggleContacted(listing.signature);
      onChanged();
    } finally {
      setPending((prev) => {
        const next = new Set(prev);
        next.delete(key);
        return next;
      });
    }
  }

  if (listings.length === 0) {
    return <p className="empty">No listings yet. Add a watch and hit “Poll now”.</p>;
  }

  // Cheapest first. Copy before sorting — .sort() mutates in place, and mutating a prop
  // (which is React state upstream) is how you get renders that silently disagree.
  // Listings with no price sort last rather than as zero.
  const sorted = [...listings].sort((a, b) => {
    if (a.price === null) return 1;
    if (b.price === null) return -1;
    return a.price - b.price;
  });

  return (
    <table>
      <thead>
        <tr>
          <th>Title</th>
          <th>Price ↑</th>
          <th>Location</th>
          <th>Watch</th>
          <th>Seen</th>
          <th>Asked</th>
          <th>Sold</th>
        </tr>
      </thead>
      <tbody>
        {/* .map turns an array of data into an array of rows. React needs a stable "key"
            per row to tell them apart between renders — signature is our natural id. */}
        {sorted.map((listing) => {
          const isSold = listing.soldAt !== null;
          const isPending = pending.has(listing.signature);
          const isContacted = Boolean(listing.contactedAt); // Boolean(): also false for undefined from an older API
          const isContactPending = pending.has(`contacted:${listing.signature}`);

          return (
            <tr key={listing.signature} className={isSold ? "sold" : undefined}>
              <td>
                {listing.url ? (
                  <a href={listing.url} target="_blank" rel="noreferrer">
                    {listing.title}
                  </a>
                ) : (
                  listing.title
                )}
              </td>
              <td className="price">{formatPrice(listing.price)}</td>
              <td>{listing.location ?? "—"}</td>
              <td>{listing.watchName ?? "—"}</td>
              <td className="muted">{formatSeenAt(listing.seenAt)}</td>
              <td className="sold-cell">
                <button
                  type="button"
                  className={isContacted ? "icon-button contacted" : "icon-button"}
                  onClick={() => toggleContacted(listing)}
                  disabled={isContactPending}
                  title={isContacted ? "Clear asked / reached" : "Mark as asked / reached"}
                  aria-label={isContacted ? "Clear asked or reached" : "Mark as asked or reached"}
                  aria-pressed={isContacted}
                >
                  {isContacted ? "✓" : "○"}
                </button>
              </td>
              <td className="sold-cell">
                <button
                  type="button"
                  className="icon-button"
                  onClick={() => toggleSold(listing)}
                  disabled={isPending}
                  title={isSold ? "Mark as still available" : "Mark as sold / gone"}
                  aria-label={isSold ? "Mark as still available" : "Mark as sold or gone"}
                  aria-pressed={isSold}
                >
                  {isSold ? "↩" : "×"}
                </button>
              </td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}

function formatPrice(price: number | null): string {
  if (price === null) return "—";
  return price === 0 ? "FREE" : `NZ$${price.toLocaleString()}`;
}

function formatSeenAt(iso: string): string {
  // The API sends UTC without a trailing "Z", so Date would read it as local time.
  // Appending Z makes the parse explicit rather than depending on the browser's guess.
  const date = new Date(iso.endsWith("Z") ? iso : `${iso}Z`);
  const minutesAgo = Math.round((Date.now() - date.getTime()) / 60000);

  if (minutesAgo < 1) return "just now";
  if (minutesAgo < 60) return `${minutesAgo}m ago`;
  if (minutesAgo < 60 * 24) return `${Math.round(minutesAgo / 60)}h ago`;
  return date.toLocaleDateString();
}
