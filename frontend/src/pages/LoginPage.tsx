import { useMutation } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { Link, Navigate, useSearchParams } from 'react-router';
import { login } from '../api/sessions';
import { formErrorMessage, serverFieldErrors, type FieldErrors } from '../auth/formErrors';
import { safeReturnPath, withReturnTo } from '../auth/returnTo';
import { useCurrentUser } from '../auth/session';
import { TextField } from '../components/TextField';
import './AuthPage.css';

export function LoginPage() {
  const [searchParams] = useSearchParams();
  const returnTo = safeReturnPath(searchParams.get('returnTo'));
  const justRegistered = searchParams.get('registered') === '1';
  const user = useCurrentUser();

  // State: values this component remembers between renders. Each setX call
  // stores a new value and makes React render the component again with it.
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [clientErrors, setClientErrors] = useState<FieldErrors>({});

  // useMutation is TanStack Query's tool for requests that change something.
  // It tracks isPending / isError / error for us.
  const loginMutation = useMutation({ mutationFn: login });

  // Once logged in (here, or in another tab), leave this page. No "on success"
  // code is needed: login() stores the session, useCurrentUser() sees it, and
  // this render sends the user on.
  if (user !== null) {
    return <Navigate to={returnTo} replace />;
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    // Stop the browser's own form submit, which would reload the page.
    event.preventDefault();

    const errors: FieldErrors = {};
    if (email.trim() === '') errors.email = ['Email is required.'];
    if (password === '') errors.password = ['Password is required.'];
    setClientErrors(errors);

    if (Object.keys(errors).length > 0) {
      loginMutation.reset();
      return;
    }
    loginMutation.mutate({ email: email.trim(), password });
  }

  const serverErrors = serverFieldErrors(loginMutation.error);
  const formError = loginMutation.isError ? formErrorMessage(loginMutation.error, ['email', 'password']) : null;

  return (
    <section className="auth-page">
      <h1>Log in</h1>

      {justRegistered && (
        <p role="status" className="auth-notice">
          Your account was created. Please log in.
        </p>
      )}

      <form className="auth-form" onSubmit={handleSubmit} noValidate>
        {formError && (
          <p role="alert" className="error-box">
            {formError}
          </p>
        )}

        <TextField
          label="Email"
          name="email"
          type="email"
          autoComplete="email"
          value={email}
          onChange={setEmail}
          errors={clientErrors.email ?? serverErrors.email}
        />
        <TextField
          label="Password"
          name="password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={setPassword}
          errors={clientErrors.password ?? serverErrors.password}
        />

        <button type="submit" className="button-primary" disabled={loginMutation.isPending}>
          {loginMutation.isPending ? 'Logging in…' : 'Log in'}
        </button>
      </form>

      <p>
        New here? <Link to={withReturnTo('/register', returnTo)}>Create an account</Link>
      </p>
    </section>
  );
}
