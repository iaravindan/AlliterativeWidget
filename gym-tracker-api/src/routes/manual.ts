import type { Env } from '../index';
import { validateAuth, unauthorizedResponse } from '../services/auth';
import { setManualEntry } from '../services/rollup';

interface ManualEntryRequest {
  date: string;      // YYYY-MM-DD
  status: 'visit' | 'miss';
}

interface BulkManualRequest {
  entries: ManualEntryRequest[];
}

/**
 * Handles POST /gym/manual
 * Sets manual entries that won't be overwritten by the daily rollup
 */
export async function handleManualEntry(
  request: Request,
  env: Env
): Promise<Response> {
  // Validate auth - requires write access
  if (!validateAuth(request, env, 'write')) {
    return unauthorizedResponse();
  }

  try {
    const body = await request.json() as BulkManualRequest;

    if (!body.entries || !Array.isArray(body.entries)) {
      return new Response(
        JSON.stringify({ error: 'Invalid request: entries array required' }),
        { status: 400, headers: { 'Content-Type': 'application/json' } }
      );
    }

    // Validate all entries
    for (const entry of body.entries) {
      if (!entry.date || !/^\d{4}-\d{2}-\d{2}$/.test(entry.date)) {
        return new Response(
          JSON.stringify({ error: `Invalid date format: ${entry.date}. Expected YYYY-MM-DD` }),
          { status: 400, headers: { 'Content-Type': 'application/json' } }
        );
      }
      if (!['visit', 'miss'].includes(entry.status)) {
        return new Response(
          JSON.stringify({ error: `Invalid status: ${entry.status}. Expected 'visit' or 'miss'` }),
          { status: 400, headers: { 'Content-Type': 'application/json' } }
        );
      }
    }

    // Insert manual entries
    for (const entry of body.entries) {
      await setManualEntry(env, entry.date, entry.status);
    }

    return new Response(
      JSON.stringify({
        success: true,
        message: `${body.entries.length} manual entries set`,
        entries: body.entries,
      }),
      { headers: { 'Content-Type': 'application/json' } }
    );
  } catch (error) {
    return new Response(
      JSON.stringify({
        error: 'Failed to parse request',
        message: error instanceof Error ? error.message : 'Unknown error',
      }),
      { status: 400, headers: { 'Content-Type': 'application/json' } }
    );
  }
}
