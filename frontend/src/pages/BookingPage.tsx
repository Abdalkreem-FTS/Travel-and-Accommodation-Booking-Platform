import { useQuery } from '@tanstack/react-query';
import { Link, useLocation, useParams } from 'react-router';
import { getBooking, type Booking } from '../api/bookings';
import { ApiError } from '../api/types';
import { withReturnTo } from '../auth/returnTo';
import { useCurrentUser } from '../auth/session';
import { HotelName } from '../components/HotelName';
import { LoadError } from '../components/LoadError';
import { StayDetails } from '../components/StayDetails';
import { formatDate, formatDateTime, formatPrice } from '../format';
import { CancelBooking } from './booking/CancelBooking';
import './BookingPage.css';

// How often to ask the server again while something is still changing.
const POLL_INTERVAL_MS = 2_000;

// /bookings/:id
//
// Stripe sends the user here after paying. The booking is still Pending at that
// moment: the backend confirms it only when Stripe tells it the money arrived,
// a few seconds later. So while it's Pending this page asks again every 2 seconds.
export function BookingPage() {
  const { id = '' } = useParams();
  const user = useCurrentUser();
  const location = useLocation();

  if (user === null) {
    return (
      <section>
        <h1>Your booking</h1>
        <p>
          <Link to={withReturnTo('/login', location.pathname)}>Log in</Link> to see this booking.
        </p>
      </section>
    );
  }

  return <BookingDetails id={id} />;
}

function BookingDetails({ id }: { id: string }) {
  const { data: booking, dataUpdatedAt, isPending, isError, error, refetch } = useQuery({
    queryKey: ['bookings', id],
    queryFn: () => getBooking(id),
    retry: (failureCount, err) => !(err instanceof ApiError && err.status === 404) && failureCount < 3,
    // A function, so the interval is decided again after every answer: every
    // 2 seconds while something is still in progress, and not at all once it is
    // settled. TanStack Query also pauses it while the tab is hidden.
    refetchInterval: (query) => (isSettling(query.state.data) ? POLL_INTERVAL_MS : false),
  });

  if (isPending) {
    return <p role="status">Loading your booking…</p>;
  }

  if (isError) {
    if (error instanceof ApiError && error.status === 404) {
      return (
        <section>
          <h1>Booking not found</h1>
          <p>There's no booking of yours with this address.</p>
          <Link to="/">Go to the home page</Link>
        </section>
      );
    }
    return <LoadError what="your booking" error={error} onRetry={() => refetch()} />;
  }

  return (
    <article className="booking-page">
      <header>
        <h1>Booking {booking.confirmationNumber}</h1>
        <p>
          <HotelName hotelId={booking.hotelId} />
          <span className="muted">
            {' '}
            · {formatDate(booking.checkIn)} – {formatDate(booking.checkOut)}
          </span>
        </p>
      </header>

      <StatusBox booking={booking} checkedAt={dataUpdatedAt} />

      <section aria-labelledby="stays-heading">
        <h2 id="stays-heading">Stays</h2>
        <ul className="booking-lines">
          {booking.lines.map((line) => (
            <li key={line.lineNumber} className="booking-line">
              <StayDetails {...line} />
              <div className="booking-line-price">
                <span className="muted">{formatPrice(line.nightlyRate, booking.currency)} / night</span>
                {line.discount > 0 && (
                  <span className="booking-discount">−{formatPrice(line.discount, booking.currency)} deal</span>
                )}
                <strong>{formatPrice(line.lineTotal, booking.currency)}</strong>
              </div>
            </li>
          ))}
        </ul>
        <p className="booking-total">
          Total: <strong>{formatPrice(booking.totalAmount, booking.currency)}</strong>
        </p>
      </section>

      <RefundDetails booking={booking} />

      {(booking.status === 'Pending' || booking.status === 'Confirmed') && <CancelBooking booking={booking} />}
    </article>
  );
}

// Still waiting for Stripe: the payment (Pending) or the refund (Refunding).
function isSettling(booking: Booking | undefined): boolean {
  return booking?.status === 'Pending' || booking?.payment?.status === 'Refunding';
}

// What state the booking is in, and what the user can do about it.
// role="status" makes a screen reader announce the change when it happens.
//
// `checkedAt` is when the booking was last loaded (a millisecond timestamp).
// It stands in for "now" when deciding whether the user can still pay: a
// component should give the same output for the same props, and Date.now()
// would not. While Pending it moves forward every 2 seconds anyway.
function StatusBox({ booking, checkedAt }: { booking: Booking; checkedAt: number }) {
  const payment = booking.payment;

  switch (booking.status) {
    case 'Pending': {
      const canStillPay =
        payment?.checkoutUrl?.startsWith('https://') && new Date(payment.expiresAtUtc).getTime() > checkedAt;
      return (
        <div role="status" className="booking-status booking-status-pending">
          <p>
            <strong>Waiting for payment.</strong> This page updates by itself as soon as your payment goes through.
          </p>
          {payment && canStillPay && (
            <p>
              Your rooms are held until {formatDateTime(payment.expiresAtUtc)}.{' '}
              {/* A plain <a>, not <Link>: the payment page is on Stripe's site. */}
              <a href={payment.checkoutUrl ?? undefined}>Pay now</a>
            </p>
          )}
        </div>
      );
    }
    case 'Confirmed':
      return (
        <div role="status" className="booking-status booking-status-good">
          <p>
            <strong>Confirmed.</strong> Your payment went through and a confirmation email is on its way.
          </p>
        </div>
      );
    case 'Expired':
      return (
        <div role="status" className="booking-status">
          <p>
            <strong>Expired.</strong> The payment wasn't completed in time, so the rooms were released. Nothing was
            charged.
          </p>
          <Link to="/hotels">Find a hotel</Link>
        </div>
      );
    case 'Cancelled':
      return (
        <div role="status" className="booking-status">
          <p>
            <strong>Cancelled.</strong> The rooms were released.
          </p>
        </div>
      );
    case 'CheckedIn':
      return (
        <div role="status" className="booking-status booking-status-good">
          <p>
            <strong>Checked in.</strong> Enjoy your stay.
          </p>
        </div>
      );
    case 'Completed':
      return (
        <div role="status" className="booking-status">
          <p>
            <strong>Completed.</strong> We hope you enjoyed your stay.
          </p>
        </div>
      );
  }
}

// Only shown for a paid booking that was cancelled.
function RefundDetails({ booking }: { booking: Booking }) {
  const payment = booking.payment;
  const refund = payment?.refund;
  if (!payment || !refund) {
    return null;
  }

  const amount = formatPrice(refund.amount, refund.currency);
  return (
    <section aria-labelledby="refund-heading">
      <h2 id="refund-heading">Refund</h2>
      {payment.status === 'RefundFailed' ? (
        <p role="alert" className="error-box">
          We couldn't refund {amount}. Please contact us with your confirmation number, {booking.confirmationNumber}.
        </p>
      ) : refund.resolvedAtUtc === null ? (
        <p role="status">{amount} is being refunded to your card. This page updates when it's done.</p>
      ) : (
        <p role="status">
          {amount} was refunded to your card on {formatDateTime(refund.resolvedAtUtc)}.
        </p>
      )}
    </section>
  );
}
