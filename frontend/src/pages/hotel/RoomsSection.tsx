import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useLocation, useNavigate } from 'react-router';
import { addCartItem } from '../../api/cart';
import { getHotelRooms, type AvailableRoom, type Stay } from '../../api/hotels';
import { ApiError } from '../../api/types';
import { formErrorMessage } from '../../auth/formErrors';
import { withReturnTo } from '../../auth/returnTo';
import { useCurrentUser } from '../../auth/session';
import { LoadError } from '../../components/LoadError';
import { Pagination } from '../../components/Pagination';
import { formatDate, formatPrice, guestsText } from '../../format';
import { hotelHref, stayKey } from './stayParams';
import { StayForm } from './StayForm';

// The fields the stay form shows. Other validation errors go above the rooms.
const FORM_FIELDS = ['checkIn', 'checkOut', 'adults', 'children'];

interface RoomsSectionProps {
  hotelId: string;
  stay: Stay;
  page: number;
}

// The stay form and the rooms that fit it. The stay lives in the URL, like the
// search page's filters: "Check availability" goes to a new URL, and this
// component reads the stay from the props on every render.
export function RoomsSection({ hotelId, stay, page }: RoomsSectionProps) {
  const navigate = useNavigate();

  const { data, isPending, isError, error, isPlaceholderData, refetch } = useQuery({
    queryKey: ['hotels', hotelId, 'rooms', stay, page],
    queryFn: () => getHotelRooms(hotelId, stay, page),
    placeholderData: keepPreviousData,
  });

  const isInvalid = error instanceof ApiError && error.status === 400;
  const fieldErrors = isInvalid ? error.errors : {};
  const hasFormFieldErrors = FORM_FIELDS.some((field) => field in fieldErrors);
  const otherErrors = isInvalid ? formErrorMessage(error, FORM_FIELDS) : null;
  const { checkIn, checkOut } = stay;
  const hasDates = checkIn !== undefined && checkOut !== undefined;

  return (
    <section className="hotel-rooms" aria-labelledby="rooms-heading">
      <h2 id="rooms-heading">Rooms</h2>

      {/* A new stay in the URL makes a fresh form, filled from it (see SearchPage). */}
      <StayForm
        key={stayKey(stay)}
        initial={stay}
        onSubmit={(newStay) => navigate(hotelHref(hotelId, newStay), { preventScrollReset: true })}
        clearHref={hotelHref(hotelId)}
        errors={fieldErrors}
      />

      {isPlaceholderData && <p role="status">Updating rooms…</p>}

      {isPending ? (
        <p role="status">Loading rooms…</p>
      ) : isError ? (
        isInvalid ? (
          <div role="alert" className="error-box">
            {hasFormFieldErrors && <p>Some details need fixing. See the messages next to them.</p>}
            {otherErrors && <p>{otherErrors}</p>}
          </div>
        ) : (
          <LoadError what="the rooms" error={error} onRetry={() => refetch()} />
        )
      ) : data.totalCount === 0 ? (
        <p className="muted">
          {hasDates
            ? 'No room is free for these dates and guests. Try other dates or fewer guests.'
            : 'This hotel has no rooms for this many guests.'}
        </p>
      ) : data.items.length === 0 ? (
        <p className="muted">
          There is no page {data.page}. <Link to={hotelHref(hotelId, stay)}>Go to page 1</Link>.
        </p>
      ) : (
        <>
          {checkIn !== undefined && checkOut !== undefined && (
            <p className="muted">
              Free from {formatDate(checkIn)} to {formatDate(checkOut)}:
            </p>
          )}
          {/* Keyed by the stay, so a new stay starts every row fresh and an
              "Added" message never sticks to dates it wasn't for. */}
          <ul key={stayKey(stay)} className="room-list">
            {data.items.map((room) => (
              <li key={room.id} className="room">
                <RoomDetails room={room} />
                <AddToCart hotelId={hotelId} room={room} stay={stay} />
              </li>
            ))}
          </ul>
          <Pagination
            page={data.page}
            totalPages={data.totalPages}
            hrefFor={(pageNumber) => hotelHref(hotelId, stay, pageNumber)}
          />
        </>
      )}
    </section>
  );
}

function RoomDetails({ room }: { room: AvailableRoom }) {
  return (
    <div className="room-details">
      <strong>
        {room.type} room {room.number}
      </strong>
      <span className="muted">
        Up to {guestsText(room.adults, 'adult')}
        {room.children > 0 && ` and ${guestsText(room.children, 'child')}`}
      </span>
      <span>
        {formatPrice(room.nightlyRate, room.currency)}
        <span className="muted"> / night</span>
      </span>
      {room.nights !== null && room.total !== null && (
        <span>
          {room.nights === 1 ? '1 night' : `${room.nights} nights`}:{' '}
          <strong>{formatPrice(room.total, room.currency)}</strong>
        </span>
      )}
    </div>
  );
}

interface AddToCartProps {
  hotelId: string;
  room: AvailableRoom;
  stay: Stay;
}

// The button of one room. Each row has its own mutation, so each row has its
// own "Adding…", error and "Added" state.
function AddToCart({ hotelId, room, stay }: AddToCartProps) {
  const user = useCurrentUser();
  const location = useLocation();
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: addCartItem,
    onSuccess: (cart) => {
      // The answer is the whole cart. Keep it, so the cart page can show it
      // without loading it again.
      queryClient.setQueryData(['cart'], cart);
    },
    onError: (error) => {
      // The room was removed after the list loaded: load the list again.
      if (error instanceof ApiError && error.errorCode === 'Room.NotFound') {
        queryClient.invalidateQueries({ queryKey: ['hotels', hotelId, 'rooms'] });
      }
    },
  });

  // Pulled out into consts so TypeScript knows, inside handleAdd too, that
  // they are strings after the check below.
  const { checkIn, checkOut } = stay;
  if (checkIn === undefined || checkOut === undefined) {
    return <p className="muted room-action">Choose dates to add this room.</p>;
  }

  if (user === null) {
    // Coming back after logging in keeps the dates, because they are in the URL.
    return (
      <Link className="room-action" to={withReturnTo('/login', location.pathname + location.search)}>
        Log in to book
      </Link>
    );
  }

  // An arrow function, not `function handleAdd()`: a function declaration is
  // hoisted to the top of AddToCart, where TypeScript can't know the dates exist.
  const handleAdd = () =>
    mutation.mutate({
      roomId: room.id,
      checkIn,
      checkOut,
      // The same defaults the room list used, so we add the stay that was priced.
      adults: stay.adults ?? 2,
      children: stay.children ?? 0,
    });

  return (
    <div className="room-action">
      <button
        type="button"
        className="button-primary"
        onClick={handleAdd}
        disabled={mutation.isPending || mutation.isSuccess}
      >
        {mutation.isPending ? 'Adding…' : mutation.isSuccess ? 'In your cart' : 'Add to cart'}
      </button>
      {mutation.isSuccess && (
        <p role="status">
          Added. You have {mutation.data.itemCount === 1 ? '1 stay' : `${mutation.data.itemCount} stays`} in your{' '}
          <Link to="/cart">cart</Link>.
        </p>
      )}
      {mutation.isError && (
        <p role="alert" className="room-error">
          {addToCartError(mutation.error)}
        </p>
      )}
    </div>
  );
}

// The message for a failed add, chosen by errorCode.
function addToCartError(error: Error): string {
  if (!(error instanceof ApiError)) {
    return 'Something went wrong. Please try again.';
  }

  switch (error.errorCode) {
    case 'Cart.Full':
      return 'Your cart holds at most 10 stays. Remove one before adding another.';
    case 'Cart.CurrencyMismatch':
      return 'Your cart has stays priced in another currency. Check those out before adding this one.';
    case 'Cart.RoomCannotHostParty':
      return 'This room is too small for your party.';
    case 'Cart.Unavailable':
      return 'The cart is unavailable right now. Please try again in a moment.';
    case 'Room.NotFound':
      return 'This room is no longer offered. The list has been updated.';
  }

  // No login, or the session ended while the page was open.
  if (error.status === 401) {
    return error.message;
  }

  // Anything else (a date that has passed since the page loaded, 429, no
  // network, a server error) gets the shared message.
  return formErrorMessage(error, []) ?? 'Something went wrong. Please try again.';
}
