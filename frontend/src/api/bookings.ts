import { apiGet, apiRequest } from './client';
import type { PagedList } from './types';

// The spec only says "string" for these two. The values come from the backend's
// BookingStatus and PaymentStatus enums.
export type BookingStatus = 'Pending' | 'Confirmed' | 'CheckedIn' | 'Completed' | 'Cancelled' | 'Expired';
export type PaymentStatus = 'Pending' | 'Succeeded' | 'Expired' | 'Refunding' | 'Refunded' | 'RefundFailed';

// One stay in a booking (BookingLineDto). The prices are a snapshot from checkout.
export interface BookingLine {
  lineNumber: number;
  roomId: string;
  // Dates without a time, e.g. "2026-09-30".
  checkIn: string;
  checkOut: string;
  nights: number;
  adults: number;
  children: number;
  // What the room lists at per night.
  nightlyRate: number;
  // What the deals took off the whole stay.
  discount: number;
  // What is due for this stay.
  lineTotal: number;
}

// Money going back to the guest after a paid booking is cancelled (RefundDto).
// `resolvedAtUtc` stays null until the payment provider has answered.
export interface Refund {
  amount: number;
  currency: string;
  requestedAtUtc: string;
  resolvedAtUtc: string | null;
}

// The payment of a booking (PaymentDto). `checkoutUrl` is the Stripe page where
// the guest pays; the nights are held for them until `expiresAtUtc`.
export interface Payment {
  id: string;
  status: PaymentStatus;
  amount: number;
  currency: string;
  checkoutUrl: string | null;
  expiresAtUtc: string;
  refund: Refund | null;
}

// A booking (BookingDto). `checkIn`/`checkOut` span all of its lines.
export interface Booking {
  id: string;
  hotelId: string;
  confirmationNumber: string;
  status: BookingStatus;
  checkIn: string;
  checkOut: string;
  totalAmount: number;
  currency: string;
  createdAtUtc: string;
  lines: BookingLine[];
  payment: Payment | null;
}

// One row of the "My bookings" list (BookingSummaryDto). `hotelName` is there
// even if the hotel was removed since; `rooms` is how many stays (lines) it has.
export interface BookingSummary {
  id: string;
  hotelId: string;
  hotelName: string;
  confirmationNumber: string;
  status: BookingStatus;
  checkIn: string;
  checkOut: string;
  rooms: number;
  totalAmount: number;
  currency: string;
  createdAtUtc: string;
}

// One stay to book (BookingItemRequest).
export interface BookingItem {
  roomId: string;
  checkIn: string;
  checkOut: string;
  adults: number;
  children: number;
}

// The answer to a cancellation (BookingCancellationDto). A paid booking comes
// back with a refund that is still being sent.
export interface BookingCancellation {
  bookingId: string;
  confirmationNumber: string;
  status: BookingStatus;
  reason: string;
  cancelledAtUtc: string;
  nightsReleased: number;
  refund: Refund | null;
}

// Reserves these stays (all at one hotel) and opens a payment page. Answers a
// Pending booking; send the guest to `payment.checkoutUrl`.
//
// `idempotencyKey` must be the same when the same checkout is sent again: if the
// first try did reserve a booking, the server answers with that booking instead
// of reserving a second one. Errors, by errorCode:
//   400 Booking.LinesSpanHotels, Booking.RoomCannotHostParty, bad dates
//   404 Room.NotFound
//   409 Booking.RoomUnavailable, Booking.PaymentPending, Idempotency.RequestInProgress
//   502 Payment.ProviderUnavailable (nothing was reserved)
export function createBooking(items: BookingItem[], idempotencyKey: string): Promise<Booking> {
  return apiRequest<Booking>('POST', '/api/bookings', {
    body: { items },
    auth: true,
    headers: { 'Idempotency-Key': idempotencyKey },
  });
}

// The logged-in user's bookings, newest first, in every status. A user with no
// bookings gets an empty page. `page` starts at 1; 400 if it's out of range.
export function listBookings(page: number): Promise<PagedList<BookingSummary>> {
  return apiGet<PagedList<BookingSummary>>(`/api/bookings?page=${page}`, { auth: true });
}

// One of the logged-in user's bookings. Someone else's booking is 404, like a
// booking that doesn't exist.
export function getBooking(id: string): Promise<Booking> {
  return apiGet<Booking>(`/api/bookings/${encodeURIComponent(id)}`, { auth: true });
}

// Cancels a booking and releases its nights; a paid one is refunded in full.
// Errors, by errorCode:
//   409 Booking.CancellationWindowClosed (less than 48 hours before check-in),
//       Booking.InvalidTransition (already cancelled, expired, ...),
//       Booking.PaymentJustCompleted, Persistence.ConcurrencyConflict
//   502 Payment.ProviderUnavailable (nothing changed)
export function cancelBooking(id: string): Promise<BookingCancellation> {
  return apiRequest<BookingCancellation>('POST', `/api/bookings/${encodeURIComponent(id)}/cancellation`, {
    auth: true,
  });
}
