// Where to go after logging in, taken from ?returnTo=... in the URL.
//
// Anyone can put anything in a URL, so a link like /login?returnTo=https://evil.example
// would send the user to another site right after they typed their password.
// Only same-site paths are allowed:
//   "/bookings/42"      allowed
//   "//evil.example"    rejected: browsers read "//host" as another site
//   "/\evil.example"    rejected: some browsers treat "\" like "/"
//   "https://..."       rejected: doesn't start with "/"
export function safeReturnPath(value: string | null): string {
  if (value === null || !value.startsWith('/') || value.startsWith('//') || value.startsWith('/\\')) {
    return '/';
  }

  // Going "back" to the login or register page after logging in would be a dead end.
  const path = value.split(/[?#]/)[0];
  if (path === '/login' || path === '/register') {
    return '/';
  }

  return value;
}

// "/login" + "/bookings/42" -> "/login?returnTo=%2Fbookings%2F42". Leaves the
// query off when the destination is just the home page.
export function withReturnTo(path: string, returnTo: string): string {
  return returnTo === '/' ? path : `${path}?${new URLSearchParams({ returnTo })}`;
}
