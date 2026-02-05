-- Events table: stores raw Geofency enter/exit events
CREATE TABLE IF NOT EXISTS events (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    event_hash TEXT NOT NULL UNIQUE,  -- SHA256(timestamp+action+location) for deduplication
    timestamp TEXT NOT NULL,           -- ISO 8601 UTC timestamp
    action TEXT NOT NULL CHECK(action IN ('enter', 'exit')),
    location_name TEXT NOT NULL,
    raw_payload TEXT,                  -- Original JSON payload for debugging
    created_at TEXT DEFAULT (datetime('now'))
);

-- Index for efficient timestamp-based queries
CREATE INDEX IF NOT EXISTS idx_events_timestamp ON events(timestamp);

-- Index for finding events by action
CREATE INDEX IF NOT EXISTS idx_events_action ON events(action);

-- Index for location filtering
CREATE INDEX IF NOT EXISTS idx_events_location ON events(location_name);
