import { useSyncExternalStore } from 'react';
import type { SessionDto } from '../api/sessions';

// Where the logged-in session lives, and who the current user is.
//
// The tokens are kept in localStorage so that every tab shares one session and a
// page reload doesn't log the user out. This file never calls the API; it only
// stores, reads and announces changes. Refreshing tokens is done in api/client.ts.

const STORAGE_KEY = 'hotel-booking.session';

// What the app needs to know about the logged-in user. Read from the access
// token (a JWT), because the API has no "who am I" endpoint.
export interface CurrentUser {
  id: string;
  email: string;
  roles: string[];
}

// Whether to show the admin pages. Only a convenience: the token could be
// edited in the browser, and the API checks the role on every admin request.
export function isAdmin(user: CurrentUser | null): boolean {
  return user !== null && user.roles.includes('Admin');
}

// Always read from localStorage rather than a variable: another tab may have
// refreshed the tokens a moment ago, and localStorage is the one copy all tabs share.
export function getSession(): SessionDto | null {
  let session: SessionDto;
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw === null) return null;
    session = JSON.parse(raw) as SessionDto;
  } catch {
    return null;
  }

  // Once the refresh token has expired the session can't be renewed, so it is over.
  if (new Date(session.refreshTokenExpiresAtUtc).getTime() <= Date.now()) {
    return null;
  }
  return session;
}

export function setSession(session: SessionDto): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  sessionChanged();
}

export function clearSession(): void {
  localStorage.removeItem(STORAGE_KEY);
  sessionChanged();
}

// --- Telling React about changes -------------------------------------------
//
// React components can't see a plain variable change. useSyncExternalStore is
// React's way to read data that lives outside React: we give it a `subscribe`
// function (call me back when something changes) and a `getSnapshot` function
// (what is the value right now). It re-renders the component when the value changes.

let currentUser: CurrentUser | null = userFromSession(getSession());
const listeners = new Set<() => void>();

function sessionChanged(): void {
  currentUser = userFromSession(getSession());
  listeners.forEach((listener) => listener());
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

// The browser fires `storage` in every *other* tab when localStorage changes,
// so logging in or out in one tab updates the header in all of them.
window.addEventListener('storage', (event) => {
  if (event.key === STORAGE_KEY || event.key === null) {
    sessionChanged();
  }
});

// The logged-in user, or null. Components that use this re-render on login,
// logout, token refresh, and when another tab does any of those.
export function useCurrentUser(): CurrentUser | null {
  return useSyncExternalStore(subscribe, () => currentUser);
}

// --- Reading the access token ------------------------------------------------
//
// A JWT is three base64url parts separated by dots: header.payload.signature.
// The payload is plain JSON anyone can read (only the signature is secret-backed),
// so no library is needed. We only read it for display and navigation; the
// backend checks the signature on every request.

interface AccessTokenPayload {
  sub: string;
  email: string;
  // A JWT claim with a single value may arrive as a string instead of an array.
  role?: string | string[];
}

function userFromSession(session: SessionDto | null): CurrentUser | null {
  if (session === null) return null;
  try {
    const payload = decodeJwtPayload(session.accessToken);
    const roles = payload.role === undefined ? [] : [payload.role].flat();
    return { id: payload.sub, email: payload.email, roles };
  } catch {
    return null;
  }
}

function decodeJwtPayload(token: string): AccessTokenPayload {
  const base64Url = token.split('.')[1];
  const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
  // atob gives one character per byte; TextDecoder turns the bytes back into
  // UTF-8 text so non-English characters survive.
  const bytes = Uint8Array.from(atob(base64), (char) => char.charCodeAt(0));
  return JSON.parse(new TextDecoder().decode(bytes)) as AccessTokenPayload;
}
