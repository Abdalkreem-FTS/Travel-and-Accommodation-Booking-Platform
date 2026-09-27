import { Link } from 'react-router';
import './Pagination.css';

interface PaginationProps {
  page: number;
  totalPages: number;
  // The link for a given page number. The caller decides how a page goes into
  // the URL, so this component works for any list.
  hrefFor: (page: number) => string;
}

// "← Previous   Page 2 of 7   Next →". Real links rather than buttons, so a page
// can be opened in a new tab and the browser's Back button steps through pages.
export function Pagination({ page, totalPages, hrefFor }: PaginationProps) {
  if (totalPages <= 1) {
    return null;
  }

  return (
    <nav className="pagination" aria-label="Pages">
      {page > 1 ? (
        <Link to={hrefFor(page - 1)} rel="prev">
          ← Previous
        </Link>
      ) : (
        // A link that goes nowhere confuses keyboard and screen-reader users, so
        // on the first page this is plain text.
        <span className="muted" aria-hidden="true">
          ← Previous
        </span>
      )}
      <span aria-current="page">
        Page {page} of {totalPages}
      </span>
      {page < totalPages ? (
        <Link to={hrefFor(page + 1)} rel="next">
          Next →
        </Link>
      ) : (
        <span className="muted" aria-hidden="true">
          Next →
        </span>
      )}
    </nav>
  );
}
