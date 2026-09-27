import { useMutation } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router';
import { ApiError } from '../api/types';
import { login } from '../api/sessions';
import { register, type RegisterUserRequest } from '../api/users';
import { formErrorMessage, serverFieldErrors, type FieldErrors } from '../auth/formErrors';
import { safeReturnPath, withReturnTo } from '../auth/returnTo';
import { useCurrentUser } from '../auth/session';
import { TextField } from '../components/TextField';
import './AuthPage.css';

// Same limits as the backend's RegisterUserRequestValidator. Checking here only
// gives quicker feedback; the server still checks everything.
const MIN_PASSWORD_LENGTH = 12;
const MAX_PASSWORD_LENGTH = 128;

const FORM_FIELDS = ['firstName', 'lastName', 'email', 'password'];

// Registering returns no tokens, so log straight in with the same details.
// If that second step fails (e.g. too many attempts), the account still exists,
// so we must not show "registration failed"; we send the user to log in instead.
async function registerAndLogIn(request: RegisterUserRequest): Promise<'loggedIn' | 'needsLogin'> {
  await register(request);
  try {
    await login({ email: request.email, password: request.password });
    return 'loggedIn';
  } catch {
    return 'needsLogin';
  }
}

export function RegisterPage() {
  const [searchParams] = useSearchParams();
  const returnTo = safeReturnPath(searchParams.get('returnTo'));
  const navigate = useNavigate();
  const user = useCurrentUser();

  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [clientErrors, setClientErrors] = useState<FieldErrors>({});

  const registerMutation = useMutation({
    mutationFn: registerAndLogIn,
    onSuccess: (result) => {
      if (result === 'needsLogin') {
        navigate(`/login?${new URLSearchParams({ registered: '1', returnTo })}`);
      }
    },
  });

  if (user !== null) {
    return <Navigate to={returnTo} replace />;
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const errors: FieldErrors = {};
    if (firstName.trim() === '') errors.firstName = ['First name is required.'];
    if (lastName.trim() === '') errors.lastName = ['Last name is required.'];
    if (email.trim() === '') errors.email = ['Email is required.'];
    if (password.length < MIN_PASSWORD_LENGTH) {
      errors.password = [`Password must be at least ${MIN_PASSWORD_LENGTH} characters.`];
    } else if (password.length > MAX_PASSWORD_LENGTH) {
      errors.password = [`Password must be ${MAX_PASSWORD_LENGTH} characters or fewer.`];
    }
    setClientErrors(errors);

    if (Object.keys(errors).length > 0) {
      registerMutation.reset();
      return;
    }
    registerMutation.mutate({
      firstName: firstName.trim(),
      lastName: lastName.trim(),
      email: email.trim(),
      password,
    });
  }

  const serverErrors = serverFieldErrors(registerMutation.error);
  const formError = registerMutation.isError ? formErrorMessage(registerMutation.error, FORM_FIELDS) : null;
  const emailTaken =
    registerMutation.error instanceof ApiError && registerMutation.error.errorCode === 'User.EmailAlreadyRegistered';

  return (
    <section className="auth-page">
      <h1>Create an account</h1>

      <form className="auth-form" onSubmit={handleSubmit} noValidate>
        {formError && (
          <p role="alert" className="error-box">
            {formError}{' '}
            {emailTaken && <Link to={withReturnTo('/login', returnTo)}>Log in instead</Link>}
          </p>
        )}

        <TextField
          label="First name"
          name="firstName"
          autoComplete="given-name"
          value={firstName}
          onChange={setFirstName}
          errors={clientErrors.firstName ?? serverErrors.firstName}
        />
        <TextField
          label="Last name"
          name="lastName"
          autoComplete="family-name"
          value={lastName}
          onChange={setLastName}
          errors={clientErrors.lastName ?? serverErrors.lastName}
        />
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
          autoComplete="new-password"
          hint={`At least ${MIN_PASSWORD_LENGTH} characters.`}
          value={password}
          onChange={setPassword}
          errors={clientErrors.password ?? serverErrors.password}
        />

        <button type="submit" className="button-primary" disabled={registerMutation.isPending}>
          {registerMutation.isPending ? 'Creating account…' : 'Create account'}
        </button>
      </form>

      <p>
        Already have an account? <Link to={withReturnTo('/login', returnTo)}>Log in</Link>
      </p>
    </section>
  );
}
