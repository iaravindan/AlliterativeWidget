import type { Env } from '../index';
import { validateAuth, unauthorizedResponse } from '../services/auth';
import { sessionizeVisit } from '../services/sessionizer';

interface GeofencyPayload {
  // Standard Geofency fields
  name?: string;           // Location name
  entry?: string;          // "1" for enter, "0" for exit
  date?: string;           // ISO timestamp
  // Alternative field names
  location?: string;
  action?: 'enter' | 'exit';
  timestamp?: string;
}

/**
 * Generates a SHA-256 hash for event deduplication
 */
async function generateEventHash(timestamp: string, action: string, location: string): Promise<string> {
  const data = `${timestamp}|${action}|${location}`;
  const encoder = new TextEncoder();
  const hashBuffer = await crypto.subtle.digest('SHA-256', encoder.encode(data));
  const hashArray = Array.from(new Uint8Array(hashBuffer));
  return hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
}

/**
 * Parses the Geofency webhook payload into a normalized format
 */
function parseGeofencyPayload(payload: GeofencyPayload): {
  timestamp: string;
  action: 'enter' | 'exit';
  locationName: string
} | null {
  // Handle location name
  const locationName = payload.name || payload.location;
  if (!locationName) {
    return null;
  }

  // Handle action - Geofency uses entry="1" for enter, entry="0" for exit
  let action: 'enter' | 'exit';
  if (payload.action) {
    action = payload.action;
  } else if (payload.entry !== undefined) {
    action = payload.entry === '1' ? 'enter' : 'exit';
  } else {
    return null;
  }

  // Handle timestamp
  const timestamp = payload.date || payload.timestamp;
  if (!timestamp) {
    return null;
  }

  // Normalize timestamp to ISO 8601 UTC
  const parsedDate = new Date(timestamp);
  if (isNaN(parsedDate.getTime())) {
    return null;
  }

  return {
    timestamp: parsedDate.toISOString(),
    action,
    locationName,
  };
}

/**
 * Handles POST /ingest/geofency
 * Receives Geofency enter/exit webhooks and stores them
 */
export async function handleIngest(request: Request, env: Env): Promise<Response> {
  // Validate auth
  if (!validateAuth(request, env, 'write')) {
    return unauthorizedResponse();
  }

  // Parse request body
  let payload: GeofencyPayload;
  try {
    const contentType = request.headers.get('Content-Type') || '';

    if (contentType.includes('application/json')) {
      payload = await request.json();
    } else if (contentType.includes('application/x-www-form-urlencoded')) {
      // Geofency often sends form data
      const formData = await request.formData();
      payload = Object.fromEntries(formData.entries()) as unknown as GeofencyPayload;
    } else {
      // Try JSON anyway
      payload = await request.json();
    }
  } catch (error) {
    return new Response(
      JSON.stringify({ error: 'Bad request', message: 'Invalid request body' }),
      { status: 400, headers: { 'Content-Type': 'application/json' } }
    );
  }

  // Parse and validate payload
  const parsed = parseGeofencyPayload(payload);
  if (!parsed) {
    return new Response(
      JSON.stringify({
        error: 'Bad request',
        message: 'Missing required fields: name/location, entry/action, date/timestamp'
      }),
      { status: 400, headers: { 'Content-Type': 'application/json' } }
    );
  }

  const { timestamp, action, locationName } = parsed;

  // Generate deduplication hash
  const eventHash = await generateEventHash(timestamp, action, locationName);

  // Check for duplicate
  const existing = await env.DB.prepare(
    'SELECT id FROM events WHERE event_hash = ?'
  ).bind(eventHash).first();

  if (existing) {
    return new Response(
      JSON.stringify({
        status: 'duplicate',
        message: 'Event already recorded',
        event_hash: eventHash
      }),
      { status: 200, headers: { 'Content-Type': 'application/json' } }
    );
  }

  // Insert new event
  const result = await env.DB.prepare(
    `INSERT INTO events (event_hash, timestamp, action, location_name, raw_payload)
     VALUES (?, ?, ?, ?, ?)`
  ).bind(
    eventHash,
    timestamp,
    action,
    locationName,
    JSON.stringify(payload)
  ).run();

  const eventId = result.meta.last_row_id;

  // Trigger sessionization
  const sessionResult = await sessionizeVisit(env, eventId as number, timestamp, action, locationName);

  return new Response(
    JSON.stringify({
      status: 'created',
      event_id: eventId,
      event_hash: eventHash,
      action,
      timestamp,
      location: locationName,
      session: sessionResult,
    }),
    { status: 201, headers: { 'Content-Type': 'application/json' } }
  );
}
