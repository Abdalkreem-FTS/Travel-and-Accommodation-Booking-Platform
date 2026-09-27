import { Link, NavLink, Outlet, useLocation } from 'react-router';
import '../pages/admin/Admin.css';
import { withReturnTo } from './returnTo';
import { isAdmin, useCurrentUser } from './session';

// Wraps every /admin/... page. Admins see the page (<Outlet />); anyone else is
// told why not. This only saves them a page of 403 errors: the API is what
// actually refuses a non-admin.
export function RequireAdmin() {
  const user = useCurrentUser();
  const location = useLocation();

  if (user === null) {
    return (
      <section>
        <h1>Admin</h1>
        <p>
          <Link to={withReturnTo('/login', location.pathname + location.search)}>Log in</Link> with an admin account to
          continue.
        </p>
      </section>
    );
  }

  if (!isAdmin(user)) {
    return (
      <section>
        <h1>Admin</h1>
        <p>This page is for administrators only.</p>
        <Link to="/">Go to the home page</Link>
      </section>
    );
  }

  // The admin area's own menu above every admin page. NavLink is a Link that
  // knows when its page is open: it adds aria-current="page", which the CSS
  // uses to underline the current section.
  return (
    <>
      <nav className="admin-nav" aria-label="Admin">
        <NavLink to="/admin/cities">Cities</NavLink>
        <NavLink to="/admin/users">Users</NavLink>
      </nav>
      <Outlet />
    </>
  );
}
