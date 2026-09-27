import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Link, useSearchParams } from 'react-router';
import { listRoomDeals, type Deal } from '../../../api/deals';
import { LoadError } from '../../../components/LoadError';
import { Pagination } from '../../../components/Pagination';
import { formatDateWithYear, todayIso } from '../../../format';
import { readPage } from '../readPage';

// The "Deals" section of /admin/rooms/:id?page=2
export function RoomDeals({ roomId }: { roomId: string }) {
  const [searchParams] = useSearchParams();
  const page = readPage(searchParams.get('page'));

  const { data, isPending, isError, error, isPlaceholderData, refetch } = useQuery({
    queryKey: ['rooms', roomId, 'deals', page],
    queryFn: () => listRoomDeals(roomId, page),
    placeholderData: keepPreviousData,
  });

  const here = `/admin/rooms/${roomId}`;
  const hrefFor = (n: number) => (n === 1 ? here : `${here}?page=${n}`);
  // Worked out once, so every row compares with the same day.
  const today = todayIso();

  return (
    <section className="admin-section" aria-labelledby="room-deals-heading" aria-busy={isPlaceholderData}>
      <div className="admin-heading">
        <h2 id="room-deals-heading">Deals</h2>
        <Link to={`/admin/rooms/${roomId}/deals/new`} className="admin-add">
          Add a deal
        </Link>
      </div>

      {isPending ? (
        <p role="status">Loading deals…</p>
      ) : isError ? (
        <LoadError what="the deals" error={error} onRetry={() => refetch()} />
      ) : data.totalCount === 0 ? (
        <p className="muted">This room has no deals.</p>
      ) : data.items.length === 0 ? (
        <p className="muted">
          There is no page {data.page}. <Link to={hrefFor(1)}>Go to page 1</Link>.
        </p>
      ) : (
        <>
          <ul className="admin-list">
            {data.items.map((deal) => (
              <li key={deal.id} className="admin-row">
                <div>
                  <Link to={`/admin/deals/${deal.id}`} className="admin-row-title">
                    {deal.discountPercentage}% off
                  </Link>
                  <p className="muted">
                    {formatDateWithYear(deal.startsOn)} to {formatDateWithYear(deal.endsOn)}
                    {deal.isFeatured && ' · Featured'}
                  </p>
                </div>
                <DealStatus deal={deal} today={today} />
              </li>
            ))}
          </ul>
          <Pagination page={data.page} totalPages={data.totalPages} hrefFor={hrefFor} />
        </>
      )}
    </section>
  );
}

// "Upcoming", "Running" or "Ended", from the browser's date. The server uses
// its own clock, so around midnight the two can disagree for a few hours.
function DealStatus({ deal, today }: { deal: Deal; today: string }) {
  // "2026-09-27" style dates sort as text in date order, so plain < works.
  const status = today < deal.startsOn ? 'upcoming' : today < deal.endsOn ? 'running' : 'ended';
  const label = { upcoming: 'Upcoming', running: 'Running', ended: 'Ended' }[status];
  return <span className={`admin-status admin-status-${status}`}>{label}</span>;
}
