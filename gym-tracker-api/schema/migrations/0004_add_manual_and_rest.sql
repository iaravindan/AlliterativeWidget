-- Add is_manual flag to prevent daily rollup from overwriting manual entries
ALTER TABLE daily_rollups ADD COLUMN is_manual INTEGER DEFAULT 0;

-- Update status constraint to include 'rest' status
-- SQLite doesn't support ALTER CONSTRAINT, so we need to recreate with trigger validation
-- The existing CHECK constraint will remain, and we'll handle 'rest' in application code

-- Create index for manual entries
CREATE INDEX IF NOT EXISTS idx_rollups_manual ON daily_rollups(is_manual) WHERE is_manual = 1;
