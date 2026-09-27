import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { useSearchParams } from 'react-router';
import { findUserByEmail, grantRole, revokeRole, type UserDto } from '../../api/users';
import { ApiError } from '../../api/types';
import { formErrorMessage, serverFieldErrors } from '../../auth/formErrors';
import { useCurrentUser } from '../../auth/session';
import { LoadError } from '../../components/LoadError';
import { TextField } from '../../components/TextField';
import './Admin.css';

// /admin/users?email=someone@example.com
//
// Find an account by email and make it an admin, or stop it being one. The
// email is in the URL so Back and a refresh show the same user.
export function AdminUsersPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const email = searchParams.get('email') ?? '';

  return (
    <section className="admin-page admin-form-page">
      <h1>Users</h1>
      <FindForm key={email} initial={email} onFind={(text) => setSearchParams(text === '' ? {} : { email: text })} />
      {email !== '' && <FoundUser email={email} />}
    </section>
  );
}

interface FindFormProps {
  initial: string;
  onFind: (email: string) => void;
}

function FindForm({ initial, onFind }: FindFormProps) {
  const [text, setText] = useState(initial);
  const [error, setError] = useState<string[]>([]);

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmed = text.trim();
    setError(trimmed === '' ? ['Enter an email address.'] : []);
    if (trimmed !== '') onFind(trimmed);
  };

  return (
    <form className="admin-search" role="search" onSubmit={handleSubmit} noValidate>
      <TextField label="Email" name="email" type="email" value={text} onChange={setText} errors={error} />
      <button type="submit">Find</button>
    </form>
  );
}

function FoundUser({ email }: { email: string }) {
  const { data: user, isPending, isError, error, refetch } = useQuery({
    queryKey: ['users', email],
    queryFn: () => findUserByEmail(email),
    // Roles change here and nowhere else in the app; always show them fresh.
    staleTime: 0,
  });

  if (isPending) {
    return <p role="status">Looking for the account…</p>;
  }
  if (isError) {
    // A malformed email is a 400 with errors.email. Show that message rather
    // than "couldn't load".
    const emailErrors = serverFieldErrors(error).email;
    if (emailErrors) {
      return (
        <p role="alert" className="error-box">
          {emailErrors.join(' ')}
        </p>
      );
    }
    return <LoadError what="the account" error={error} onRetry={() => refetch()} />;
  }
  if (user === null) {
    return <p className="muted">No account has the email {email}.</p>;
  }
  return <UserRoles user={user} email={email} />;
}

function UserRoles({ user, email }: { user: UserDto; email: string }) {
  const queryClient = useQueryClient();
  const me = useCurrentUser();
  const isMe = me?.id === user.id;
  const isAdmin = user.roles.includes('Admin');

  // After either change (or a failure that may have changed something), load
  // the user again so the roles on screen are the server's.
  const reloadUser = () => queryClient.invalidateQueries({ queryKey: ['users', email] });

  const grantMutation = useMutation({
    mutationFn: () => grantRole(user.id, 'Admin'),
    onSettled: reloadUser,
  });

  const revokeMutation = useMutation({
    mutationFn: () => revokeRole(user.id, 'Admin'),
    onSettled: reloadUser,
  });

  const handleRevoke = () => {
    const question =
      `Remove the admin role from ${user.firstName} ${user.lastName}? ` +
      'They will be signed out on every device right away.';
    if (window.confirm(question)) {
      grantMutation.reset();
      revokeMutation.mutate();
    }
  };

  const handleGrant = () => {
    revokeMutation.reset();
    grantMutation.mutate();
  };

  const failed = grantMutation.error ?? revokeMutation.error;
  const busy = grantMutation.isPending || revokeMutation.isPending;

  return (
    <article className="admin-user">
      <h2>
        {user.firstName} {user.lastName}
      </h2>
      <p className="muted">{user.email}</p>
      <p className="admin-roles">
        Roles:{' '}
        {user.roles.map((role) => (
          <span key={role} className="admin-status">
            {role}
          </span>
        ))}
      </p>

      {failed && (
        <p role="alert" className="error-box">
          {roleError(failed)}
        </p>
      )}
      {grantMutation.isSuccess && (
        <p role="status" className="admin-saved">
          Done. The admin role reaches them the next time their login is refreshed, within 15 minutes.
        </p>
      )}
      {revokeMutation.isSuccess && (
        <p role="status" className="admin-saved">
          Done. They have been signed out everywhere and are no longer an admin.
        </p>
      )}

      {isAdmin ? (
        isMe ? (
          <p className="muted">
            You can't remove your own admin role, because you couldn't undo it. Ask another administrator.
          </p>
        ) : (
          <button type="button" className="button-danger" onClick={handleRevoke} disabled={busy}>
            {revokeMutation.isPending ? 'Removing…' : 'Remove admin role'}
          </button>
        )
      ) : (
        <button type="button" className="button-primary" onClick={handleGrant} disabled={busy}>
          {grantMutation.isPending ? 'Granting…' : 'Make admin'}
        </button>
      )}
    </article>
  );
}

// The message for a refused grant or revoke, chosen by errorCode.
function roleError(error: Error): string {
  if (error instanceof ApiError) {
    switch (error.errorCode) {
      case 'User.CannotChangeYourOwnRoles':
        return "You can't remove a role from yourself. Ask another administrator.";
      case 'User.LastRoleCannotBeRevoked':
        return 'This is their only role, and every account must keep one.';
      case 'User.RoleNotGranted':
        return 'They are not an admin any more. Someone may have just changed it.';
      case 'User.RoleGrantRaced':
        return 'Another administrator granted this at the same moment. Try again; it will be a no-op.';
      case 'User.RoleRevocationIncomplete':
        // No "try again": the role is already gone, so a retry is refused with
        // User.RoleNotGranted and can't finish the job (the endpoint's OpenAPI
        // description says the same: wait out the window).
        return (
          'The admin role was removed and their sessions ended, but a login they already had ' +
          'may keep admin access for up to 15 minutes.'
        );
      case 'User.NotFound':
        return 'This account no longer exists.';
    }
    if (error.status === 401) {
      return error.message;
    }
    if (error.status === 403) {
      return 'Your account is no longer an administrator. Log out and in again.';
    }
  }
  return formErrorMessage(error, []) ?? 'Something went wrong. Please try again.';
}
