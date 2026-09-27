import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router';
import { getTrendingCities } from '../../api/cities';
import { LoadError } from '../../components/LoadError';
import { Thumbnail } from '../../components/Thumbnail';

const CITY_COUNT = 5;

export function TrendingCitiesSection() {
  const { data, isPending, isError, error, refetch } = useQuery({
    queryKey: ['cities', 'trending', CITY_COUNT],
    queryFn: () => getTrendingCities(CITY_COUNT),
  });

  return (
    <section className="home-section" aria-labelledby="cities-heading">
      <h2 id="cities-heading">Trending destinations</h2>
      {isPending ? (
        <p role="status">Loading destinations…</p>
      ) : isError ? (
        <LoadError what="the trending destinations" error={error} onRetry={() => refetch()} />
      ) : data.items.length === 0 ? (
        <p className="muted">No trending destinations yet.</p>
      ) : (
        <ul className="card-grid">
          {data.items.map((city) => (
            <li key={city.id}>
              <Link to={`/hotels?cityId=${city.id}`} className="card">
                <Thumbnail url={city.thumbnailUrl} />
                <div className="card-body">
                  <strong>{city.name}</strong>
                  <span className="muted">{city.country}</span>
                  <span className="muted">
                    {city.hotelCount} {city.hotelCount === 1 ? 'hotel' : 'hotels'}
                  </span>
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
