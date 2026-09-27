import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { createBooking, type Booking } from '../../api/bookings';
import { removeCartItem, type CartItem } from '../../api/cart';
import { ApiError } from '../../api/types';
import { formErrorMessage } from '../../auth/formErrors';
import { HotelName } from '../../components/HotelName';
import { StayDetails } from '../../components/StayDetails';
import { formatPrice } from '../../format';

interface CheckoutGroupProps {
  hotelId: string;
  // Never empty: the cart page only makes a group for a hotel it has items for.
  items: CartItem[];
}

// One hotel's stays in the cart, its total and its Check out button.
export function CheckoutGroup({ hotelId, items }: CheckoutGroupProps) {
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  // The Idempotency-Key of this checkout. useState with a function runs the
  // function once, when the component first appears, and keeps the value across
  // re-renders (like a field set in a C# constructor).
  //
  // Pressing "Check out" again after a failure sends the SAME key. If the first
  // try did reserve a booking and only the answer was lost (network error), the
  // server sees the key and answers with that booking instead of reserving and
  // charging a second one. The server keeps a key only when it reserves a
  // booking, so after a real failure (room taken) the same key simply tries again.
  //
  // It must change when the stays change, because a key that already reserved
  // booking X always answers X, whatever items come with it. The cart page puts
  // the item ids in this component's `key`, so different items = new component
  // = new idempotency key.
  const [idempotencyKey] = useState(() => crypto.randomUUID());

  const checkout = useMutation({
    mutationFn: () =>
      createBooking(
        items.map(({ roomId, checkIn, checkOut, adults, children }) => ({
          roomId,
          checkIn,
          checkOut,
          adults,
          children,
        })),
        idempotencyKey,
      ),
    onSuccess: (booking) => {
      // The server dropped the booked stays from the cart.
      queryClient.invalidateQueries({ queryKey: ['cart'] });
      goToPayment(booking);
    },
  });

  function goToPayment(booking: Booking) {
    const url = booking.payment?.checkoutUrl;
    // The payment page is on Stripe's site, so this leaves the app with a full
    // page load (navigate() only moves between our own pages). Only an https
    // link is followed. A booking that is no longer waiting for payment (the
    // server replayed an older checkout) goes to its own page instead.
    if (booking.status === 'Pending' && url && url.startsWith('https://')) {
      window.location.assign(url);
    } else {
      navigate(`/bookings/${booking.id}`);
    }
  }

  const total = items.reduce((sum, item) => sum + item.total, 0);
  const currency = items[0].currency;
  // After success the button stays disabled while the browser leaves for Stripe.
  const busy = checkout.isPending || checkout.isSuccess;

  return (
    <section className="cart-group" aria-label="Stays at one hotel">
      <h2>
        <HotelName hotelId={hotelId} />
      </h2>

      <ul className="cart-items">
        {items.map((item) => (
          <li key={item.id} className="cart-item">
            <StayDetails {...item} />
            <div className="cart-item-price">
              <span className="muted">{formatPrice(item.nightlyRate, item.currency)} / night</span>
              {item.discount > 0 && (
                <span className="cart-discount">−{formatPrice(item.discount, item.currency)} deal</span>
              )}
              <strong>{formatPrice(item.total, item.currency)}</strong>
              <RemoveButton itemId={item.id} disabled={busy} />
            </div>
          </li>
        ))}
      </ul>

      <div className="cart-checkout">
        <p className="cart-total">
          Total: <strong>{formatPrice(total, currency)}</strong>
        </p>
        <p className="muted">
          Prices are checked again when you check out. The rooms are then held for 30 minutes while you pay.
        </p>
        <button type="button" className="button-primary" onClick={() => checkout.mutate()} disabled={busy}>
          {checkout.isPending ? 'Reserving…' : checkout.isSuccess ? 'Opening payment…' : 'Check out'}
        </button>
        {checkout.isError && (
          <p role="alert" className="cart-error">
            {checkoutError(checkout.error)}
          </p>
        )}
      </div>
    </section>
  );
}

function RemoveButton({ itemId, disabled }: { itemId: string; disabled: boolean }) {
  const queryClient = useQueryClient();
  const mutation = useMutation({
    mutationFn: () => removeCartItem(itemId),
    // Wait for the fresh cart, so the item doesn't sit there looking removable.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['cart'] }),
    onError: (error) => {
      // Already gone (another tab removed or booked it): show the cart as it is.
      if (error instanceof ApiError && error.errorCode === 'Cart.ItemNotFound') {
        return queryClient.invalidateQueries({ queryKey: ['cart'] });
      }
    },
  });

  return (
    <>
      <button type="button" onClick={() => mutation.mutate()} disabled={disabled || mutation.isPending}>
        {mutation.isPending ? 'Removing…' : 'Remove'}
      </button>
      {mutation.isError && (
        <span role="alert" className="cart-error">
          {formErrorMessage(mutation.error, [])}
        </span>
      )}
    </>
  );
}

// The message for a failed checkout, chosen by errorCode.
function checkoutError(error: Error): string {
  if (!(error instanceof ApiError)) {
    return 'Something went wrong. Please try again.';
  }

  switch (error.errorCode) {
    case 'Booking.RoomUnavailable':
      return 'Someone else has just booked one of these rooms for some of these nights. Remove it and check out the rest.';
    case 'Booking.PaymentPending':
      return 'You already have a booking waiting for payment. Finish paying for it, or wait for it to expire (at most 30 minutes), before booking again.';
    case 'Idempotency.RequestInProgress':
      return 'Your checkout is still being processed. Wait a moment, then press Check out again.';
    case 'Payment.ProviderUnavailable':
      return "The payment page couldn't be opened, so nothing was reserved. Please try again.";
    case 'Booking.RoomCannotHostParty':
      return 'One of these rooms is too small for its guests. Remove it and add it again with fewer guests.';
    case 'Room.NotFound':
      return 'One of these rooms is no longer offered. Remove it and check out the rest.';
    case 'Idempotency.ResultMissing':
      return error.message;
  }

  // No login, or the session ended while the page was open.
  if (error.status === 401) {
    return error.message;
  }

  // Validation (a check-in date that has passed since it was added), 429, no
  // network, a server error. After a network error, pressing Check out again is
  // safe: the same idempotency key goes with it.
  return formErrorMessage(error, []) ?? 'Something went wrong. Please try again.';
}
