import { handleIngest } from './routes/ingest';
import { handleSummary } from './routes/summary';
import { handleManualEntry } from './routes/manual';
import { handleStravaCallback } from './routes/strava-callback';
import { runDailyRollup } from './scheduled/daily-rollup';
import { syncCyclingWeekly } from './services/strava';
import { validateAuth, unauthorizedResponse } from './services/auth';

export interface Env {
  DB: D1Database;
  WRITE_TOKEN: string;
  READ_TOKEN: string;
  TIMEZONE: string;
  STRAVA_CLIENT_ID: string;
  STRAVA_CLIENT_SECRET: string;
}

export default {
  async fetch(request: Request, env: Env, ctx: ExecutionContext): Promise<Response> {
    const url = new URL(request.url);
    const path = url.pathname;

    // CORS headers for widget requests
    const corsHeaders = {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type, X-Auth-Token',
    };

    // Handle preflight requests
    if (request.method === 'OPTIONS') {
      return new Response(null, { headers: corsHeaders });
    }

    try {
      // Route dispatch
      if (path === '/ingest/geofency' && request.method === 'POST') {
        const response = await handleIngest(request, env);
        return addCorsHeaders(response, corsHeaders);
      }

      if (path === '/gym/summary' && request.method === 'GET') {
        const response = await handleSummary(request, env, url.searchParams);
        return addCorsHeaders(response, corsHeaders);
      }

      if (path === '/gym/manual' && request.method === 'POST') {
        const response = await handleManualEntry(request, env);
        return addCorsHeaders(response, corsHeaders);
      }

      if (path === '/strava/callback' && request.method === 'GET') {
        const response = await handleStravaCallback(request, env, url.searchParams);
        return addCorsHeaders(response, corsHeaders);
      }

      if (path === '/strava/sync' && request.method === 'POST') {
        if (!validateAuth(request, env, 'write')) {
          return addCorsHeaders(unauthorizedResponse(), corsHeaders);
        }
        const body = await request.json().catch(() => ({})) as { weeksBack?: number };
        const weeksBack = body.weeksBack || 4;
        const count = await syncCyclingWeekly(env, weeksBack);
        return addCorsHeaders(
          new Response(JSON.stringify({ ok: true, weeksSynced: count }), {
            headers: { 'Content-Type': 'application/json' },
          }),
          corsHeaders
        );
      }

      // Health check endpoint
      if (path === '/health') {
        return addCorsHeaders(
          new Response(JSON.stringify({ status: 'ok', timestamp: new Date().toISOString() }), {
            headers: { 'Content-Type': 'application/json' },
          }),
          corsHeaders
        );
      }

      return addCorsHeaders(
        new Response(JSON.stringify({ error: 'Not found' }), {
          status: 404,
          headers: { 'Content-Type': 'application/json' },
        }),
        corsHeaders
      );
    } catch (error) {
      console.error('Request error:', error);
      return addCorsHeaders(
        new Response(
          JSON.stringify({
            error: 'Internal server error',
            message: error instanceof Error ? error.message : 'Unknown error',
          }),
          {
            status: 500,
            headers: { 'Content-Type': 'application/json' },
          }
        ),
        corsHeaders
      );
    }
  },

  async scheduled(event: ScheduledEvent, env: Env, ctx: ExecutionContext): Promise<void> {
    console.log('Running scheduled daily rollup at', new Date().toISOString());
    await runDailyRollup(env);
  },
};

function addCorsHeaders(response: Response, corsHeaders: Record<string, string>): Response {
  const newHeaders = new Headers(response.headers);
  for (const [key, value] of Object.entries(corsHeaders)) {
    newHeaders.set(key, value);
  }
  return new Response(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers: newHeaders,
  });
}
