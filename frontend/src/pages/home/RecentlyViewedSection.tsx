import { useQuery } from '@tanstack/react-query';
import { getViewedHotels } from '../../api/hotels';
import { HotelCard } from '../../components/HotelCard';
import { LoadError } from '../../components/LoadError';

interface RecentlyViewedSectionProps {
  userId: string;
}

// Only rendered for a logged-in user (see HomePage).
export function RecentlyViewedSection({ userId }: RecentlyViewedSectionProps) {
  const { data, isPending, isError, error, refetch } = useQuery({
    // The user id is part of the key, so each user's list is cached separately
    // and one user can never be shown another user's cached list.
    queryKey: ['viewed-hotels', userId],
    queryFn: getViewedHotels,
    // Opening a hotel changes this list, so ask again every time Home is shown.
    // The cached list still appears at once while the new one loads.
    staleTime: 0,
  });

  return (
    <section className="home-section" aria-labelledby="viewed-heading">
      <h2 id="viewed-heading">Recently viewed</h2>
      {isPending ? (
        <p role="status">Loading your recently viewed hotels…</p>
      ) : isError ? (
        <LoadError what="your recently viewed hotels" error={error} onRetry={() => refetch()} />
      ) : data.length === 0 ? (
        <p className="muted">Hotels you open will show up here.</p>
      ) : (
        <ul className="card-grid">
          {data.map((hotel) => (
            <li key={hotel.id}>
              <HotelCard hotel={hotel} />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
