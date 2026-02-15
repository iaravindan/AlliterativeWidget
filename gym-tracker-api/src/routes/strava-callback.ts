import type { Env } from '../index';

interface StravaTokenResponse {
  access_token: string;
  refresh_token: string;
  expires_at: number;
  athlete: { id: number };
}

/**
 * Handles GET /strava/callback?code=XXX&state=WRITE_TOKEN
 * Exchanges the authorization code for tokens and stores them.
 */
export async function handleStravaCallback(
  request: Request,
  env: Env,
  params: URLSearchParams
): Promise<Response> {
  const code = params.get('code');
  const state = params.get('state');

  if (!code) {
    return new Response('Missing authorization code', { status: 400 });
  }

  // CSRF protection: state must match WRITE_TOKEN
  if (state !== env.WRITE_TOKEN) {
    return new Response('Invalid state parameter', { status: 403 });
  }

  if (!env.STRAVA_CLIENT_ID || !env.STRAVA_CLIENT_SECRET) {
    return new Response('Strava client credentials not configured', { status: 500 });
  }

  try {
    const response = await fetch('https://www.strava.com/api/v3/oauth/token', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        client_id: env.STRAVA_CLIENT_ID,
        client_secret: env.STRAVA_CLIENT_SECRET,
        code,
        grant_type: 'authorization_code',
      }),
    });

    if (!response.ok) {
      const body = await response.text();
      console.error(`Strava token exchange failed: ${response.status} ${body}`);
      return new Response(`Token exchange failed: ${response.status}`, { status: 502 });
    }

    const data = await response.json() as StravaTokenResponse;

    // Store tokens (single-row table, id=1)
    await env.DB.prepare(`
      INSERT OR REPLACE INTO strava_tokens (id, access_token, refresh_token, expires_at, athlete_id, updated_at)
      VALUES (1, ?, ?, ?, ?, datetime('now'))
    `).bind(data.access_token, data.refresh_token, data.expires_at, data.athlete.id).run();

    return new Response(
      `<!DOCTYPE html>
<html><body style="font-family:system-ui;text-align:center;padding:60px">
<h1>Strava Connected</h1>
<p>Athlete ID: ${data.athlete.id}</p>
<p>You can close this tab.</p>
</body></html>`,
      { headers: { 'Content-Type': 'text/html' } }
    );
  } catch (err) {
    console.error('Strava callback error:', err);
    return new Response('Internal error during token exchange', { status: 500 });
  }
}
