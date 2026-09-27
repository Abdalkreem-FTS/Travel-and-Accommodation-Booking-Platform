// Shapes shared by many endpoints. Field names match v1.json.

// Every list endpoint returns one page of results wrapped like this.
export interface PagedList<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

// The body of every non-2xx response (RFC 9457 ProblemDetails).
// `errors` is only present on validation failures: field name -> messages.
export interface ProblemDetails {
  status: number;
  title?: string;
  detail?: string;
  errorCode?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
  // Not part of the body: copied from the Retry-After header of a 429.
  retryAfterSeconds?: number;
}

// Thrown by the API client when a request fails, so pages can read
// `errorCode` and decide what to show.
export class ApiError extends Error {
  readonly status: number;
  readonly errorCode: string | undefined;
  readonly traceId: string | undefined;
  readonly errors: Record<string, string[]>;
  readonly retryAfterSeconds: number | undefined;

  constructor(problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `Request failed with status ${problem.status}`);
    this.name = 'ApiError';
    this.status = problem.status;
    this.errorCode = problem.errorCode;
    this.traceId = problem.traceId;
    this.errors = problem.errors ?? {};
    this.retryAfterSeconds = problem.retryAfterSeconds;
  }
}
