import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Link, useSearchParams } from 'react-router';
import { listCityHotels } from '../../../api/cities';
import { LoadError } from '../../../components/LoadError';
import { Pagination } from '../../../components/Pagination';
import { Stars } from '../../../components/Stars';
import { readPage } from '../readPage';
import { SearchForm } from '../SearchForm';

// The "Hotels" section of /admin/cities/:id?search=petra&page=2
//
// Every hotel in the city, including the ones with no rooms yet, which the
// guests' search leaves out. The search and page are in the URL.
export function CityHotels({ cityId }: { cityId: string }) {
  const [searchParams, setSearchParams] = useSearchParams();
  const search = searchParams.get('search') ?? '';
  const page = readPage(searchParams.get('page'));

  const { data, isPending, isError, error, isPlaceholderData, refetch } = useQuery({
    queryKey: ['cities', cityId, 'hotels', search, page],
    queryFn: () => listCityHotels(cityId, search, page),
    placeholderData: keepPreviousData,
  });

  const here = `/admin/cities/${cityId}`;
  const hrefFor = (n: number) => {
    const params = new URLSearchParams();
    if (search !== '') params.set('search', search);
    if (n > 1) params.set('page', String(n));
    const query = params.toString();
    return query === '' ? here : `${here}?${query}`;
  };

  // replace: a search doesn't add a Back step here; the page is about the city.
  const handleSearch = (text: string) => {
    setSearchParams(text === '' ? {} : { search: text }, { replace: true });
  };

  return (
    <section className="admin-section" aria-labelledby="city-hotels-heading" aria-busy={isPlaceholderData}>
      <div className="admin-heading">
        <h2 id="city-hotels-heading">Hotels</h2>
        <Link to={`/admin/hotels/new?cityId=${cityId}`} className="admin-add">
          Add a hotel
        </Link>
      </div>

      <SearchForm key={search} initial={search} onSearch={handleSearch} />

      {isPending ? (
        <p role="status">Loading hotels…</p>
      ) : isError ? (
        <LoadError what="the hotels" error={error} onRetry={() => refetch()} />
      ) : data.totalCount === 0 ? (
        <p className="muted">
          {search === '' ? 'This city has no hotels yet.' : `No hotel name contains “${search}”.`}
        </p>
      ) : data.items.length === 0 ? (
        <p className="muted">
          There is no page {data.page}. <Link to={hrefFor(1)}>Go to page 1</Link>.
        </p>
      ) : (
        <>
          <ul className="admin-list">
            {data.items.map((hotel) => (
              <li key={hotel.id} className="admin-row">
                <div>
                  <Link to={`/admin/hotels/${hotel.id}`} className="admin-row-title">
                    {hotel.name}
                  </Link>
                  <p>
                    <Stars rating={hotel.starRating} />
                  </p>
                </div>
                {hotel.roomCount === 0 ? (
                  <span className="admin-badge">No rooms: not bookable yet</span>
                ) : (
                  <span className="muted">{hotel.roomCount === 1 ? '1 room' : `${hotel.roomCount} rooms`}</span>
                )}
              </li>
            ))}
          </ul>
          <Pagination page={data.page} totalPages={data.totalPages} hrefFor={hrefFor} />
        </>
      )}
    </section>
  );
}
