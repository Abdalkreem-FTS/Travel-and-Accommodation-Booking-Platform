import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Link, useSearchParams } from 'react-router';
import { listHotelRooms } from '../../../api/hotels';
import { LoadError } from '../../../components/LoadError';
import { Pagination } from '../../../components/Pagination';
import { formatPrice, guestsText } from '../../../format';
import { readPage } from '../readPage';

// The "Rooms" section of /admin/hotels/:id?page=2
export function HotelRooms({ hotelId }: { hotelId: string }) {
  const [searchParams] = useSearchParams();
  const page = readPage(searchParams.get('page'));

  const { data, isPending, isError, error, isPlaceholderData, refetch } = useQuery({
    queryKey: ['hotels', hotelId, 'rooms', 'admin', page],
    queryFn: () => listHotelRooms(hotelId, page),
    placeholderData: keepPreviousData,
  });

  const here = `/admin/hotels/${hotelId}`;
  const hrefFor = (n: number) => (n === 1 ? here : `${here}?page=${n}`);

  return (
    <section className="admin-section" aria-labelledby="hotel-rooms-heading" aria-busy={isPlaceholderData}>
      <div className="admin-heading">
        <h2 id="hotel-rooms-heading">Rooms</h2>
        <Link to={`/admin/hotels/${hotelId}/rooms/new`} className="admin-add">
          Add a room
        </Link>
      </div>

      {isPending ? (
        <p role="status">Loading rooms…</p>
      ) : isError ? (
        <LoadError what="the rooms" error={error} onRetry={() => refetch()} />
      ) : data.totalCount === 0 ? (
        <p className="muted">This hotel has no rooms yet, so guests can't find it in the search.</p>
      ) : data.items.length === 0 ? (
        <p className="muted">
          There is no page {data.page}. <Link to={hrefFor(1)}>Go to page 1</Link>.
        </p>
      ) : (
        <>
          <ul className="admin-list">
            {data.items.map((room) => (
              <li key={room.id} className="admin-row">
                <div>
                  <Link to={`/admin/rooms/${room.id}`} className="admin-row-title">
                    Room {room.number}
                  </Link>
                  <p className="muted">
                    {room.type} · {guestsText(room.adults, 'adult')}
                    {room.children > 0 && `, ${guestsText(room.children, 'child')}`}
                  </p>
                </div>
                <span>{formatPrice(room.nightlyRate, room.currency)} a night</span>
              </li>
            ))}
          </ul>
          <Pagination page={data.page} totalPages={data.totalPages} hrefFor={hrefFor} />
        </>
      )}
    </section>
  );
}
