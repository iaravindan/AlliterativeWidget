-- Visits table: paired enter/exit events representing gym sessions
CREATE TABLE IF NOT EXISTS visits (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    enter_event_id INTEGER NOT NULL REFERENCES events(id),
    exit_event_id INTEGER REFERENCES events(id),    -- NULL if visit not yet closed
    enter_time TEXT NOT NULL,                        -- ISO 8601 UTC
    exit_time TEXT,                                  -- NULL if visit not yet closed
    duration_minutes INTEGER,                        -- Computed on close
    visit_date TEXT NOT NULL,                        -- YYYY-MM-DD in local timezone
    is_qualified INTEGER DEFAULT 0,                  -- 1 if duration >= 20 minutes
    auto_closed INTEGER DEFAULT 0                    -- 1 if closed by scheduled job
);

-- Index for finding open visits
CREATE INDEX IF NOT EXISTS idx_visits_open ON visits(exit_event_id) WHERE exit_event_id IS NULL;

-- Index for date-based queries
CREATE INDEX IF NOT EXISTS idx_visits_date ON visits(visit_date);

-- Index for qualified visits lookup
CREATE INDEX IF NOT EXISTS idx_visits_qualified ON visits(is_qualified, visit_date);
