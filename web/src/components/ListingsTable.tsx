import type { Listing } from "../api";

// A component is a function taking one argument (conventionally "props") and returning
// markup. This one owns no state — it renders whatever it is handed. Components like this
// are the easy kind: same input, same output, nothing to debug.
export function ListingsTable({ listings }: { listings: Listing[] }) {
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
        </tr>
      </thead>
      <tbody>
        {/* .map turns an array of data into an array of rows. React needs a stable "key"
            per row to tell them apart between renders — signature is our natural id. */}
        {sorted.map((listing) => (
          <tr key={listing.signature}>
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
          </tr>
        ))}
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
