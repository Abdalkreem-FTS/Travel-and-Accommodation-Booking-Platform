import { ApiError } from '../api/types';

interface LoadErrorProps {
  // What failed to load, e.g. "the deals".
  what: string;
  error: Error;
  onRetry: () => void;
}

// The box shown when a query fails: the message, the traceId (so a bug report can
// be matched to the server logs) and a button to try again.
export function LoadError({ what, error, onRetry }: LoadErrorProps) {
  return (
    <div role="alert" className="error-box">
      <p>
        We couldn't load {what}. {error.message}
      </p>
      {error instanceof ApiError && error.traceId && <p className="muted">Reference: {error.traceId}</p>}
      <button type="button" onClick={onRetry}>
        Try again
      </button>
    </div>
  );
}
