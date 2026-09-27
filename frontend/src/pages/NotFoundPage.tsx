import { Link } from 'react-router';

export function NotFoundPage() {
  return (
    <section>
      <h1>Page not found</h1>
      <p>There's nothing at this address.</p>
      <Link to="/">Go to the home page</Link>
    </section>
  );
}
