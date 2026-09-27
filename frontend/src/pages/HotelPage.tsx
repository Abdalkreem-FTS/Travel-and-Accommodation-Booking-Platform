import { useQuery } from '@tanstack/react-query';
import { Link, useParams, useSearchParams } from 'react-router';
import { getCity } from '../api/cities';
import { getHotel, type HotelDetails } from '../api/hotels';
import { ApiError } from '../api/types';
import { LoadError } from '../components/LoadError';
import { Stars } from '../components/Stars';
import { RoomsSection } from './hotel/RoomsSection';
import { readRoomsPage, readStay } from './hotel/stayParams';
import './HotelPage.css';

// /hotels/:id?checkIn=...&checkOut=...&adults=2&children=0
//
// The hotel on top, its rooms underneath. The hotel and the rooms are two
// separate requests with their own loading and error states, so a slow room
// list doesn't hide the hotel.
export function HotelPage() {
  // useParams reads the ":id" part of the route. The router only shows this
  // page when there is one, so the '' is just to satisfy TypeScript.
  const { id = '' } = useParams();
  const [searchParams] = useSearchParams();

  const { data: hotel, isPending, isError, error, refetch } = useQuery({
    queryKey: ['hotels', id],
    queryFn: () => getHotel(id),
    // A 404 stays a 404; retrying it only makes the user wait. Other failures
    // (network, 5xx) may pass, so they get TanStack Query's usual 3 retries.
    retry: (failureCount, err) => !(err instanceof ApiError && err.status === 404) && failureCount < 3,
  });

  if (isPending) {
    return <p role="status">Loading the hotel…</p>;
  }

  if (isError) {
    if (error instanceof ApiError && error.status === 404) {
      return (
        <section>
          <h1>Hotel not found</h1>
          <p>This hotel doesn't exist, or it has been removed.</p>
          <Link to="/hotels">Find another hotel</Link>
        </section>
      );
    }
    return <LoadError what="the hotel" error={error} onRetry={() => refetch()} />;
  }

  return (
    <article className="hotel-page">
      <header className="hotel-header">
        <h1>{hotel.name}</h1>
        <Stars rating={hotel.starRating} />
        <CityName cityId={hotel.cityId} />
      </header>

      <Gallery hotel={hotel} />

      <section aria-labelledby="about-heading">
        <h2 id="about-heading">About this hotel</h2>
        <p className="hotel-description">{hotel.description}</p>
        <p className="muted">Owned by {hotel.owner}</p>
      </section>

      <Amenities hotel={hotel} />

      <RoomsSection hotelId={hotel.id} stay={readStay(searchParams)} page={readRoomsPage(searchParams)} />
    </article>
  );
}

// The hotel only has the city's id, so the name is one more (cached) request.
// It is a small detail: while it loads, or if it fails, the line is left out
// rather than showing a spinner or an error for it.
function CityName({ cityId }: { cityId: string }) {
  const { data: city } = useQuery({
    queryKey: ['cities', cityId],
    queryFn: () => getCity(cityId),
    staleTime: 5 * 60_000,
  });

  if (city === undefined) {
    return null;
  }
  return (
    <p className="muted">
      {city.name}, {city.country}
    </p>
  );
}

function Gallery({ hotel }: { hotel: HotelDetails }) {
  // The API sends them in order already; sorting again costs nothing and keeps
  // the order right even if that changes. toSorted() leaves the cached array alone.
  const images = hotel.images.toSorted((a, b) => a.position - b.position);

  if (images.length === 0) {
    return hotel.thumbnailUrl === null ? null : (
      <img className="hotel-hero" src={hotel.thumbnailUrl} alt={hotel.name} />
    );
  }

  return (
    <ul className="hotel-gallery" aria-label="Photos">
      {images.map((image, index) => (
        <li key={image.url}>
          <figure>
            {/* The first photo loads right away; the rest only when scrolled to. */}
            <img
              src={image.url}
              alt={image.caption ?? `${hotel.name}, photo ${index + 1}`}
              loading={index === 0 ? 'eager' : 'lazy'}
            />
            {image.caption && <figcaption>{image.caption}</figcaption>}
          </figure>
        </li>
      ))}
    </ul>
  );
}

function Amenities({ hotel }: { hotel: HotelDetails }) {
  if (hotel.amenities.length === 0) {
    return null;
  }

  return (
    <section aria-labelledby="amenities-heading">
      <h2 id="amenities-heading">Amenities</h2>
      <ul className="hotel-amenities">
        {hotel.amenities.map((amenity) => (
          // title shows the longer description when the pointer rests on it.
          <li key={amenity.id} title={amenity.description}>
            {amenity.name}
          </li>
        ))}
      </ul>
    </section>
  );
}
