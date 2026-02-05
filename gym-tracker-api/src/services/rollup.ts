import type { Env } from '../index';

type DayStatus = 'visit' | 'miss' | 'future' | 'excluded';

interface VisitRow {
  is_qualified: number;
  duration_minutes: number;
}

interface RollupRow {
  roll_date: string;
  day_of_week: number;
  is_workday: number;
  status: string;
  qualified_visits: number;
  total_minutes: number;
}

interface DateRow {
  roll_date: string;
}

/**
 * Gets the day of week (0=Sunday, 1=Monday, ..., 6=Saturday)
 */
function getDayOfWeek(dateStr: string): number {
  return new Date(dateStr + 'T12:00:00Z').getDay();
}

/**
 * Checks if a day is a workday (Monday-Friday)
 */
function isWorkday(dayOfWeek: number): boolean {
  return dayOfWeek >= 1 && dayOfWeek <= 5;
}

/**
 * Determines the status for a given date
 */
async function computeDayStatus(
  env: Env,
  date: string,
  today: string
): Promise<{ status: DayStatus; qualifiedVisits: number; totalMinutes: number }> {
  const dayOfWeek = getDayOfWeek(date);

  // Weekend days are excluded
  if (!isWorkday(dayOfWeek)) {
    return { status: 'excluded', qualifiedVisits: 0, totalMinutes: 0 };
  }

  // Future days
  if (date > today) {
    return { status: 'future', qualifiedVisits: 0, totalMinutes: 0 };
  }

  // Query visits for this date
  const visits = await env.DB.prepare(`
    SELECT is_qualified, duration_minutes
    FROM visits
    WHERE visit_date = ?
      AND exit_time IS NOT NULL
  `).bind(date).all();

  const visitRows = visits.results as unknown as VisitRow[];
  const qualifiedVisits = visitRows.filter(v => v.is_qualified === 1).length;
  const totalMinutes = visitRows.reduce(
    (sum, v) => sum + (v.duration_minutes || 0),
    0
  );

  // If there's at least one qualified visit, mark as visit
  if (qualifiedVisits > 0) {
    return { status: 'visit', qualifiedVisits, totalMinutes };
  }

  // Past workday with no qualified visit is a miss
  return { status: 'miss', qualifiedVisits: 0, totalMinutes };
}

/**
 * Computes and stores rollup for a single date
 */
export async function computeRollupForDate(env: Env, date: string, today: string): Promise<void> {
  const dayOfWeek = getDayOfWeek(date);
  const workday = isWorkday(dayOfWeek);
  const { status, qualifiedVisits, totalMinutes } = await computeDayStatus(env, date, today);

  // Upsert the rollup
  await env.DB.prepare(`
    INSERT INTO daily_rollups (roll_date, day_of_week, is_workday, status, qualified_visits, total_minutes)
    VALUES (?, ?, ?, ?, ?, ?)
    ON CONFLICT(roll_date) DO UPDATE SET
      status = excluded.status,
      qualified_visits = excluded.qualified_visits,
      total_minutes = excluded.total_minutes
  `).bind(date, dayOfWeek, workday ? 1 : 0, status, qualifiedVisits, totalMinutes).run();
}

/**
 * Computes rollups for a date range
 * Used by the scheduled worker to backfill and update
 */
export async function computeRollupsForRange(
  env: Env,
  startDate: string,
  endDate: string
): Promise<number> {
  const today = new Date().toISOString().split('T')[0];
  let count = 0;

  // Iterate through each date in range
  const current = new Date(startDate + 'T12:00:00Z');
  const end = new Date(endDate + 'T12:00:00Z');

  while (current <= end) {
    const dateStr = current.toISOString().split('T')[0];
    await computeRollupForDate(env, dateStr, today);
    count++;
    current.setDate(current.getDate() + 1);
  }

  return count;
}

/**
 * Gets rollup data for the summary API
 */
export async function getRollupsForRange(
  env: Env,
  startDate: string,
  endDate: string
): Promise<Array<{
  date: string;
  dayOfWeek: number;
  isWorkday: boolean;
  status: DayStatus;
  qualifiedVisits: number;
  totalMinutes: number;
}>> {
  const results = await env.DB.prepare(`
    SELECT roll_date, day_of_week, is_workday, status, qualified_visits, total_minutes
    FROM daily_rollups
    WHERE roll_date >= ? AND roll_date <= ?
    ORDER BY roll_date ASC
  `).bind(startDate, endDate).all();

  return (results.results as unknown as RollupRow[]).map(r => ({
    date: r.roll_date,
    dayOfWeek: r.day_of_week,
    isWorkday: r.is_workday === 1,
    status: r.status as DayStatus,
    qualifiedVisits: r.qualified_visits,
    totalMinutes: r.total_minutes,
  }));
}

/**
 * Generates rollup entries for dates that don't exist yet
 * Fills in historical data and future dates within the range
 */
export async function ensureRollupsExist(
  env: Env,
  startDate: string,
  endDate: string
): Promise<void> {
  const today = new Date().toISOString().split('T')[0];

  // Get existing rollups
  const existing = await env.DB.prepare(`
    SELECT roll_date FROM daily_rollups WHERE roll_date >= ? AND roll_date <= ?
  `).bind(startDate, endDate).all();

  const existingDates = new Set((existing.results as unknown as DateRow[]).map(r => r.roll_date));

  // Iterate through range and fill missing
  const current = new Date(startDate + 'T12:00:00Z');
  const end = new Date(endDate + 'T12:00:00Z');

  while (current <= end) {
    const dateStr = current.toISOString().split('T')[0];
    if (!existingDates.has(dateStr)) {
      await computeRollupForDate(env, dateStr, today);
    }
    current.setDate(current.getDate() + 1);
  }
}
