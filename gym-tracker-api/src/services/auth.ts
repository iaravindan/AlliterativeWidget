import type { Env } from '../index';

export type AuthLevel = 'read' | 'write' | 'none';

/**
 * Extracts the token from either X-Auth-Token header or HTTP Basic Auth.
 * For Basic Auth, the token is expected as the password field.
 */
function extractToken(request: Request): string | null {
  // Try X-Auth-Token header first (widget uses this)
  const authToken = request.headers.get('X-Auth-Token');
  if (authToken) {
    return authToken;
  }

  // Try HTTP Basic Auth (Geofency uses this)
  const authHeader = request.headers.get('Authorization');
  if (authHeader?.startsWith('Basic ')) {
    try {
      const decoded = atob(authHeader.slice(6));
      const colonIndex = decoded.indexOf(':');
      if (colonIndex !== -1) {
        // Token is in the password field
        return decoded.slice(colonIndex + 1);
      }
    } catch {
      // Invalid base64
    }
  }

  return null;
}

/**
 * Validates auth via X-Auth-Token header or HTTP Basic Auth (password field).
 * @param request - The incoming request
 * @param env - Environment bindings
 * @param requiredLevel - The minimum auth level required
 * @returns true if authorized, false otherwise
 */
export function validateAuth(
  request: Request,
  env: Env,
  requiredLevel: AuthLevel
): boolean {
  if (requiredLevel === 'none') {
    return true;
  }

  const token = extractToken(request);

  if (!token) {
    return false;
  }

  // Write token has access to both read and write endpoints
  if (token === env.WRITE_TOKEN) {
    return true;
  }

  // Read token only has access to read endpoints
  if (requiredLevel === 'read' && token === env.READ_TOKEN) {
    return true;
  }

  return false;
}

/**
 * Creates an unauthorized response
 */
export function unauthorizedResponse(): Response {
  return new Response(
    JSON.stringify({ error: 'Unauthorized', message: 'Invalid or missing X-Auth-Token header' }),
    {
      status: 401,
      headers: { 'Content-Type': 'application/json' },
    }
  );
}
