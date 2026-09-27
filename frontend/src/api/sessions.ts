import { clearSession, setSession } from '../auth/session';
import { apiRequest } from './client';

// POST /api/sessions body (LoginRequest in v1.json).
export interface LoginRequest {
  email: string;
  password: string;
}

// What login and refresh return (SessionDto in v1.json).
export interface SessionDto {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  tokenType?: string;
}

export async function login(request: LoginRequest): Promise<void> {
  const session = await apiRequest<SessionDto>('POST', '/api/sessions', { body: request });
  setSession(session);
}

// Tells the server to end the session, then forgets the tokens here. The local
// part happens even if the server call fails (offline, 503, session already
// gone): the user asked to be logged out, so this browser must not stay logged in.
export async function logout(): Promise<void> {
  try {
    await apiRequest<void>('DELETE', '/api/sessions/current', { auth: true });
  } catch {
    // Nothing useful to show; the tokens are dropped below either way.
  } finally {
    clearSession();
  }
}
