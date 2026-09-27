import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Link, useSearchParams } from 'react-router';
import { listCities } from '../../api/cities';
import { LoadError } from '../../components/LoadError';
import { Pagination } from '../../components/Pagination';
import { readPage } from './readPage';
import { SearchForm } from './SearchForm';
import './Admin.css';

// /admin/cities?search=amm&page=2
//
// Every city, by name. The search and page live in the URL (like the hotel
// search), so Back and a shared link show the same list.
export function AdminCitiesPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const search = searchParams.get('search') ?? '';
  const page = readPage(searchParams.get('page'));

  const { data, isPending, isError, error, isPlaceholderData, refetch } = useQuery({
    queryKey: ['cities', 'admin', search, page],
    queryFn: () => listCities(search, page),
    placeholderData: keepPreviousData,
  });

  // A new search starts again at page 1.
  const handleSearch = (text: string) => {
    setSearchParams(text === '' ? {} : { search: text });
  };

  const hrefFor = (n: number) => {
    const params = new URLSearchParams();
    if (search !== '') params.set('search', search);
    if (n > 1) params.set('page', String(n));
    const query = params.toString();
    return query === '' ? '/admin/cities' : `/admin/cities?${query}`;
  };

  return (
    <section className="admin-page" aria-busy={isPlaceholderData}>
      <div className="admin-heading">
        <h1>Cities</h1>
        <Link to="/admin/cities/new" className="admin-add">
          Add a city
        </Link>
      </div>

      {/* key: when the URL's search changes (Back, a link), start the box
          over with the new text instead of keeping what was typed. */}
      <SearchForm key={search} initial={search} onSearch={handleSearch} />

      {isPending ? (
        <p role="status">Loading cities…</p>
      ) : isError ? (
        <LoadError what="the cities" error={error} onRetry={() => refetch()} />
      ) : data.totalCount === 0 ? (
        <p className="muted">
          {search === '' ? 'There are no cities yet.' : `No city name contains “${search}”.`}
        </p>
      ) : data.items.length === 0 ? (
        <p className="muted">
          There is no page {data.page}. <Link to={hrefFor(1)}>Go to page 1</Link>.
        </p>
      ) : (
        <>
          <ul className="admin-list">
            {data.items.map((city) => (
              <li key={city.id} className="admin-row">
                <div>
                  <Link to={`/admin/cities/${city.id}`} className="admin-row-title">
                    {city.name}
                  </Link>
                  <p className="muted">
                    {city.country} · {city.postOffice}
                  </p>
                </div>
                <span className="muted">{city.hotelCount === 1 ? '1 hotel' : `${city.hotelCount} hotels`}</span>
              </li>
            ))}
          </ul>
          <Pagination page={data.page} totalPages={data.totalPages} hrefFor={hrefFor} />
        </>
      )}
    </section>
  );
}
