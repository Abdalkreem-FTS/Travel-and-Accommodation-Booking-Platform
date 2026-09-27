import { ApiError } from '../api/types';

// Field name -> messages. Same shape as the `errors` of a validation problem.
export type FieldErrors = Record<string, string[]>;

// The message shown at the top of the login and register forms, chosen by
// errorCode. Returns null when every problem is already shown next to a field.
export function formErrorMessage(error: unknown, formFields: string[]): string | null {
  if (!(error instanceof ApiError)) {
    return 'Something went wrong. Please try again.';
  }

  if (error.status === 0) {
    return error.message;
  }

  if (error.status === 429) {
    return error.retryAfterSeconds
      ? `Too many attempts. Try again in ${error.retryAfterSeconds} seconds.`
      : 'Too many attempts. Please wait a minute and try again.';
  }

  switch (error.errorCode) {
    case 'Auth.InvalidCredentials':
      return 'Email or password is incorrect.';
    case 'User.EmailAlreadyRegistered':
      return 'That email address is already registered.';
  }

  if (error.status === 400) {
    // Validation errors for fields on the form are shown next to them. Anything
    // the form has no field for would otherwise be invisible, so show it here.
    const other = Object.entries(error.errors)
      .filter(([field]) => !formFields.includes(field))
      .flatMap(([, messages]) => messages);
    if (Object.keys(error.errors).length > 0) {
      return other.length > 0 ? other.join(' ') : null;
    }
  }

  return error.traceId
    ? `Something went wrong. Please try again. (Reference: ${error.traceId})`
    : 'Something went wrong. Please try again.';
}

// Validation messages from the server, for the given field.
export function serverFieldErrors(error: unknown): FieldErrors {
  return error instanceof ApiError ? error.errors : {};
}
