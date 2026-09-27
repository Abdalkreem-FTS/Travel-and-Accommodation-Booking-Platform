import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router';
import { getFeaturedDeals } from '../../api/deals';
import { LoadError } from '../../components/LoadError';
import { Stars } from '../../components/Stars';
import { Thumbnail } from '../../components/Thumbnail';
import { formatDate, formatPrice } from '../../format';

const DEAL_COUNT = 5;

export function DealsSection() {
  const { data, isPending, isError, error, refetch } = useQuery({
    queryKey: ['deals', 'featured', DEAL_COUNT],
    queryFn: () => getFeaturedDeals(DEAL_COUNT),
  });

  return (
    <section className="home-section" aria-labelledby="deals-heading">
      <h2 id="deals-heading">Featured deals</h2>
      {isPending ? (
        <p role="status">Loading deals…</p>
      ) : isError ? (
        <LoadError what="the deals" error={error} onRetry={() => refetch()} />
      ) : data.length === 0 ? (
        <p className="muted">No deals running right now.</p>
      ) : (
        <ul className="card-grid">
          {data.map((deal) => (
            <li key={deal.id}>
              <Link to={`/hotels/${deal.hotelId}`} className="card">
                <Thumbnail url={deal.thumbnailUrl} />
                <span className="deal-badge">−{deal.discountPercentage}%</span>
                <div className="card-body">
                  <strong>{deal.hotelName}</strong>
                  <span className="muted">{deal.cityName}</span>
                  <Stars rating={deal.starRating} />
                  <span>{deal.roomType}</span>
                  <span>
                    <s className="muted">{formatPrice(deal.originalPrice, deal.currency)}</s>{' '}
                    <strong>{formatPrice(deal.discountedPrice, deal.currency)}</strong>
                    <span className="muted"> / night</span>
                  </span>
                  <span className="muted">Ends {formatDate(deal.endsOn)}</span>
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
