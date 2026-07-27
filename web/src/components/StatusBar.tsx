import type { Status } from "../api";

interface StatusBarProps {
  status: Status | null;
  onPollNow: () => void;
  polling: boolean;
}

export function StatusBar({ status, onPollNow, polling }: StatusBarProps) {
  return (
    <div className="statusbar">
      <div className="statusbar-facts">
        {status === null ? (
          <span className="muted">Connecting…</span>
        ) : (
          <>
            <span className={status.healthy ? "pill pill-ok" : "pill pill-bad"}>
              {status.healthy ? "healthy" : `${status.consecutiveFailures} failed poll(s)`}
            </span>
            <span>
              last poll{" "}
              <strong>
                {status.minutesSinceLastSuccess === null
                  ? "never"
                  : `${status.minutesSinceLastSuccess}m ago`}
              </strong>
            </span>
            <span>
              <strong>{status.activeWatches}</strong> watches
            </span>
            <span>
              <strong>{status.seenListings}</strong> listings seen
            </span>
            {status.lastError && <span className="error">{status.lastError}</span>}
          </>
        )}
      </div>

      {/* Disabled while a poll is in flight so an impatient double-click can't queue two. */}
      <button onClick={onPollNow} disabled={polling}>
        {polling ? "Polling…" : "Poll now"}
      </button>
    </div>
  );
}
