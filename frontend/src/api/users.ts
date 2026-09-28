import { apiGet, apiRequest } from './client';
import { ApiError } from './types';

// POST /api/users body (RegisterUserRequest in v1.json).
export interface RegisterUserRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}

// UserDto in v1.json.
export interface UserDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
}

// Creates the account only. It returns no tokens, so the caller logs in afterwards.
export function register(request: RegisterUserRequest): Promise<UserDto> {
  return apiRequest<UserDto>('POST', '/api/users', { body: request });
}

// --- Admin ---------------------------------------------------------------------

// The roles an admin can grant or revoke (the backend's UserRole enum).
export type Role = 'User' | 'Admin';

// The account with this email, or null when there is none (the API answers 404 User.NotFound). Exact match,
// ignoring case and spaces around it. Admins only.
// Answers 400 with errors.email when the email is missing or malformed.
export async function findUserByEmail(email: string): Promise<UserDto | null> {
  try {
    return await apiGet<UserDto>(`/api/users?email=${encodeURIComponent(email)}`, { auth: true });
  } catch (error) {
    if (error instanceof ApiError && error.errorCode === 'User.NotFound') {
      return null;
    }
    throw error;
  }
}

// Admins only. Granting a role the user already has changes nothing, so it's
// safe to retry. The user gets the role at their next token refresh.
export function grantRole(userId: string, role: Role): Promise<void> {
  return apiRequest<void>('PUT', `/api/users/${encodeURIComponent(userId)}/roles/${role}`, { auth: true });
}

// Admins only. Signs the user out on every device. Refused for your own
// account (403) and for a user's last role (409).
export function revokeRole(userId: string, role: Role): Promise<void> {
  return apiRequest<void>('DELETE', `/api/users/${encodeURIComponent(userId)}/roles/${role}`, { auth: true });
}
