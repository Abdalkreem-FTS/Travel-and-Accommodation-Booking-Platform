import { Link } from 'react-router';
import type { HotelSummary, Stay } from '../api/hotels';
import { formatPrice } from '../format';
import { hotelHref } from '../pages/hotel/stayParams';
import { Stars } from './Stars';
import { Thumbnail } from './Thumbnail';

interface HotelCardProps {
  hotel: HotelSummary;
  // The dates and guests to carry over to the hotel's page, if the user chose any.
  stay?: Stay;
}

// One hotel in a card grid. Used by the search results and "Recently viewed".
// The caller puts it inside an <li>.
export function HotelCard({ hotel, stay }: HotelCardProps) {
  return (
    <Link to={hotelHref(hotel.id, stay)} className="card">
      <Thumbnail url={hotel.thumbnailUrl} />
      <div className="card-body">
        <strong>{hotel.name}</strong>
        <span className="muted">{hotel.cityName}</span>
        <Stars rating={hotel.starRating} />
        <span>
          From <strong>{formatPrice(hotel.fromPrice, hotel.currency)}</strong>
          <span className="muted"> / night</span>
        </span>
      </div>
    </Link>
  );
}
