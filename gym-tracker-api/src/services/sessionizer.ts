import type { Env } from '../index';

// Configuration constants
const MIN_VISIT_DURATION_MINUTES = 20;
const DEDUP_WINDOW_HOURS = 3;
const AUTO_CLOSE_MINUTES = 240;

interface SessionResult {
  action: 'visit_started' | 'visit_closed' | 'visit_ignored' | 'no_open_visit';
  visitId?: number;
  duration?: number;
  isQualified?: boolean;
  reason?: string;
}

interface OpenVisitRow {
  id: number;
  enter_time: string;
  enter_event_id: number;
}

interface StaleVisitRow {
  id: number;
  enter_time: string;
}

/**
 * Gets the local date (YYYY-MM-DD) for a UTC timestamp
 * Uses the configured timezone or defaults to local
 */
function getLocalDate(utcTimestamp: string, timezone?: string): string {
  const date = new Date(utcTimestamp);
  // For now, use UTC date - timezone handling can be enhanced
  // In production, you'd use Intl.DateTimeFormat with timezone
  if (timezone) {
    try {
      const formatter = new Intl.DateTimeFormat('en-CA', {
        timeZone: timezone,
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
      });
      return formatter.format(date);
    } catch {
      // Fall back to UTC
    }
  }
  return date.toISOString().split('T')[0];
}

/**
 * Calculates duration in minutes between two ISO timestamps
 */
function calculateDuration(enterTime: string, exitTime: string): number {
  const enter = new Date(enterTime).getTime();
  const exit = new Date(exitTime).getTime();
  return Math.round((exit - enter) / (1000 * 60));
}

/**
 * Checks if this is a rapid re-entry within the dedup window
 */
async function isRapidReentry(env: Env, locationName: string, enterTime: string): Promise<boolean> {
  const windowStart = new Date(
    new Date(enterTime).getTime() - DEDUP_WINDOW_HOURS * 60 * 60 * 1000
  ).toISOString();

  // Check for recent exits from the same location
  const recentExit = await env.DB.prepare(`
    SELECT v.id, v.exit_time
    FROM visits v
    JOIN events e ON v.enter_event_id = e.id
    WHERE e.location_name = ?
      AND v.exit_time IS NOT NULL
      AND v.exit_time > ?
    ORDER BY v.exit_time DESC
    LIMIT 1
  `).bind(locationName, windowStart).first();

  return recentExit !== null;
}

/**
 * Processes a new event and creates/closes visits as appropriate
 */
export async function sessionizeVisit(
  env: Env,
  eventId: number,
  timestamp: string,
  action: 'enter' | 'exit',
  locationName: string
): Promise<SessionResult> {
  const timezone = env.TIMEZONE || 'UTC';

  if (action === 'enter') {
    // Check for rapid re-entry
    if (await isRapidReentry(env, locationName, timestamp)) {
      return {
        action: 'visit_ignored',
        reason: `Rapid re-entry within ${DEDUP_WINDOW_HOURS} hours`,
      };
    }

    // Check if there's already an open visit
    const openVisit = await env.DB.prepare(`
      SELECT v.id
      FROM visits v
      JOIN events e ON v.enter_event_id = e.id
      WHERE e.location_name = ?
        AND v.exit_event_id IS NULL
      LIMIT 1
    `).bind(locationName).first();

    if (openVisit) {
      return {
        action: 'visit_ignored',
        reason: 'Visit already in progress',
      };
    }

    // Create new visit
    const visitDate = getLocalDate(timestamp, timezone);
    const result = await env.DB.prepare(`
      INSERT INTO visits (enter_event_id, enter_time, visit_date)
      VALUES (?, ?, ?)
    `).bind(eventId, timestamp, visitDate).run();

    return {
      action: 'visit_started',
      visitId: result.meta.last_row_id as number,
    };
  } else {
    // Exit action - find and close the open visit
    const openVisit = await env.DB.prepare(`
      SELECT v.id, v.enter_time, v.enter_event_id
      FROM visits v
      JOIN events e ON v.enter_event_id = e.id
      WHERE e.location_name = ?
        AND v.exit_event_id IS NULL
      ORDER BY v.enter_time DESC
      LIMIT 1
    `).bind(locationName).first() as OpenVisitRow | null;

    if (!openVisit) {
      return {
        action: 'no_open_visit',
        reason: 'No open visit found to close',
      };
    }

    const duration = calculateDuration(openVisit.enter_time, timestamp);
    const isQualified = duration >= MIN_VISIT_DURATION_MINUTES ? 1 : 0;

    // Update visit with exit info
    await env.DB.prepare(`
      UPDATE visits
      SET exit_event_id = ?,
          exit_time = ?,
          duration_minutes = ?,
          is_qualified = ?
      WHERE id = ?
    `).bind(eventId, timestamp, duration, isQualified, openVisit.id).run();

    return {
      action: 'visit_closed',
      visitId: openVisit.id,
      duration,
      isQualified: isQualified === 1,
    };
  }
}

/**
 * Auto-closes visits that have been open for too long
 * Called by the scheduled worker
 */
export async function autoCloseStaleVisits(env: Env): Promise<number> {
  const cutoff = new Date(
    Date.now() - AUTO_CLOSE_MINUTES * 60 * 1000
  ).toISOString();

  // Find open visits older than cutoff
  const staleVisits = await env.DB.prepare(`
    SELECT id, enter_time
    FROM visits
    WHERE exit_event_id IS NULL
      AND enter_time < ?
  `).bind(cutoff).all();

  let closedCount = 0;

  for (const row of staleVisits.results) {
    const visit = row as unknown as StaleVisitRow;
    // Calculate exit time as enter + AUTO_CLOSE_MINUTES
    const exitTime = new Date(
      new Date(visit.enter_time).getTime() + AUTO_CLOSE_MINUTES * 60 * 1000
    ).toISOString();

    const duration = AUTO_CLOSE_MINUTES;
    const isQualified = duration >= MIN_VISIT_DURATION_MINUTES ? 1 : 0;

    await env.DB.prepare(`
      UPDATE visits
      SET exit_time = ?,
          duration_minutes = ?,
          is_qualified = ?,
          auto_closed = 1
      WHERE id = ?
    `).bind(exitTime, duration, isQualified, visit.id).run();

    closedCount++;
  }

  return closedCount;
}

export { MIN_VISIT_DURATION_MINUTES, AUTO_CLOSE_MINUTES };
