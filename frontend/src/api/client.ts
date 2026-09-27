import { clearSession, getSession, setSession } from '../auth/session';
import type { SessionDto } from './sessions';
import { ApiError, type ProblemDetails } from './types';

// The only place in the app that calls fetch. Every function in src/api/
// goes through here, so auth headers, token refresh and error parsing live in one spot.

type Method = 'GET' | 'POST' | 'PUT' | 'DELETE';

interface RequestOptions {
  // Sent as JSON.
  body?: unknown;
  // true: send the access token (refreshed first if it is about to expire) and
  // fail with 401 when nobody is logged in.
  // 'if-logged-in': for endpoints anyone may call that do a little more for a
  // logged-in user. Sends the token when there is one, and nothing otherwise.
  auth?: boolean | 'if-logged-in';
  // Extra request headers, e.g. Idempotency-Key.
  headers?: Record<string, string>;
}

export async function apiRequest<T>(method: Method, url: string, options: RequestOptions = {}): Promise<T> {
  const withAuth = options.auth === true || (options.auth === 'if-logged-in' && getSession() !== null);
  const response = withAuth
    ? await sendWithAuth(method, url, options.body, options.headers)
    : await send(method, url, options.body, null, options.headers);

  if (!response.ok) {
    throw new ApiError(await readProblem(response));
  }
  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

// The If-Match header for an admin edit or delete. `version` is the one the
// record came with (the `version` field, which is also its ETag without the
// quotes). If someone saved the record since, the API answers 409.
export function ifMatch(version: string): Record<string, string> {
  return { 'If-Match': `"${version}"` };
}

export function apiGet<T>(url: string, options: Omit<RequestOptions, 'body'> = {}): Promise<T> {
  return apiRequest<T>('GET', url, options);
}

async function send(
  method: Method,
  url: string,
  body: unknown,
  accessToken: string | null,
  extraHeaders: Record<string, string> = {},
): Promise<Response> {
  const headers: Record<string, string> = { ...extraHeaders, Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (accessToken !== null) headers.Authorization = `Bearer ${accessToken}`;

  try {
    return await fetch(url, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    // fetch only throws when there is no response at all (server down, no network).
    throw new ApiError({ status: 0, detail: 'Could not reach the server. Check your connection and try again.' });
  }
}

// --- Authenticated requests ----------------------------------------------------

// Refresh when the access token has less than this left, so it can't expire
// while the request is travelling.
const EXPIRY_MARGIN_MS = 30_000;

async function sendWithAuth(
  method: Method,
  url: string,
  body: unknown,
  extraHeaders: Record<string, string> | undefined,
): Promise<Response> {
  let session = getSession();
  if (session === null) {
    throw new ApiError({ status: 401, detail: 'Please log in to continue.' });
  }

  if (new Date(session.accessTokenExpiresAtUtc).getTime() - Date.now() < EXPIRY_MARGIN_MS) {
    session = await refreshSession(session.refreshToken);
  }

  const response = await send(method, url, body, session.accessToken, extraHeaders);
  if (response.status !== 401) {
    return response;
  }

  // The server rejected a token we thought was fine (clocks disagree, or another
  // tab rotated it). Refresh once and try once more; a second 401 is final.
  const refreshed = await refreshSession(session.refreshToken);
  return send(method, url, body, refreshed.accessToken, extraHeaders);
}

// --- Refreshing: only one at a time ------------------------------------------
//
// The backend's refresh tokens are single use. If the same refresh token is ever
// sent twice, the backend assumes it was stolen and ends the session on every device.
// So we must never send one twice, even when several requests (or several tabs)
// notice the expired token at the same moment. Two guards:
//
// 1. Inside this tab: `refreshInFlight` holds the running refresh. Anyone else who
//    needs one waits for that same promise instead of starting a second.
// 2. Across tabs: navigator.locks is a lock shared by every tab of this site. Only one
//    tab can hold 'hotel-booking.refresh' at a time; the others queue behind it.
//    (Like a C# `lock`, but across browser tabs.)
//
// Inside the lock we look at localStorage again. If the refresh token there is no
// longer the one we were going to use, another tab already rotated it while we
// waited, so we use its result and don't call the API at all.

let refreshInFlight: Promise<SessionDto> | null = null;

function refreshSession(staleRefreshToken: string): Promise<SessionDto> {
  if (refreshInFlight === null) {
    const run = () => rotateUnlessAlreadyDone(staleRefreshToken);
    // navigator.locks only exists on https or localhost. Without it we still have
    // the in-tab guard; only the multi-tab protection is lost.
    const locked = 'locks' in navigator ? navigator.locks.request('hotel-booking.refresh', run) : run();
    refreshInFlight = locked.finally(() => {
      refreshInFlight = null;
    });
  }
  return refreshInFlight;
}

async function rotateUnlessAlreadyDone(staleRefreshToken: string): Promise<SessionDto> {
  const stored = getSession();
  if (stored === null) {
    throw new ApiError({ status: 401, detail: 'Your session has ended. Please log in again.' });
  }
  if (stored.refreshToken !== staleRefreshToken) {
    return stored;
  }

  const response = await send('PUT', '/api/sessions/current', { refreshToken: stored.refreshToken }, null);
  if (response.ok) {
    const session = (await response.json()) as SessionDto;
    setSession(session);
    return session;
  }

  const problem = await readProblem(response);
  // 400/401 mean the server will never accept this refresh token again: the
  // session is over. Anything else (429, 5xx) may pass, so keep the tokens.
  if (response.status === 400 || response.status === 401) {
    clearSession();
    throw new ApiError({ ...problem, detail: 'Your session has ended. Please log in again.' });
  }
  throw new ApiError(problem);
}

// --- Errors ------------------------------------------------------------------

// Error bodies should be ProblemDetails JSON, but a proxy or gateway can also
// answer with HTML or nothing at all, so fall back to just the status code.
async function readProblem(response: Response): Promise<ProblemDetails> {
  const retryAfter = Number(response.headers.get('Retry-After'));
  const retryAfterSeconds = retryAfter > 0 ? retryAfter : undefined;

  try {
    const body = (await response.json()) as ProblemDetails;
    return { ...body, status: response.status, retryAfterSeconds };
  } catch {
    return { status: response.status, retryAfterSeconds };
  }
}
