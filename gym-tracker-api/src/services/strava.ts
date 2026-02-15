import type { Env } from '../index';

interface StravaTokenRow {
  access_token: string;
  refresh_token: string;
  expires_at: number;
  athlete_id: number | null;
}

interface StravaTokenResponse {
  access_token: string;
  refresh_token: string;
  expires_at: number;
  athlete: { id: number };
}

interface StravaActivity {
  id: number;
  type: string;
  sport_type: string;
  start_date: string;
  distance: number;
  moving_time: number;
}

interface CyclingWeekRow {
  week_start: string;
  has_ride: number;
  total_rides: number;
  total_distance_meters: number;
  total_moving_time_seconds: number;
}

/**
 * Gets the Monday of the week containing the given date (ISO week, Monday start)
 */
function getMondayOfWeek(date: Date): Date {
  const d = new Date(date);
  const day = d.getDay();
  const diff = day === 0 ? -6 : 1 - day;
  d.setDate(d.getDate() + diff);
  return d;
}

function formatDate(date: Date): string {
  return date.toISOString().split('T')[0];
}

/**
 * Reads stored tokens and refreshes if within 5 minutes of expiry.
 * Returns a valid access token or null if unavailable.
 */
export async function getValidAccessToken(env: Env): Promise<string | null> {
  try {
    const row = await env.DB.prepare(
      'SELECT access_token, refresh_token, expires_at, athlete_id FROM strava_tokens WHERE id = 1'
    ).first() as StravaTokenRow | null;

    if (!row) {
      return null;
    }

    const now = Math.floor(Date.now() / 1000);
    // If token is still valid (with 5-minute buffer), return it
    if (row.expires_at > now + 300) {
      return row.access_token;
    }

    // Token expired or about to expire — refresh it
    if (!env.STRAVA_CLIENT_ID || !env.STRAVA_CLIENT_SECRET) {
      console.error('Strava client credentials not configured');
      return null;
    }

    const response = await fetch('https://www.strava.com/api/v3/oauth/token', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        client_id: env.STRAVA_CLIENT_ID,
        client_secret: env.STRAVA_CLIENT_SECRET,
        grant_type: 'refresh_token',
        refresh_token: row.refresh_token,
      }),
    });

    if (!response.ok) {
      console.error(`Strava token refresh failed: ${response.status}`);
      return null;
    }

    const data = await response.json() as StravaTokenResponse;

    await env.DB.prepare(`
      UPDATE strava_tokens
      SET access_token = ?, refresh_token = ?, expires_at = ?, updated_at = datetime('now')
      WHERE id = 1
    `).bind(data.access_token, data.refresh_token, data.expires_at).run();

    return data.access_token;
  } catch (err) {
    console.error('Error getting Strava access token:', err);
    return null;
  }
}

/**
 * Fetches recent activities from Strava, filtered to rides.
 * Returns empty array on any failure.
 */
export async function fetchRecentActivities(
  env: Env,
  afterEpoch: number
): Promise<StravaActivity[]> {
  try {
    const token = await getValidAccessToken(env);
    if (!token) return [];

    const activities: StravaActivity[] = [];
    let page = 1;
    const perPage = 100;

    while (true) {
      const url = `https://www.strava.com/api/v3/athlete/activities?after=${afterEpoch}&per_page=${perPage}&page=${page}`;
      const response = await fetch(url, {
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) {
        console.error(`Strava activities fetch failed: ${response.status}`);
        break;
      }

      const batch = await response.json() as StravaActivity[];
      if (batch.length === 0) break;

      // Filter for cycling activities
      const rides = batch.filter(
        a => a.type === 'Ride' || a.type === 'VirtualRide'
      );
      activities.push(...rides);

      if (batch.length < perPage) break;
      page++;
    }

    return activities;
  } catch (err) {
    console.error('Error fetching Strava activities:', err);
    return [];
  }
}

/**
 * Syncs cycling data into cycling_weekly table for the last N weeks.
 */
export async function syncCyclingWeekly(env: Env, weeksBack: number = 4): Promise<number> {
  const now = new Date();
  const startMonday = getMondayOfWeek(now);
  startMonday.setDate(startMonday.getDate() - (weeksBack - 1) * 7);

  const afterEpoch = Math.floor(new Date(formatDate(startMonday) + 'T00:00:00Z').getTime() / 1000);
  const activities = await fetchRecentActivities(env, afterEpoch);

  // Group activities by ISO week (Monday start)
  const weekMap = new Map<string, { rides: number; distance: number; movingTime: number }>();

  // Initialize all weeks in range
  for (let w = 0; w < weeksBack; w++) {
    const weekStart = new Date(startMonday);
    weekStart.setDate(startMonday.getDate() + w * 7);
    weekMap.set(formatDate(weekStart), { rides: 0, distance: 0, movingTime: 0 });
  }

  // Bucket activities into weeks
  for (const activity of activities) {
    const activityDate = new Date(activity.start_date);
    const monday = getMondayOfWeek(activityDate);
    const weekKey = formatDate(monday);

    const existing = weekMap.get(weekKey);
    if (existing) {
      existing.rides++;
      existing.distance += activity.distance || 0;
      existing.movingTime += activity.moving_time || 0;
    }
  }

  // Upsert into cycling_weekly
  let upserted = 0;
  for (const [weekStart, data] of weekMap) {
    await env.DB.prepare(`
      INSERT INTO cycling_weekly (week_start, has_ride, total_rides, total_distance_meters, total_moving_time_seconds, updated_at)
      VALUES (?, ?, ?, ?, ?, datetime('now'))
      ON CONFLICT(week_start) DO UPDATE SET
        has_ride = excluded.has_ride,
        total_rides = excluded.total_rides,
        total_distance_meters = excluded.total_distance_meters,
        total_moving_time_seconds = excluded.total_moving_time_seconds,
        updated_at = excluded.updated_at
    `).bind(weekStart, data.rides > 0 ? 1 : 0, data.rides, data.distance, data.movingTime).run();
    upserted++;
  }

  console.log(`Synced ${activities.length} rides across ${upserted} weeks`);
  return upserted;
}

/**
 * Queries cycling_weekly for a date range (used by the summary endpoint).
 */
export async function getCyclingWeeksForRange(
  env: Env,
  startDate: string,
  endDate: string
): Promise<Array<{ weekStart: string; hasRide: boolean; totalRides: number }>> {
  try {
    const results = await env.DB.prepare(`
      SELECT week_start, has_ride, total_rides
      FROM cycling_weekly
      WHERE week_start >= ? AND week_start <= ?
      ORDER BY week_start ASC
    `).bind(startDate, endDate).all();

    return (results.results as unknown as CyclingWeekRow[]).map(r => ({
      weekStart: r.week_start,
      hasRide: r.has_ride === 1,
      totalRides: r.total_rides,
    }));
  } catch (err) {
    console.error('Error querying cycling_weekly:', err);
    return [];
  }
}
