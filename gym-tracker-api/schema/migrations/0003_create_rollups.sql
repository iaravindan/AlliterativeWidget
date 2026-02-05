-- Daily rollups table: pre-computed daily status for heatmap display
CREATE TABLE IF NOT EXISTS daily_rollups (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    roll_date TEXT NOT NULL UNIQUE,                  -- YYYY-MM-DD
    day_of_week INTEGER NOT NULL,                    -- 0=Sunday, 1=Monday, ..., 6=Saturday
    is_workday INTEGER NOT NULL,                     -- 1 if Monday-Friday
    status TEXT NOT NULL CHECK(status IN ('visit', 'miss', 'future', 'excluded')),
    qualified_visits INTEGER DEFAULT 0,              -- Count of visits >= 20 min
    total_minutes INTEGER DEFAULT 0                  -- Sum of all visit durations
);

-- Index for date range queries
CREATE INDEX IF NOT EXISTS idx_rollups_date ON daily_rollups(roll_date);

-- Index for workday filtering
CREATE INDEX IF NOT EXISTS idx_rollups_workday ON daily_rollups(is_workday, roll_date);
