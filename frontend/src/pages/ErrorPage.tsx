import { Link } from 'react-router';

// Shown by the router when a page crashes while rendering (a bug, not a failed
// API call — those are handled inside each page). Without it, one broken
// component would leave the whole screen blank.
export function ErrorPage() {
  return (
    <main className="page">
      <h1>Something went wrong</h1>
      <p>This page hit an unexpected problem. Please reload, or go back to the home page.</p>
      <Link to="/">Go to the home page</Link>
    </main>
  );
}
