import { ApiError } from '../../api/types';
import { formErrorMessage } from '../../auth/formErrors';

// The message at the top of an admin form after a failed save or delete.
//
// `thing` names the record ("city", "hotel") for the messages every form
// shares. `messages` holds the form's own errorCodes. Returns null when every
// problem is already shown next to a field.
export function adminErrorMessage(
  error: Error,
  thing: string,
  formFields: string[],
  messages: Record<string, string> = {},
): string | null {
  if (!(error instanceof ApiError)) {
    return formErrorMessage(error, formFields);
  }

  if (error.errorCode !== undefined && error.errorCode in messages) {
    return messages[error.errorCode];
  }
  if (isStale(error)) {
    return `Someone else changed this ${thing} after you opened it. Load the latest version to see their changes, then make yours again.`;
  }
  if (error.status === 401) {
    // The client's own "Your session has ended" message.
    return error.message;
  }
  if (error.status === 403) {
    // The token says Admin, but the server no longer agrees (the role was revoked).
    return 'Your account is no longer an administrator. Log out and in again.';
  }
  if (error.status === 404 || error.status === 409) {
    // A code this form doesn't know yet. The API's detail is written for
    // people, which beats "Something went wrong".
    return error.message;
  }
  return formErrorMessage(error, formFields);
}

// True when the save was refused because the record changed since it was
// loaded. The form then offers to load the latest version.
export function isStale(error: Error | null): boolean {
  return error instanceof ApiError && error.errorCode === 'Persistence.ConcurrencyConflict';
}
