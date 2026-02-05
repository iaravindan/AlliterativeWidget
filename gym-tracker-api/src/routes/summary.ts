import type { Env } from '../index';
import { validateAuth, unauthorizedResponse } from '../services/auth';
import { getRollupsForRange, ensureRollupsExist } from '../services/rollup';

interface WeekData {
  weekStart: string;  // Monday date
  days: DayData[];
}

interface DayData {
  date: string;
  dayOfWeek: number;  // 1=Monday, 5=Friday for workdays
  status: 'visit' | 'miss' | 'future' | 'excluded';
}

interface MonthLabel {
  month: string;      // "Jan", "Feb", etc.
  weekIndex: number;  // Starting column index
  weekSpan: number;   // Number of weeks this label covers
}

interface SummaryResponse {
  currentPeriod: {
    label: string;        // "This Week" or "This Month"
    visits: number;
    target: number;
    progressPercent: number;
  };
  heatmap: {
    weeks: number;
    grid: WeekData[];
    monthLabels: MonthLabel[];
  };
  stats: {
    totalVisits: number;
    totalMinutes: number;
    currentStreak: number;
    longestStreak: number;
  };
  generatedAt: string;
}

interface CountRow {
  count: number;
}

interface RollupStatusRow {
  roll_date: string;
  status: string;
}

/**
 * Gets the Monday of the week containing the given date
 */
function getMondayOfWeek(date: Date): Date {
  const d = new Date(date);
  const day = d.getDay();
  // Sunday = 0, so we need special handling
  const diff = day === 0 ? -6 : 1 - day;
  d.setDate(d.getDate() + diff);
  return d;
}

/**
 * Formats a date as YYYY-MM-DD
 */
function formatDate(date: Date): string {
  return date.toISOString().split('T')[0];
}

/**
 * Calculates the start date for the heatmap
 */
function getHeatmapStartDate(weeks: number): string {
  const today = new Date();
  const thisMonday = getMondayOfWeek(today);
  // Go back (weeks - 1) weeks, since current week is included
  thisMonday.setDate(thisMonday.getDate() - (weeks - 1) * 7);
  return formatDate(thisMonday);
}

/**
 * Calculates current period stats
 */
async function getCurrentPeriodStats(
  env: Env,
  mode: 'weekly' | 'monthly',
  target: number
): Promise<{ label: string; visits: number; target: number; progressPercent: number }> {
  const today = new Date();
  let startDate: string;
  let label: string;

  if (mode === 'weekly') {
    const monday = getMondayOfWeek(today);
    startDate = formatDate(monday);
    label = 'This Week';
  } else {
    // First day of current month
    const firstOfMonth = new Date(today.getFullYear(), today.getMonth(), 1);
    startDate = formatDate(firstOfMonth);
    label = 'This Month';
  }

  const endDate = formatDate(today);

  // Count qualified visits in the period
  const result = await env.DB.prepare(`
    SELECT COUNT(*) as count
    FROM visits
    WHERE visit_date >= ? AND visit_date <= ?
      AND is_qualified = 1
  `).bind(startDate, endDate).first() as CountRow | null;

  const visits = result?.count || 0;
  const progressPercent = target > 0 ? Math.round((visits / target) * 100) : 0;

  return { label, visits, target, progressPercent };
}

/**
 * Builds the heatmap grid from rollup data
 */
function buildHeatmapGrid(
  rollups: Array<{
    date: string;
    dayOfWeek: number;
    status: string;
  }>,
  weeks: number,
  startDate: string
): { grid: WeekData[]; monthLabels: MonthLabel[] } {
  const grid: WeekData[] = [];
  const monthLabels: MonthLabel[] = [];

  // Create week buckets
  const start = new Date(startDate + 'T12:00:00Z');
  let currentMonth = '';
  let currentMonthStartWeek = 0;
  let currentMonthWeeks = 0;

  for (let w = 0; w < weeks; w++) {
    const weekStart = new Date(start);
    weekStart.setDate(start.getDate() + w * 7);
    const weekStartStr = formatDate(weekStart);

    // Check for month label
    const monthStr = weekStart.toLocaleDateString('en-US', { month: 'short' });
    if (monthStr !== currentMonth) {
      // Save previous month label if exists
      if (currentMonth && currentMonthWeeks > 0) {
        monthLabels.push({
          month: currentMonth,
          weekIndex: currentMonthStartWeek,
          weekSpan: currentMonthWeeks,
        });
      }
      currentMonth = monthStr;
      currentMonthStartWeek = w;
      currentMonthWeeks = 1;
    } else {
      currentMonthWeeks++;
    }

    // Build days array for Mon-Fri (dayOfWeek 1-5)
    const days: DayData[] = [];
    for (let d = 0; d < 5; d++) {
      const dayDate = new Date(weekStart);
      dayDate.setDate(weekStart.getDate() + d);
      const dayDateStr = formatDate(dayDate);
      const dayOfWeek = d + 1; // 1=Monday, 5=Friday

      // Find rollup for this day
      const rollup = rollups.find(r => r.date === dayDateStr);
      const status = (rollup?.status || 'future') as DayData['status'];

      days.push({
        date: dayDateStr,
        dayOfWeek,
        status,
      });
    }

    grid.push({
      weekStart: weekStartStr,
      days,
    });
  }

  // Don't forget the last month
  if (currentMonth && currentMonthWeeks > 0) {
    monthLabels.push({
      month: currentMonth,
      weekIndex: currentMonthStartWeek,
      weekSpan: currentMonthWeeks,
    });
  }

  return { grid, monthLabels };
}

/**
 * Calculates streak statistics
 */
async function calculateStreaks(env: Env): Promise<{
  currentStreak: number;
  longestStreak: number;
}> {
  // Get all workday rollups ordered by date descending
  const rollups = await env.DB.prepare(`
    SELECT roll_date, status
    FROM daily_rollups
    WHERE is_workday = 1 AND status != 'future'
    ORDER BY roll_date DESC
  `).all();

  let currentStreak = 0;
  let longestStreak = 0;
  let tempStreak = 0;
  let countingCurrent = true;

  for (const row of rollups.results) {
    const rollup = row as unknown as RollupStatusRow;
    if (rollup.status === 'visit') {
      tempStreak++;
      if (countingCurrent) {
        currentStreak = tempStreak;
      }
    } else if (rollup.status === 'miss') {
      longestStreak = Math.max(longestStreak, tempStreak);
      tempStreak = 0;
      countingCurrent = false;
    }
  }

  longestStreak = Math.max(longestStreak, tempStreak);

  return { currentStreak, longestStreak };
}

/**
 * Handles GET /gym/summary
 */
export async function handleSummary(
  request: Request,
  env: Env,
  params: URLSearchParams
): Promise<Response> {
  // Validate auth
  if (!validateAuth(request, env, 'read')) {
    return unauthorizedResponse();
  }

  // Parse query parameters
  const mode = (params.get('mode') || 'weekly') as 'weekly' | 'monthly';
  const target = parseInt(params.get('target') || '4', 10);
  const startParam = params.get('start'); // Optional: YYYY-MM-DD start date

  // Calculate date range
  const today = formatDate(new Date());
  let startDate: string;
  let weeks: number;

  if (startParam) {
    // Use explicit start date and calculate weeks to cover until end of year
    startDate = startParam;
    const startMs = new Date(startParam + 'T12:00:00Z').getTime();
    const endOfYear = new Date(new Date().getFullYear(), 11, 31).getTime();
    weeks = Math.max(1, Math.ceil((endOfYear - startMs) / (7 * 24 * 60 * 60 * 1000)) + 1);
  } else {
    weeks = Math.min(52, Math.max(12, parseInt(params.get('weeks') || '12', 10)));
    startDate = getHeatmapStartDate(weeks);
  }

  // Ensure rollups exist only up to today (not future dates)
  await ensureRollupsExist(env, startDate, today);

  // Get rollup data up to today
  const rollups = await getRollupsForRange(env, startDate, today);

  // Build heatmap grid for the full range (including future weeks)
  const { grid, monthLabels } = buildHeatmapGrid(rollups, weeks, startDate);

  // Get current period stats
  const currentPeriod = await getCurrentPeriodStats(env, mode, target);

  // Calculate streaks
  const streaks = await calculateStreaks(env);

  // Calculate total stats
  const totalVisits = rollups.filter(r => r.status === 'visit').length;
  const totalMinutes = rollups.reduce((sum, r) => sum + r.totalMinutes, 0);

  const response: SummaryResponse = {
    currentPeriod,
    heatmap: {
      weeks,
      grid,
      monthLabels,
    },
    stats: {
      totalVisits,
      totalMinutes,
      currentStreak: streaks.currentStreak,
      longestStreak: streaks.longestStreak,
    },
    generatedAt: new Date().toISOString(),
  };

  return new Response(JSON.stringify(response), {
    headers: {
      'Content-Type': 'application/json',
      'Cache-Control': 'public, max-age=300',  // 5 minute cache
    },
  });
}
