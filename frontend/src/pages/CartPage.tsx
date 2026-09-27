import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router';
import { emptyCart, getCart, type CartItem } from '../api/cart';
import { formErrorMessage } from '../auth/formErrors';
import { withReturnTo } from '../auth/returnTo';
import { useCurrentUser } from '../auth/session';
import { LoadError } from '../components/LoadError';
import { CheckoutGroup } from './cart/CheckoutGroup';
import './CartPage.css';

// /cart
//
// The stays the user is holding, grouped by hotel. One booking can only be at
// one hotel, so each hotel is checked out on its own.
export function CartPage() {
  const user = useCurrentUser();

  if (user === null) {
    return (
      <section>
        <h1>Your cart</h1>
        <p>
          <Link to={withReturnTo('/login', '/cart')}>Log in</Link> to see your cart.
        </p>
      </section>
    );
  }

  return <Cart />;
}

// Split from CartPage so the query only runs for a logged-in user: a hook can't
// be skipped with an early return, but a whole component can.
function Cart() {
  const { data: cart, isPending, isError, error, refetch } = useQuery({
    queryKey: ['cart'],
    queryFn: getCart,
  });

  if (isPending) {
    return <p role="status">Loading your cart…</p>;
  }

  if (isError) {
    return <LoadError what="your cart" error={error} onRetry={() => refetch()} />;
  }

  // The server couldn't reach the cart store and answered an empty cart. That
  // doesn't mean the cart is empty, so don't say it is.
  if (cart.degraded) {
    return (
      <section>
        <h1>Your cart</h1>
        <div role="alert" className="error-box">
          <p>We can't reach your cart right now. The stays you added are not lost.</p>
          <button type="button" onClick={() => refetch()}>
            Try again
          </button>
        </div>
      </section>
    );
  }

  if (cart.items.length === 0) {
    return (
      <section>
        <h1>Your cart</h1>
        <p>Your cart is empty.</p>
        <Link to="/hotels">Find a hotel</Link>
      </section>
    );
  }

  return (
    <section className="cart-page">
      <div className="cart-heading">
        <h1>Your cart</h1>
        <EmptyCartButton />
      </div>
      <p className="muted">
        Rooms in your cart are not reserved yet: someone else can still book them until you check out. Each hotel
        is checked out and paid for separately.
      </p>

      {groupByHotel(cart.items).map(([hotelId, items]) => (
        // The key holds the group's item ids, so a group whose stays change
        // (one removed, one added) becomes a new CheckoutGroup with a fresh
        // Idempotency-Key. See CheckoutGroup for why that matters.
        <CheckoutGroup key={`${hotelId}:${items.map((item) => item.id).join(',')}`} hotelId={hotelId} items={items} />
      ))}
    </section>
  );
}

function EmptyCartButton() {
  const queryClient = useQueryClient();
  const mutation = useMutation({
    mutationFn: emptyCart,
    // Returning the promise keeps the button on "Emptying…" until the cart has
    // been loaded again, so the old items never show next to an idle button.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['cart'] }),
  });

  const handleClick = () => {
    if (window.confirm('Remove every stay from your cart?')) {
      mutation.mutate();
    }
  };

  return (
    <div>
      <button type="button" onClick={handleClick} disabled={mutation.isPending}>
        {mutation.isPending ? 'Emptying…' : 'Empty cart'}
      </button>
      {mutation.isError && (
        <p role="alert" className="cart-error">
          {formErrorMessage(mutation.error, [])}
        </p>
      )}
    </div>
  );
}

// [[hotelId, items], ...] in the order the hotels first appear in the cart.
// Map keeps insertion order, like a C# Dictionary usually does (but guaranteed).
function groupByHotel(items: CartItem[]): [string, CartItem[]][] {
  const groups = new Map<string, CartItem[]>();
  for (const item of items) {
    const group = groups.get(item.hotelId);
    if (group) group.push(item);
    else groups.set(item.hotelId, [item]);
  }
  return [...groups];
}
