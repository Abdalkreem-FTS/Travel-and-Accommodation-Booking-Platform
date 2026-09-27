import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Link, useLocation, useSearchParams } from 'react-router';
import { listBookings, type BookingStatus, type BookingSummary } from '../api/bookings';
import { withReturnTo } from '../auth/returnTo';
import { useCurrentUser } from '../auth/session';
import { LoadError } from '../components/LoadError';
import { Pagination } from '../components/Pagination';
import { formatDate, formatPrice } from '../format';
import './MyBookingsPage.css';

const STATUS_LABELS: Record<BookingStatus, string> = {
  Pending: 'Waiting for payment',
  Confirmed: 'Confirmed',
  CheckedIn: 'Checked in',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
  Expired: 'Expired',
};

// /bookings?page=2
//
// Every booking the logged-in user has made, newest first. Each row links to
// /bookings/:id, which has the stays, the payment and the cancel button.
export function MyBookingsPage() {
  const user = useCurrentUser();
  const location = useLocation();

  if (user === null) {
    return (
      <section>
        <h1>My bookings</h1>
        <p>
          <Link to={withReturnTo('/login', location.pathname + location.search)}>Log in</Link> to see your bookings.
        </p>
      </section>
    );
  }

  return <BookingList />;
}

function BookingList() {
  const [searchParams] = useSearchParams();
  const page = readPage(searchParams.get('page'));

  const { data, isPending, isError, error, isPlaceholderData, refetch } = useQuery({
    queryKey: ['bookings', 'list', page],
    queryFn: () => listBookings(page),
    placeholderData: keepPreviousData,
    // A status changes on the server (a payment goes through, a booking expires)
    // and after a cancel on the booking page. So load the list again every time
    // the page is opened, instead of trusting a copy up to 30 seconds old.
    staleTime: 0,
  });

  return (
    <section className="my-bookings" aria-busy={isPlaceholderData}>
      <h1>My bookings</h1>

      {isPending ? (
        <p role="status">Loading your bookings…</p>
      ) : isError ? (
        <LoadError what="your bookings" error={error} onRetry={() => refetch()} />
      ) : data.totalCount === 0 ? (
        <p className="muted">
          You haven't booked anything yet. <Link to="/hotels">Find a hotel</Link>.
        </p>
      ) : data.items.length === 0 ? (
        // The URL asks for a page past the last one.
        <p className="muted">
          There is no page {data.page}. <Link to="/bookings">Go to page 1</Link>.
        </p>
      ) : (
        <>
          <ul className="my-bookings-list">
            {data.items.map((booking) => (
              <li key={booking.id}>
                <BookingRow booking={booking} />
              </li>
            ))}
          </ul>
          <Pagination
            page={data.page}
            totalPages={data.totalPages}
            hrefFor={(n) => (n === 1 ? '/bookings' : `/bookings?page=${n}`)}
          />
        </>
      )}
    </section>
  );
}

function BookingRow({ booking }: { booking: BookingSummary }) {
  return (
    <article className="my-booking">
      <div>
        <h2 className="my-booking-hotel">
          <Link to={`/bookings/${booking.id}`}>{booking.hotelName}</Link>
        </h2>
        <p className="muted">
          {formatDate(booking.checkIn)} – {formatDate(booking.checkOut)} · {roomsText(booking.rooms)} ·{' '}
          {booking.confirmationNumber}
        </p>
      </div>
      <div className="my-booking-side">
        <span className={`booking-badge booking-badge-${booking.status.toLowerCase()}`}>
          {STATUS_LABELS[booking.status]}
        </span>
        <strong>{formatPrice(booking.totalAmount, booking.currency)}</strong>
      </div>
    </article>
  );
}

// "page=abc" or "page=0" in the URL means page 1, like no page at all.
function readPage(value: string | null): number {
  const page = Number(value);
  return Number.isInteger(page) && page >= 1 ? page : 1;
}

function roomsText(rooms: number): string {
  return rooms === 1 ? '1 room' : `${rooms} rooms`;
}
