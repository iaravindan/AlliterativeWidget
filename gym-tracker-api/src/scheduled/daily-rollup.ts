import type { Env } from '../index';
import { autoCloseStaleVisits } from '../services/sessionizer';
import { computeRollupsForRange } from '../services/rollup';

/**
 * Runs the daily rollup job
 * - Auto-closes stale visits (open for > 240 minutes)
 * - Recomputes rollups for the past 7 days to catch any updates
 */
export async function runDailyRollup(env: Env): Promise<{
  closedVisits: number;
  rollupsUpdated: number;
}> {
  console.log('Starting daily rollup job...');

  // Step 1: Auto-close stale visits
  const closedVisits = await autoCloseStaleVisits(env);
  console.log(`Auto-closed ${closedVisits} stale visits`);

  // Step 2: Recompute rollups for the past 7 days
  // This catches any visits that may have been added/modified
  const today = new Date();
  const endDate = today.toISOString().split('T')[0];

  const weekAgo = new Date(today);
  weekAgo.setDate(weekAgo.getDate() - 7);
  const startDate = weekAgo.toISOString().split('T')[0];

  const rollupsUpdated = await computeRollupsForRange(env, startDate, endDate);
  console.log(`Updated ${rollupsUpdated} rollups for ${startDate} to ${endDate}`);

  return { closedVisits, rollupsUpdated };
}
