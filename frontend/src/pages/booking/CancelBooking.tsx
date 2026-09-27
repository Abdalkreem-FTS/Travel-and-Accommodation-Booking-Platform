import { useMutation, useQueryClient } from '@tanstack/react-query';
import { cancelBooking, type Booking } from '../../api/bookings';
import { ApiError } from '../../api/types';
import { formErrorMessage } from '../../auth/formErrors';

// The "Cancel booking" button of a Pending or Confirmed booking. The server
// decides whether it's still allowed (up to 48 hours before check-in), so the
// button is always offered and a refusal is explained.
export function CancelBooking({ booking }: { booking: Booking }) {
  const queryClient = useQueryClient();
  const bookingKey = ['bookings', booking.id];

  const mutation = useMutation({
    mutationFn: () => cancelBooking(booking.id),
    // Load the booking again to show its new status and the refund. Returning
    // the promise keeps the button on "Cancelling…" until that's on screen.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: bookingKey }),
    onError: (error) => {
      // The booking changed under us (paid, cancelled in another tab): show it as it is now.
      if (error instanceof ApiError && error.status === 409) {
        return queryClient.invalidateQueries({ queryKey: bookingKey });
      }
    },
  });

  const paid = booking.status === 'Confirmed';

  const handleClick = () => {
    const question = paid
      ? 'Cancel this booking? You will get a full refund.'
      : 'Cancel this booking? The rooms will be released.';
    if (window.confirm(question)) {
      mutation.mutate();
    }
  };

  return (
    <section aria-labelledby="cancel-heading" className="booking-cancel">
      <h2 id="cancel-heading">Cancel</h2>
      <p className="muted">
        You can cancel up to 48 hours before check-in{paid ? ' and get a full refund' : ''}.
      </p>
      <button type="button" onClick={handleClick} disabled={mutation.isPending}>
        {mutation.isPending ? 'Cancelling…' : 'Cancel booking'}
      </button>
      {mutation.isError && (
        <p role="alert" className="booking-error">
          {cancelError(mutation.error)}
        </p>
      )}
    </section>
  );
}

// The message for a refused cancellation, chosen by errorCode.
function cancelError(error: Error): string {
  if (!(error instanceof ApiError)) {
    return 'Something went wrong. Please try again.';
  }

  switch (error.errorCode) {
    case 'Booking.CancellationWindowClosed':
      return 'This booking can no longer be cancelled: check-in is less than 48 hours away.';
    case 'Booking.InvalidTransition':
      return 'This booking can no longer be cancelled.';
    case 'Booking.PaymentJustCompleted':
      return 'Your payment has just gone through. Once the booking shows as confirmed, you can cancel it for a full refund.';
    case 'Persistence.ConcurrencyConflict':
      return 'This booking was changed a moment ago. Check its status above and try again if needed.';
    case 'Payment.ProviderUnavailable':
      return "We couldn't reach the payment provider, so nothing was changed. Please try again.";
  }

  if (error.status === 401) {
    return error.message;
  }

  return formErrorMessage(error, []) ?? 'Something went wrong. Please try again.';
}
