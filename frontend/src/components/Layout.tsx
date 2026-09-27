import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Link, Outlet, ScrollRestoration, useLocation, useNavigate } from 'react-router';
import { logout } from '../api/sessions';
import { withReturnTo } from '../auth/returnTo';
import { isAdmin, useCurrentUser } from '../auth/session';

// The frame around every page. <Outlet /> is where the router puts
// whichever page matches the current URL.
export function Layout() {
  const user = useCurrentUser();
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const logoutMutation = useMutation({
    mutationFn: logout,
    onSettled: () => {
      // Forget cached data from this user's session (bookings, cart, ...) so the
      // next person on this browser can't see it.
      queryClient.removeQueries();
      navigate('/');
    },
  });

  // After logging in, come back to the page the user was on.
  const here = location.pathname + location.search;

  return (
    <>
      <header className="site-header">
        <div className="site-header-start">
          <Link to="/" className="brand">
            Hotel Booking
          </Link>
          <nav aria-label="Main">
            <Link to="/hotels">Hotels</Link>
            {user && (
              <>
                <Link to="/cart">Cart</Link>
                <Link to="/bookings">My bookings</Link>
              </>
            )}
            {isAdmin(user) && <Link to="/admin">Admin</Link>}
          </nav>
        </div>
        <nav className="site-nav" aria-label="Account">
          {user ? (
            <>
              <span className="user-email" title={user.email}>
                {user.email}
              </span>
              <button type="button" onClick={() => logoutMutation.mutate()} disabled={logoutMutation.isPending}>
                {logoutMutation.isPending ? 'Logging out…' : 'Log out'}
              </button>
            </>
          ) : (
            <>
              <Link to={withReturnTo('/login', here)}>Log in</Link>
              <Link to={withReturnTo('/register', here)}>Register</Link>
            </>
          )}
        </nav>
      </header>
      <main className="page">
        <Outlet />
      </main>
      {/* Opening a new page (or the next page of results) starts at the top, and
          Back returns to where the user had scrolled. */}
      <ScrollRestoration />
    </>
  );
}
