// Turning API values into text for the screen.

// 120.5 + "USD" -> "$120.50".
export function formatPrice(amount: number, currency: string): string {
  return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(amount);
}

// "2026-09-30" -> "30 Sep" (in the user's own date style).
//
// new Date("2026-09-30") means midnight UTC. Shown in a time zone behind UTC,
// that moment is still the 29th. The API sends a calendar day, not a moment,
// so we format it in UTC to get back exactly the day that was sent.
export function formatDate(dateOnly: string): string {
  return new Intl.DateTimeFormat(undefined, { day: 'numeric', month: 'short', timeZone: 'UTC' }).format(
    new Date(dateOnly),
  );
}

// "2026-09-30" -> "30 Sep 2026". For lists that can span years (an admin's
// list of past and future deals). Formatted in UTC for the same reason as formatDate.
export function formatDateWithYear(dateOnly: string): string {
  return new Intl.DateTimeFormat(undefined, {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(new Date(dateOnly));
}

// Today in the user's time zone, as "2026-09-27", for the date pickers' `min`.
export function todayIso(): string {
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

// "2026-09-27T14:05:00Z" -> "27 Sep, 17:05" in the user's own time zone. For
// moments (when a payment expires), unlike formatDate, which is for calendar days.
export function formatDateTime(isoDateTime: string): string {
  return new Intl.DateTimeFormat(undefined, {
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(isoDateTime));
}

// (2, 'adult') -> "2 adults", (1, 'child') -> "1 child".
export function guestsText(count: number, noun: 'adult' | 'child'): string {
  if (count === 1) return `1 ${noun}`;
  return `${count} ${noun === 'adult' ? 'adults' : 'children'}`;
}
