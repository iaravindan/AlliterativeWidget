-- Strava OAuth tokens (single-row table, id=1 always)
CREATE TABLE IF NOT EXISTS strava_tokens (
    id INTEGER PRIMARY KEY DEFAULT 1 CHECK (id = 1),
    access_token TEXT NOT NULL,
    refresh_token TEXT NOT NULL,
    expires_at INTEGER NOT NULL,
    athlete_id INTEGER,
    updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Weekly cycling aggregates (one row per ISO week, Monday start)
CREATE TABLE IF NOT EXISTS cycling_weekly (
    week_start TEXT PRIMARY KEY,  -- YYYY-MM-DD (Monday)
    has_ride INTEGER NOT NULL DEFAULT 0,
    total_rides INTEGER NOT NULL DEFAULT 0,
    total_distance_meters REAL NOT NULL DEFAULT 0,
    total_moving_time_seconds INTEGER NOT NULL DEFAULT 0,
    updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);
